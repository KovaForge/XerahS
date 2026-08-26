/*
 * XerahS - The Avalonia UI implementation of ShareX
 * Copyright (c) 2007-2026 ShareX Team
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 *
 * Optionally you can also view the license at <http://www.gnu.org/licenses/>.
 */

package com.getsharex.xerahs.mobile.core.data.cloud

import com.getsharex.xerahs.mobile.core.domain.CloudAccount
import com.getsharex.xerahs.mobile.core.domain.CloudGalleryPage
import com.getsharex.xerahs.mobile.core.domain.CloudPublishRequest
import com.google.gson.Gson
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import okhttp3.FormBody
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import java.time.Instant

internal const val CLOUD_API_BASE = "https://cloud.xerahs.com/"
const val CLOUD_SETTINGS_URL = "https://cloud.xerahs.com/settings"
internal const val CLOUD_AUTHORITY = "https://cvnywevwxmajyzhhpvzl.supabase.co/"
internal const val CLOUD_CLIENT_ID = "8d8adf92-86c4-4036-a4c9-09901230f2c4"
internal const val CLOUD_REDIRECT_URI = "https://cloud.xerahs.com/auth/desktop/callback"
internal const val CLOUD_CALLBACK_URI = "xerahs://oauth/callback"
internal const val CLOUD_ISSUER = "https://cvnywevwxmajyzhhpvzl.supabase.co/auth/v1"
internal const val CLOUD_JWKS_URL = "https://cvnywevwxmajyzhhpvzl.supabase.co/auth/v1/.well-known/jwks.json"

open class CloudException(message: String, cause: Throwable? = null) : Exception(message, cause)
class CloudSecurityException(message: String, cause: Throwable? = null) : CloudException(message, cause)

internal data class CloudSession(
    val accessToken: String,
    val refreshToken: String,
    val ownerSubject: String,
    val expiresAtEpochSeconds: Long
)

