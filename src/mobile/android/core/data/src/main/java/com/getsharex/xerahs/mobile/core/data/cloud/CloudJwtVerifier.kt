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

import android.util.Base64
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import okhttp3.OkHttpClient
import okhttp3.Request
import java.math.BigInteger
import java.security.AlgorithmParameters
import java.security.KeyFactory
import java.security.MessageDigest
import java.security.Signature
import java.security.spec.ECGenParameterSpec
import java.security.spec.ECParameterSpec
import java.security.spec.ECPoint
import java.security.spec.ECPublicKeySpec
import java.security.spec.RSAPublicKeySpec
import java.time.Instant

internal data class VerifiedToken(val subject: String, val expiresAtEpochSeconds: Long)

internal class CloudJwtVerifier(private val httpClient: OkHttpClient) {
    @Volatile private var cachedKeys: List<JsonObject> = emptyList()
    @Volatile private var keysExpireAt: Long = 0

    fun verifyAccess(token: String): VerifiedToken {
        val claims = verify(token)
        validateCommon(claims, CLOUD_ISSUER, "authenticated")
        val expiresAt = requiredLong(claims, "exp")
        if (!CloudTokenPolicy.hasAcceptedMaximumExpiry(expiresAt, Instant.now().epochSecond)) {
            throw CloudSecurityException("OAuth access token exceeds the accepted one-hour lifetime.")
        }
        val subject = requiredString(claims, "sub")
        if (requiredString(claims, "client_id") != CLOUD_CLIENT_ID) {
            throw CloudSecurityException("OAuth access token was not issued to this XerahS client.")
        }
        if (requiredString(claims, "aal") != "aal2") {
            throw CloudSecurityException("XerahS Cloud requires multi-factor authentication (AAL2).")
        }
        if (requiredString(claims, "session_id").isBlank()) {
            throw CloudSecurityException("OAuth access token is missing a session identifier.")
        }
        return VerifiedToken(subject, expiresAt)
    }

    fun verifyId(token: String, expectedSubject: String, expectedNonce: String?) {
        val claims = verify(token)
        validateCommon(claims, CLOUD_ISSUER, CLOUD_CLIENT_ID)
        if (requiredString(claims, "sub") != expectedSubject) {
            throw CloudSecurityException("OpenID Connect subject validation failed.")
        }
        if (expectedNonce != null && !constantTimeEquals(requiredString(claims, "nonce"), expectedNonce)) {
            throw CloudSecurityException("OpenID Connect nonce validation failed.")
        }
    }

    private fun verify(token: String): JsonObject {
        if (token.length > 32768) throw CloudSecurityException("OAuth server returned an oversized JWT.")
        val parts = token.split('.')
        if (parts.size != 3 || parts.any { it.isBlank() }) throw CloudSecurityException("OAuth server returned a malformed JWT.")
        val header = parseObject(parts[0])
        val claims = parseObject(parts[1])
        val algorithm = requiredString(header, "alg")
        val keyId = requiredString(header, "kid")
        if (algorithm != "RS256" && algorithm != "ES256") throw CloudSecurityException("OAuth JWT uses an unsupported signing algorithm.")
        val key = findKey(keyId, algorithm)
        val data = "${parts[0]}.${parts[1]}".toByteArray(Charsets.US_ASCII)
        val signature = decode(parts[2])
        val valid = when (algorithm) {
            "RS256" -> verifyRsa(key, data, signature)
            else -> verifyEc(key, data, signature)
        }
        if (!valid) throw CloudSecurityException("OAuth JWT signature validation failed.")
        return claims
    }

