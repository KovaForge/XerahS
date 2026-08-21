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

package com.getsharex.xerahs.mobile.core.data

import android.util.Base64
import com.getsharex.xerahs.mobile.core.domain.AMAZON_S3_DESTINATION_ID
import com.getsharex.xerahs.mobile.core.domain.S3Config
import com.google.gson.Gson
import com.google.gson.annotations.SerializedName
import java.nio.charset.StandardCharsets
import javax.crypto.Cipher
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec

class DestinationConfigImportException(message: String, cause: Throwable? = null) : Exception(message, cause)

class DestinationConfigImporter(
    private val settingsRepository: SettingsRepository,
    private val gson: Gson = Gson()
) {
    fun import(data: ByteArray, passphrase: String): String {
        val envelope = try {
            gson.fromJson(String(data, StandardCharsets.UTF_8), XsdcEnvelope::class.java)
        } catch (e: Exception) {
            throw DestinationConfigImportException("The .xsdc file is not valid JSON.", e)
        } ?: throw DestinationConfigImportException("The .xsdc file is empty.")

        if (envelope.format != "XerahS.DestinationConfig" || envelope.formatVersion != 1) {
            throw DestinationConfigImportException("The .xsdc file is not a XerahS destination config.")
        }

        val encryption = envelope.encryption
            ?: throw DestinationConfigImportException("The .xsdc file is missing encryption metadata.")
        if (encryption.method != "Passphrase" ||
            encryption.kdf != "PBKDF2-HMAC-SHA256" ||
            encryption.cipher != "AES-256-GCM" ||
            encryption.iterations <= 0
        ) {
            throw DestinationConfigImportException("This .xsdc encryption method is not supported.")
        }

        val plainText = try {
            decryptPayload(envelope.payload, encryption, passphrase)
        } catch (e: Exception) {
            throw DestinationConfigImportException("The passphrase is incorrect or the file is damaged.", e)
        }

        val payload = try {
            gson.fromJson(String(plainText, StandardCharsets.UTF_8), XsdcPayload::class.java)
        } catch (e: Exception) {
            throw DestinationConfigImportException("The decrypted .xsdc payload is invalid.", e)
        } ?: throw DestinationConfigImportException("The decrypted .xsdc payload is empty.")

        val destination = payload.destinations.firstOrNull {
            it.providerId.equals(AMAZON_S3_DESTINATION_ID, ignoreCase = true) &&
                it.config.authMode.equals("AccessKeys", ignoreCase = true)
        } ?: throw DestinationConfigImportException("No mobile-compatible destination was found in this .xsdc file.")

        val imported = destination.config
        settingsRepository.saveS3Config(
            S3Config(
                accessKeyId = imported.accessKeyId,
                secretAccessKey = imported.secretAccessKey,
                bucketName = imported.bucketName,
                region = imported.region,
                customEndpoint = imported.endpoint,
                usePathStyle = imported.usePathStyle,
                useCustomDomain = imported.useCustomDomain,
                customDomain = imported.customDomain,
                signedPayload = imported.signedPayload,
                setPublicAcl = imported.setPublicAcl
            )
        )

        if (destination.isDefault || settingsRepository.getDefaultDestinationInstanceId() == null) {
            settingsRepository.setDefaultDestinationInstanceId(AMAZON_S3_DESTINATION_ID)
        }

        return destination.displayName.ifBlank { "Amazon S3" }
    }

    private fun decryptPayload(payload: String, encryption: XsdcEncryption, passphrase: String): ByteArray {
        val salt = Base64.decode(encryption.salt, Base64.DEFAULT)
        val nonce = Base64.decode(encryption.nonce, Base64.DEFAULT)
        val tag = Base64.decode(encryption.tag, Base64.DEFAULT)
        val cipherText = Base64.decode(payload, Base64.DEFAULT)
        val cipherAndTag = cipherText + tag
        val keyFactory = SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
        val keySpec = PBEKeySpec(passphrase.toCharArray(), salt, encryption.iterations, 256)
        val key = SecretKeySpec(keyFactory.generateSecret(keySpec).encoded, "AES")
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, key, GCMParameterSpec(128, nonce))
        return cipher.doFinal(cipherAndTag)
    }
}

private data class XsdcEnvelope(
    @SerializedName("Format") val format: String = "",
    @SerializedName("FormatVersion") val formatVersion: Int = 0,
    @SerializedName("Encryption") val encryption: XsdcEncryption? = null,
    @SerializedName("Payload") val payload: String = ""
)

private data class XsdcEncryption(
    @SerializedName("Method") val method: String = "",
    @SerializedName("Kdf") val kdf: String = "",
    @SerializedName("Iterations") val iterations: Int = 0,
    @SerializedName("Salt") val salt: String = "",
    @SerializedName("Cipher") val cipher: String = "",
    @SerializedName("Nonce") val nonce: String = "",
    @SerializedName("Tag") val tag: String = ""
)

private data class XsdcPayload(
    @SerializedName("Destinations") val destinations: List<XsdcDestination> = emptyList()
)

private data class XsdcDestination(
    @SerializedName("ProviderId") val providerId: String = "",
    @SerializedName("DisplayName") val displayName: String = "",
    @SerializedName("IsDefault") val isDefault: Boolean = false,
    @SerializedName("Config") val config: XsdcS3Config = XsdcS3Config()
)

private data class XsdcS3Config(
    @SerializedName("AuthMode") val authMode: String = "",
    @SerializedName("AccessKeyId") val accessKeyId: String = "",
    @SerializedName("SecretAccessKey") val secretAccessKey: String = "",
    @SerializedName("BucketName") val bucketName: String = "",
    @SerializedName("Region") val region: String = "",
    @SerializedName("Endpoint") val endpoint: String = "",
    @SerializedName("UsePathStyle") val usePathStyle: Boolean = false,
    @SerializedName("UseCustomDomain") val useCustomDomain: Boolean = false,
    @SerializedName("CustomDomain") val customDomain: String = "",
    @SerializedName("SignedPayload") val signedPayload: Boolean = true,
    @SerializedName("SetPublicAcl") val setPublicAcl: Boolean = false
)
