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

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

private const val STORE_NAME = "xerahs_cloud_credentials"
private const val KEY_ALIAS = "xerahs_cloud_session_v1"
private const val KEY_REFRESH = "refresh_token"
private const val KEY_SUBJECT = "owner_subject"
private const val AES_MODE = "AES/GCM/NoPadding"

data class CloudRefreshCredential(val ownerSubject: String, val refreshToken: String)

/** AndroidKeyStore-only credential storage. Encryption failures never fall back to plaintext. */
class CloudCredentialStore(context: Context) {
    private val preferences = context.getSharedPreferences(STORE_NAME, Context.MODE_PRIVATE)

    @Synchronized
    fun write(credential: CloudRefreshCredential) {
        require(credential.ownerSubject.isNotBlank())
        require(credential.refreshToken.isNotBlank())
        val subject = encrypt(credential.ownerSubject)
        val refresh = encrypt(credential.refreshToken)
        check(preferences.edit().putString(KEY_SUBJECT, subject).putString(KEY_REFRESH, refresh).commit()) {
            "XerahS Cloud credentials could not be stored securely."
        }
    }

    @Synchronized
    fun read(): CloudRefreshCredential? {
        val subjectValue = preferences.getString(KEY_SUBJECT, null)
        val refreshValue = preferences.getString(KEY_REFRESH, null)
        if (subjectValue == null && refreshValue == null) return null
        if (subjectValue == null || refreshValue == null) {
            clear()
            throw CloudSecurityException("XerahS Cloud credentials were incomplete and have been cleared.")
        }
        return try {
            CloudRefreshCredential(decrypt(subjectValue), decrypt(refreshValue)).also {
                if (it.ownerSubject.isBlank() || it.refreshToken.isBlank()) {
                    throw CloudSecurityException("XerahS Cloud credentials were empty.")
                }
            }
        } catch (error: Exception) {
            clear()
            if (error is CloudSecurityException) throw error
            throw CloudSecurityException("XerahS Cloud credentials could not be decrypted and have been cleared.", error)
        }
    }

    @Synchronized
    fun clear() {
        check(preferences.edit().clear().commit()) { "XerahS Cloud credentials could not be cleared." }
    }

    private fun encrypt(value: String): String {
        try {
            val cipher = Cipher.getInstance(AES_MODE)
            cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
            val encrypted = cipher.doFinal(value.toByteArray(Charsets.UTF_8))
            return Base64.encodeToString(cipher.iv + encrypted, Base64.NO_WRAP)
        } catch (error: Exception) {
            throw CloudSecurityException("AndroidKeyStore could not protect XerahS Cloud credentials.", error)
        }
    }

    private fun decrypt(value: String): String {
        val payload = Base64.decode(value, Base64.NO_WRAP)
        if (payload.size <= 12) throw CloudSecurityException("Stored XerahS Cloud credentials are invalid.")
        val iv = payload.copyOfRange(0, 12)
        val encrypted = payload.copyOfRange(12, payload.size)
        val cipher = Cipher.getInstance(AES_MODE)
        cipher.init(Cipher.DECRYPT_MODE, getOrCreateKey(), GCMParameterSpec(128, iv))
        return String(cipher.doFinal(encrypted), Charsets.UTF_8)
    }

    private fun getOrCreateKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getEntry(KEY_ALIAS, null) as? KeyStore.SecretKeyEntry)?.let { return it.secretKey }
        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(
                KEY_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build()
        )
        return generator.generateKey()
    }
}