class CloudApiClient(
    private val credentialStore: CloudCredentialStore,
    private val httpClient: OkHttpClient = OkHttpClient(),
    private val gson: Gson = Gson()
) {
    private val verifier = CloudJwtVerifier(httpClient)
    private val refreshMutex = Mutex()
    @Volatile private var session: CloudSession? = null

    val hasCredential: Boolean get() = runCatching { session != null || credentialStore.read() != null }.getOrDefault(false)
    val currentOwnerSubject: String? get() = session?.ownerSubject ?: runCatching { credentialStore.read()?.ownerSubject }.getOrNull()

    suspend fun exchangeAuthorizationCode(code: String, verifierValue: String, expectedNonce: String) = withContext(Dispatchers.IO) {
        val body = FormBody.Builder()
            .add("grant_type", "authorization_code")
            .add("code", code)
            .add("client_id", CLOUD_CLIENT_ID)
            .add("redirect_uri", CLOUD_REDIRECT_URI)
            .add("code_verifier", verifierValue)
            .build()
        val response = executeTokenRequest(body, "OAuth token exchange")
        val accepted = validateTokenResponse(response, expectedNonce)
        credentialStore.write(CloudRefreshCredential(accepted.ownerSubject, accepted.refreshToken))
        session = accepted
    }

    suspend fun restoreSession(): Boolean = try {
        getAccount()
        true
    } catch (error: CloudSecurityException) {
        signOut()
        false
    }

    suspend fun getAccount(): CloudAccount {
        val response = authenticatedRequest { token ->
            Request.Builder().url("${CLOUD_API_BASE}api/v1/me").bearer(token).get().build()
        }
        response.use {
            ensureSuccess("Account verification", it)
            val account = gson.fromJson(it.body?.charStream(), CloudAccount::class.java)
                ?: throw CloudSecurityException("XerahS Cloud returned an invalid account response.")
            if (!account.slug.matches(Regex("^[a-z0-9-]{1,30}$")) || !account.strongAuth) {
                throw CloudSecurityException("The XerahS Cloud account response did not pass security checks.")
            }
            return account
        }
    }

    suspend fun listItems(cursor: String? = null, limit: Int = 25): CloudGalleryPage {
        require(limit in 1..50)
        val url = "${CLOUD_API_BASE}api/v1/items".toHttpUrl().newBuilder().addQueryParameter("limit", limit.toString()).apply {
            if (!cursor.isNullOrBlank()) addQueryParameter("cursor", cursor)
        }.build()
        val response = authenticatedRequest { token -> Request.Builder().url(url).bearer(token).get().build() }
        response.use {
            ensureSuccess("Cloud history", it)
            return gson.fromJson(it.body?.charStream(), CloudGalleryPage::class.java)
                ?: throw CloudException("Cloud history returned an invalid response.")
        }
    }

    suspend fun publish(request: CloudPublishRequest, expectedOwnerSubject: String) {
        validatePublishRequest(request)
        require(expectedOwnerSubject.isNotBlank())
        val body = gson.toJson(
            mapOf(
                "url" to request.url,
                "thumbnailUrl" to request.thumbnailUrl,
                "kind" to request.kind,
                "fileName" to request.fileName,
                "capturedAt" to request.capturedAt,
                "host" to request.host,
                "contentType" to request.contentType
            )
        ).toRequestBody("application/json; charset=utf-8".toMediaType())
        val response = authenticatedRequest(expectedOwnerSubject) { token ->
            Request.Builder()
                .url("${CLOUD_API_BASE}api/v1/items/${request.clientItemId}")
                .bearer(token)
                .header("Idempotency-Key", request.clientItemId)
                .put(body)
                .build()
        }
        response.use {
            ensureSuccess("Publish", it)
            val root = parseObject(it)
            if (root.getAsJsonObject("item")?.get("id")?.asString.isNullOrBlank()) {
                throw CloudException("Publish returned an invalid response.")
            }
        }
    }

    suspend fun unpublish(clientItemId: String): Boolean {
        require(UUID_PATTERN.matches(clientItemId))
        val response = authenticatedRequest { token ->
            Request.Builder()
                .url("${CLOUD_API_BASE}api/v1/items/$clientItemId")
                .bearer(token)
                .header("Idempotency-Key", "unpublish:$clientItemId")
                .delete()
                .build()
        }
        response.use {
            if (it.code in setOf(202, 204, 404)) return it.code != 202
            ensureSuccess("Unpublish", it)
            return true
        }
    }

    fun signOut() {
        session = null
        credentialStore.clear()
    }

    private suspend fun authenticatedRequest(
        expectedOwnerSubject: String? = null,
        factory: (String) -> Request
    ): Response = withContext(Dispatchers.IO) {
        var current = getSession(forceRefresh = false, expectedOwnerSubject)
        var response = httpClient.newCall(factory(current.accessToken)).execute()
        if (response.code != 401) return@withContext response
        response.close()
        current = getSession(forceRefresh = true, expectedOwnerSubject)
        httpClient.newCall(factory(current.accessToken)).execute()
    }

    private suspend fun getSession(
        forceRefresh: Boolean,
        expectedOwnerSubject: String? = null
    ): CloudSession = refreshMutex.withLock {
        val now = Instant.now().epochSecond
        session?.takeIf { !forceRefresh && it.expiresAtEpochSeconds > now + 60 }?.let {
            requireExpectedOwner(it.ownerSubject, expectedOwnerSubject)
            return@withLock it
        }
        val credential = credentialStore.read() ?: throw CloudSecurityException("Sign in to XerahS Cloud first.")
        requireExpectedOwner(credential.ownerSubject, expectedOwnerSubject)
        val body = FormBody.Builder()
            .add("grant_type", "refresh_token")
            .add("refresh_token", credential.refreshToken)
            .add("client_id", CLOUD_CLIENT_ID)
            .build()
        val response = try {
            withContext(Dispatchers.IO) { executeTokenRequest(body, "OAuth refresh") }
        } catch (error: CloudSecurityException) {
            signOut()
            throw error
        }
        val accepted = withContext(Dispatchers.IO) { validateTokenResponse(response, null) }
        if (accepted.ownerSubject != credential.ownerSubject) {
            signOut()
            throw CloudSecurityException("OAuth refresh attempted to switch XerahS Cloud accounts.")
        }
        requireExpectedOwner(accepted.ownerSubject, expectedOwnerSubject)
        credentialStore.write(CloudRefreshCredential(accepted.ownerSubject, accepted.refreshToken))
        session = accepted
        accepted
    }

    private fun requireExpectedOwner(actualOwnerSubject: String, expectedOwnerSubject: String?) {
        if (!CloudOwnerBinding.matchesExpected(actualOwnerSubject, expectedOwnerSubject)) {
            throw CloudSecurityException("The Cloud publish belongs to a different XerahS Cloud account.")
        }
    }

    private fun executeTokenRequest(body: FormBody, operation: String): TokenResponse {
        val request = Request.Builder().url("${CLOUD_AUTHORITY}auth/v1/oauth/token").post(body).build()
        httpClient.newCall(request).execute().use { response ->
            if (!response.isSuccessful) {
                if (response.code in 400..499) throw CloudSecurityException("$operation was rejected with HTTP ${response.code}.")
                throw CloudException("$operation failed with HTTP ${response.code}.")
            }
            return gson.fromJson(response.body?.charStream(), TokenResponse::class.java)
                ?: throw CloudSecurityException("$operation returned an invalid response.")
        }
    }

    private fun validateTokenResponse(token: TokenResponse, expectedNonce: String?): CloudSession {
        if (token.accessToken.isBlank() || token.refreshToken.isBlank() || token.expiresIn !in 1..3600) {
            throw CloudSecurityException("OAuth token response failed security validation.")
        }
        val verified = verifier.verifyAccess(token.accessToken)
        if (expectedNonce != null && token.idToken.isNullOrBlank()) {
            throw CloudSecurityException("The OpenID Connect response did not contain an ID token.")
        }
        token.idToken?.let { verifier.verifyId(it, verified.subject, expectedNonce) }
        val responseExpiry = Instant.now().plusSeconds(token.expiresIn.toLong()).epochSecond
        return CloudSession(token.accessToken, token.refreshToken, verified.subject, minOf(responseExpiry, verified.expiresAtEpochSeconds))
    }

    private fun validatePublishRequest(request: CloudPublishRequest) {
        require(UUID_PATTERN.matches(request.clientItemId))
        require(request.kind == "screenshot" || request.kind == "screencast")
        require(request.fileName.isNotBlank() && request.fileName.length <= 255 && '/' !in request.fileName && '\\' !in request.fileName)
        if (!CloudPublishPolicy.isCredentialFreeHttps(request.url)) {
            throw CloudSecurityException("Only credential-free HTTPS destination URLs can be published.")
        }
        runCatching { Instant.parse(request.capturedAt) }.getOrElse {
            throw CloudSecurityException("Cloud publish capturedAt must be an ISO-8601 UTC timestamp.")
        }
    }

    private fun ensureSuccess(operation: String, response: Response) {
        if (response.isSuccessful) return
        val root = runCatching { parseObject(response) }.getOrNull()
        val error = root?.getAsJsonObject("error")
        val code = sanitize(error?.get("code")?.asString, 64)
        val message = sanitize(error?.get("message")?.asString, 256)
        val correlation = sanitize(error?.get("correlationId")?.asString ?: response.header("X-Correlation-ID"), 128)
        val detail = listOf(code, message).filter { it.isNotBlank() }.joinToString(": ")
        val suffix = buildString {
            if (detail.isNotBlank()) append(" $detail")
            if (correlation.isNotBlank()) append(" (correlation $correlation)")
        }
        val text = "$operation failed with HTTP ${response.code}.$suffix"
        if (response.code == 401) throw CloudSecurityException(text) else throw CloudException(text)
    }

    private fun parseObject(response: Response): JsonObject =
        JsonParser.parseString(response.body?.string().orEmpty()).asJsonObject

    private fun sanitize(value: String?, max: Int): String = value.orEmpty().replace('\r', ' ').replace('\n', ' ').trim().take(max)

    private fun Request.Builder.bearer(token: String): Request.Builder =
        header("Authorization", "Bearer $token").header("Accept", "application/json")

    private data class TokenResponse(
        @com.google.gson.annotations.SerializedName("access_token") val accessToken: String = "",
        @com.google.gson.annotations.SerializedName("refresh_token") val refreshToken: String = "",
        @com.google.gson.annotations.SerializedName("id_token") val idToken: String? = null,
        @com.google.gson.annotations.SerializedName("expires_in") val expiresIn: Int = 0
    )

    private companion object {
        val UUID_PATTERN = Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$")
    }
}