    @Synchronized
    private fun findKey(keyId: String, algorithm: String): JsonObject {
        var keys = cachedKeys
        if (keysExpireAt <= Instant.now().epochSecond || keys.none { matches(it, keyId, algorithm) }) {
            val request = Request.Builder().url(CLOUD_JWKS_URL).get().build()
            httpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) throw CloudSecurityException("OAuth JWKS request failed with HTTP ${response.code}.")
                val root = JsonParser.parseString(response.body?.string().orEmpty()).asJsonObject
                val values = root.getAsJsonArray("keys") ?: throw CloudSecurityException("OAuth JWKS response is invalid.")
                keys = values.map { it.asJsonObject }
                cachedKeys = keys
                keysExpireAt = Instant.now().plusSeconds(900).epochSecond
            }
        }
        return keys.firstOrNull { matches(it, keyId, algorithm) }
            ?: throw CloudSecurityException("OAuth JWT signing key was not found in the project JWKS.")
    }

    private fun matches(key: JsonObject, keyId: String, algorithm: String): Boolean =
        key.get("kid")?.asString == keyId && (key.get("alg") == null || key.get("alg").asString == algorithm)

    private fun verifyRsa(key: JsonObject, data: ByteArray, signatureBytes: ByteArray): Boolean {
        if (requiredString(key, "kty") != "RSA") return false
        val publicKey = KeyFactory.getInstance("RSA").generatePublic(
            RSAPublicKeySpec(BigInteger(1, decode(requiredString(key, "n"))), BigInteger(1, decode(requiredString(key, "e"))))
        )
        return Signature.getInstance("SHA256withRSA").run { initVerify(publicKey); update(data); verify(signatureBytes) }
    }

    private fun verifyEc(key: JsonObject, data: ByteArray, signatureBytes: ByteArray): Boolean {
        if (requiredString(key, "kty") != "EC" || requiredString(key, "crv") != "P-256" || signatureBytes.size != 64) return false
        val parameters = AlgorithmParameters.getInstance("EC").apply { init(ECGenParameterSpec("secp256r1")) }
            .getParameterSpec(ECParameterSpec::class.java)
        val publicKey = KeyFactory.getInstance("EC").generatePublic(
            ECPublicKeySpec(
                ECPoint(BigInteger(1, decode(requiredString(key, "x"))), BigInteger(1, decode(requiredString(key, "y")))),
                parameters
            )
        )
        return Signature.getInstance("SHA256withECDSA").run {
            initVerify(publicKey)
            update(data)
            verify(joseToDer(signatureBytes))
        }
    }

    private fun validateCommon(claims: JsonObject, issuer: String, audience: String) {
        if (requiredString(claims, "iss").trimEnd('/') != issuer || !hasAudience(claims, audience)) {
            throw CloudSecurityException("OAuth JWT issuer or audience validation failed.")
        }
        val now = Instant.now().epochSecond
        if (requiredLong(claims, "exp") <= now - 60) throw CloudSecurityException("OAuth JWT has expired.")
        claims.get("nbf")?.takeIf { it.isJsonPrimitive && it.asJsonPrimitive.isNumber }?.asLong?.let {
            if (it > now + 60) throw CloudSecurityException("OAuth JWT is not valid yet.")
        }
    }

    private fun hasAudience(claims: JsonObject, expected: String): Boolean {
        val value = claims.get("aud") ?: return false
        return when {
            value.isJsonPrimitive -> value.asString == expected
            value.isJsonArray -> value.asJsonArray.any { it.isJsonPrimitive && it.asString == expected }
            else -> false
        }
    }

    private fun parseObject(value: String): JsonObject = try {
        JsonParser.parseString(String(decode(value), Charsets.UTF_8)).asJsonObject
    } catch (error: Exception) {
        throw CloudSecurityException("OAuth JWT contains invalid encoding.", error)
    }

    private fun requiredString(value: JsonObject, name: String): String =
        value.get(name)?.takeIf { it.isJsonPrimitive && it.asJsonPrimitive.isString }?.asString?.takeIf { it.isNotBlank() }
            ?: throw CloudSecurityException("OAuth JWT is missing the required '$name' claim.")

    private fun requiredLong(value: JsonObject, name: String): Long = try {
        value.get(name)?.asLong ?: throw IllegalArgumentException()
    } catch (_: Exception) {
        throw CloudSecurityException("OAuth JWT is missing the required '$name' claim.")
    }

    private fun decode(value: String): ByteArray = try {
        Base64.decode(value, Base64.URL_SAFE or Base64.NO_WRAP or Base64.NO_PADDING)
    } catch (error: Exception) {
        throw CloudSecurityException("OAuth value contains invalid base64url encoding.", error)
    }

    private fun joseToDer(raw: ByteArray): ByteArray {
        fun integer(bytes: ByteArray): ByteArray {
            val withoutLeadingZeros = bytes.dropWhile { it == 0.toByte() }.toByteArray()
            val stripped = if (withoutLeadingZeros.isEmpty()) byteArrayOf(0) else withoutLeadingZeros
            return if ((stripped[0].toInt() and 0x80) != 0) byteArrayOf(0) + stripped else stripped
        }
        val r = integer(raw.copyOfRange(0, 32))
        val s = integer(raw.copyOfRange(32, 64))
        val length = 2 + r.size + 2 + s.size
        return byteArrayOf(0x30, length.toByte(), 0x02, r.size.toByte()) + r + byteArrayOf(0x02, s.size.toByte()) + s
    }

    private fun constantTimeEquals(left: String, right: String): Boolean =
        MessageDigest.isEqual(left.toByteArray(Charsets.UTF_8), right.toByteArray(Charsets.UTF_8))
}
