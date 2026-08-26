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

import android.net.Uri
import android.util.Base64
import java.security.MessageDigest
import java.security.SecureRandom
import java.time.Instant

data class CloudOAuthAttempt(val authorizationUrl: String, val expiresAtEpochSeconds: Long)

class CloudOAuthManager(private val apiClient: CloudApiClient) {
    private val random = SecureRandom()
    @Volatile private var pending: PendingAttempt? = null

    @Synchronized
    fun begin(): CloudOAuthAttempt {
        val state = randomValue(32)
        val nonce = randomValue(32)
        val verifier = randomValue(64)
        val challenge = encode(MessageDigest.getInstance("SHA-256").digest(verifier.toByteArray(Charsets.US_ASCII)))
        val expiresAt = Instant.now().plusSeconds(600).epochSecond
        val url = Uri.parse("${CLOUD_AUTHORITY}auth/v1/oauth/authorize").buildUpon()
            .appendQueryParameter("client_id", CLOUD_CLIENT_ID)
            .appendQueryParameter("redirect_uri", CLOUD_REDIRECT_URI)
            .appendQueryParameter("response_type", "code")
            .appendQueryParameter("scope", "openid email profile")
            .appendQueryParameter("state", state)
            .appendQueryParameter("nonce", nonce)
            .appendQueryParameter("code_challenge", challenge)
            .appendQueryParameter("code_challenge_method", "S256")
            .build().toString()
        pending = PendingAttempt(state, nonce, verifier, expiresAt)
        return CloudOAuthAttempt(url, expiresAt)
    }

    suspend fun complete(callback: Uri): Boolean {
        val parsed = CloudOAuthCallbackParser.parse(callback.toString())
        val attempt = synchronized(this) {
            val current = pending
            pending = null
            current
        } ?: throw CloudSecurityException("The XerahS Cloud sign-in request is unknown or already used.")
        if (Instant.now().epochSecond >= attempt.expiresAt || parsed.state != attempt.state) {
            throw CloudSecurityException("The XerahS Cloud sign-in request expired or did not match.")
        }
        parsed.error?.let { throw CloudSecurityException("XerahS Cloud authorization was denied ($it).") }
        val code = parsed.code ?: throw CloudSecurityException("The XerahS Cloud OAuth callback did not contain a code.")
        apiClient.exchangeAuthorizationCode(code, attempt.verifier, attempt.nonce)
        return true
    }

    private fun randomValue(bytes: Int): String = ByteArray(bytes).also(random::nextBytes).let(::encode)
    private fun encode(bytes: ByteArray): String = Base64.encodeToString(bytes, Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING)

    private data class PendingAttempt(val state: String, val nonce: String, val verifier: String, val expiresAt: Long)
}
