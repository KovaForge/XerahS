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

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import com.getsharex.xerahs.mobile.core.common.Paths
import com.getsharex.xerahs.mobile.core.domain.ApplicationConfig
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import com.getsharex.xerahs.mobile.core.domain.S3Config
import com.google.gson.Gson
import java.io.File
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

private const val CONFIG_FILE_NAME = "ApplicationConfig.json"
private const val KEYSTORE_ALIAS = "xerahs_mobile_settings"
private const val KEYSTORE_PROVIDER = "AndroidKeyStore"
private const val KEYSTORE_MARKER_PREFIX = "__xerahs_keystore__:"
private const val AES_MODE = "AES/GCM/NoPadding"

/**
 * Load/save [ApplicationConfig] as JSON in settings folder. Thread-safe via synchronized on file.
 */
class SettingsRepository(
    private val gson: Gson = Gson()
) {
    private val configFile: File?
        get() = Paths.settingsFolder?.let { File(it, CONFIG_FILE_NAME) }

    fun load(): ApplicationConfig {
        val file = configFile ?: return ApplicationConfig()
        if (!file.exists()) return ApplicationConfig()
        return try {
            val decoded = file.readText().let { gson.fromJson(it, ApplicationConfig::class.java) } ?: ApplicationConfig()
            val secured = secureForStorage(decoded)
            if (secured.didChange) {
                writeConfig(secured.config, file)
            }
            hydrateSecrets(secured.config)
        } catch (e: Exception) {
            ApplicationConfig()
        }
    }

    fun save(config: ApplicationConfig) {
        val file = configFile ?: return
        writeConfig(secureForStorage(config).config, file)
    }

    fun loadS3Config(): S3Config = load().s3Config
    fun saveS3Config(config: S3Config) {
        val c = load()
        save(c.copy(s3Config = config))
    }

    fun loadCustomUploaders(): List<CustomUploaderEntry> = load().customUploaders
    fun saveCustomUploaders(list: List<CustomUploaderEntry>) {
        val c = load()
        save(c.copy(customUploaders = list))
    }

    fun getDefaultDestinationInstanceId(): String? = load().defaultDestinationInstanceId
    fun setDefaultDestinationInstanceId(id: String?) {
        val c = load()
        save(c.copy(defaultDestinationInstanceId = id))
    }

    fun getConvertHeicToPng(): Boolean = load().convertHeicToPng
    fun setConvertHeicToPng(value: Boolean) {
        val c = load()
        save(c.copy(convertHeicToPng = value))
    }

    private fun writeConfig(config: ApplicationConfig, file: File) {
        Paths.settingsFolder?.mkdirs()
        file.writeText(gson.toJson(config))
    }

    private fun secureForStorage(config: ApplicationConfig): SecuredConfig {
        var didChange = false
        val securedS3 = config.s3Config.copy(
            secretAccessKey = secureValue(config.s3Config.secretAccessKey).also {
                didChange = didChange || it.didChange
            }.value
        )

        val securedUploaders = config.customUploaders.mapIndexed { index, entry ->
            val uploaderId = entry.id.ifBlank { "custom_$index" }
            val securedParameters = secureDictionary(entry.parameters, uploaderId, "parameters").also {
                didChange = didChange || it.didChange
            }.value
            val securedHeaders = secureDictionary(entry.headers, uploaderId, "headers").also {
                didChange = didChange || it.didChange
            }.value
            val securedArguments = secureDictionary(entry.arguments, uploaderId, "arguments").also {
                didChange = didChange || it.didChange
            }.value

            entry.copy(
                parameters = securedParameters,
                headers = securedHeaders,
                arguments = securedArguments
            )
        }

        return SecuredConfig(
            config = config.copy(s3Config = securedS3, customUploaders = securedUploaders),
            didChange = didChange
        )
    }

    private fun hydrateSecrets(config: ApplicationConfig): ApplicationConfig {
        val hydratedS3 = config.s3Config.copy(
            secretAccessKey = hydrateValue(config.s3Config.secretAccessKey)
        )
        val hydratedUploaders = config.customUploaders.map { entry ->
            entry.copy(
                parameters = hydrateDictionary(entry.parameters),
                headers = hydrateDictionary(entry.headers),
                arguments = hydrateDictionary(entry.arguments)
            )
        }
        return config.copy(s3Config = hydratedS3, customUploaders = hydratedUploaders)
    }

    private fun secureDictionary(
        dictionary: Map<String, String>,
        uploaderId: String,
        section: String
    ): SecuredMap {
        var didChange = false
        val secured = dictionary.mapValues { (key, value) ->
            if (!isSensitiveCustomUploaderKey(key)) {
                value
            } else {
                secureValue(value, "customUploader.$uploaderId.$section.$key").also {
                    didChange = didChange || it.didChange
                }.value
            }
        }
        return SecuredMap(secured, didChange)
    }

    private fun hydrateDictionary(dictionary: Map<String, String>): Map<String, String> =
        dictionary.mapValues { (_, value) -> hydrateValue(value) }

    private fun secureValue(value: String, label: String = "secret"): SecuredValue {
        val trimmed = value.trim()
        if (trimmed.isEmpty() || value.startsWith(KEYSTORE_MARKER_PREFIX)) {
            return SecuredValue(value, didChange = false)
        }

        return try {
            val marker = KEYSTORE_MARKER_PREFIX + encryptString(value, label)
            SecuredValue(marker, didChange = true)
        } catch (e: Exception) {
            SecuredValue(value, didChange = false)
        }
    }

    private fun hydrateValue(value: String): String {
        if (!value.startsWith(KEYSTORE_MARKER_PREFIX)) return value
        return try {
            decryptString(value.removePrefix(KEYSTORE_MARKER_PREFIX))
        } catch (e: Exception) {
            value
        }
    }

    private fun encryptString(value: String, label: String): String {
        val cipher = Cipher.getInstance(AES_MODE)
        cipher.init(Cipher.ENCRYPT_MODE, getOrCreateSecretKey())
        val cipherText = cipher.doFinal(value.toByteArray(Charsets.UTF_8))
        val labelBytes = label.toByteArray(Charsets.UTF_8)
        val payload = byteArrayOf(cipher.iv.size.toByte()) +
            cipher.iv +
            byteArrayOf(labelBytes.size.coerceAtMost(255).toByte()) +
            labelBytes.take(255).map { it }.toByteArray() +
            cipherText
        return Base64.encodeToString(payload, Base64.NO_WRAP)
    }

    private fun decryptString(encoded: String): String {
        val payload = Base64.decode(encoded, Base64.NO_WRAP)
        val ivLength = payload[0].toInt() and 0xff
        val ivStart = 1
        val ivEnd = ivStart + ivLength
        val labelLength = payload[ivEnd].toInt() and 0xff
        val cipherStart = ivEnd + 1 + labelLength
        val iv = payload.copyOfRange(ivStart, ivEnd)
        val cipherText = payload.copyOfRange(cipherStart, payload.size)
        val cipher = Cipher.getInstance(AES_MODE)
        cipher.init(Cipher.DECRYPT_MODE, getOrCreateSecretKey(), GCMParameterSpec(128, iv))
        return String(cipher.doFinal(cipherText), Charsets.UTF_8)
    }

    private fun getOrCreateSecretKey(): SecretKey {
        val keyStore = KeyStore.getInstance(KEYSTORE_PROVIDER).apply { load(null) }
        (keyStore.getEntry(KEYSTORE_ALIAS, null) as? KeyStore.SecretKeyEntry)?.let {
            return it.secretKey
        }

        val keyGenerator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE_PROVIDER)
        keyGenerator.init(
            KeyGenParameterSpec.Builder(
                KEYSTORE_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
            )
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build()
        )
        return keyGenerator.generateKey()
    }

    private fun isSensitiveCustomUploaderKey(key: String): Boolean {
        val normalized = key.lowercase()
            .replace("-", "")
            .replace("_", "")
            .replace(" ", "")

        if (normalized == "authorization" || normalized == "auth") {
            return true
        }

        return listOf(
            "apikey",
            "accesstoken",
            "bearer",
            "clientsecret",
            "password",
            "passwd",
            "secret",
            "signature",
            "token"
        ).any { normalized.contains(it) }
    }
}

private data class SecuredConfig(val config: ApplicationConfig, val didChange: Boolean)
private data class SecuredMap(val value: Map<String, String>, val didChange: Boolean)
private data class SecuredValue(val value: String, val didChange: Boolean)
