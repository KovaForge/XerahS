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

package com.getsharex.xerahs.mobile.feature.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderBodyType
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderRequestMethod
import com.getsharex.xerahs.mobile.core.domain.SxcuDefinition
import com.google.gson.Gson
import com.google.gson.GsonBuilder
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.io.File
import java.util.UUID

class CustomUploaderConfigViewModel(
    private val settingsRepository: SettingsRepository,
    private val gson: Gson = GsonBuilder().setPrettyPrinting().create()
) : ViewModel() {

    private val _uploaders = MutableStateFlow<List<CustomUploaderEntry>>(emptyList())
    val uploaders: StateFlow<List<CustomUploaderEntry>> = _uploaders.asStateFlow()

    private val _editingEntry = MutableStateFlow<CustomUploaderEntry?>(null)
    val editingEntry: StateFlow<CustomUploaderEntry?> = _editingEntry.asStateFlow()

    private val _statusMessage = MutableStateFlow<String?>(null)
    val statusMessage: StateFlow<String?> = _statusMessage.asStateFlow()

    private val _isStatusError = MutableStateFlow(false)
    val isStatusError: StateFlow<Boolean> = _isStatusError.asStateFlow()

    private val _importErrorDetails = MutableStateFlow<String?>(null)
    val importErrorDetails: StateFlow<String?> = _importErrorDetails.asStateFlow()

    init {
        viewModelScope.launch {
            _uploaders.value = settingsRepository.loadCustomUploaders()
        }
    }

    fun refresh() {
        viewModelScope.launch {
            _uploaders.value = settingsRepository.loadCustomUploaders()
        }
    }

    fun addNew() {
        _editingEntry.value = CustomUploaderEntry(
            id = "custom_${UUID.randomUUID().toString().take(8)}",
            name = "New Uploader",
            destinationType = "FileUploader",
            requestMethod = CustomUploaderRequestMethod.POST,
            requestUrl = "",
            bodyType = CustomUploaderBodyType.MultipartFormData,
            fileFormName = "file"
        )
    }

    fun edit(entry: CustomUploaderEntry) {
        _editingEntry.value = entry.copy()
    }

    fun saveEdit(entry: CustomUploaderEntry) {
        if (!isSecureRequestUrl(entry.requestUrl)) {
            setStatus(
                "HTTP upload endpoints are not supported because uploads may contain private user files or credentials. Use HTTPS.",
                isError = true
            )
            return
        }
        val list = _uploaders.value.toMutableList()
        val index = list.indexOfFirst { it.id == entry.id }
        if (index >= 0) {
            list[index] = entry
        } else {
            list.add(entry)
        }
        settingsRepository.saveCustomUploaders(list)
        _uploaders.value = list
        _editingEntry.value = null
        setStatus("Saved ${entry.displayName}.")
    }

    fun cancelEdit() {
        _editingEntry.value = null
    }

    fun delete(entry: CustomUploaderEntry) {
        val list = _uploaders.value.filter { it.id != entry.id }
        settingsRepository.saveCustomUploaders(list)
        _uploaders.value = list
        setStatus("Deleted ${entry.displayName}.")
    }

    fun importFromClipboardText(text: String?) {
        val payload = clipboardPayload(text)
        if (payload == null) {
            setStatus(
                "Clipboard does not contain .sxcu JSON or a readable .sxcu file path.",
                isError = true,
                details = clipboardDiagnostics(null, text, null)
            )
            return
        }

        try {
            val definition = gson.fromJson(payload, SxcuDefinition::class.java)
            val imported = CustomUploaderEntry.from(definition)
            if (!isSecureRequestUrl(imported.requestUrl)) {
                setStatus(
                    "HTTP upload endpoints are not supported because uploads may contain private user files or credentials. Use HTTPS.",
                    isError = true,
                    details = clipboardDiagnostics(payload, text, null)
                )
                return
            }
            val list = _uploaders.value.toMutableList()
            val existingIndex = list.indexOfFirst {
                it.requestUrl.equals(imported.requestUrl, ignoreCase = true) &&
                    it.name.equals(imported.name, ignoreCase = true)
            }
            if (existingIndex >= 0) {
                imported.id = list[existingIndex].id
                list[existingIndex] = imported
                setStatus("Updated custom uploader: ${imported.displayName}")
            } else {
                list.add(imported)
                setStatus("Imported custom uploader: ${imported.displayName}")
            }
            settingsRepository.saveCustomUploaders(list)
            if (settingsRepository.getDefaultDestinationInstanceId() == null) {
                settingsRepository.setDefaultDestinationInstanceId(imported.id)
            }
            _uploaders.value = list
        } catch (e: Exception) {
            setStatus(
                "Failed to import .sxcu from clipboard: ${e.message ?: e::class.java.simpleName}",
                isError = true,
                details = clipboardDiagnostics(payload, text, e)
            )
        }
    }

    fun sxcuJson(entry: CustomUploaderEntry): String = gson.toJson(entry.toSxcuDefinition())

    fun importErrorDetails(): String? = _importErrorDetails.value

    fun markExported(entry: CustomUploaderEntry) {
        setStatus("Copied ${entry.displayName} as .sxcu JSON.")
    }

    fun markImportErrorCopied() {
        setStatus("Copied import error details to clipboard.")
    }

    private fun clipboardPayload(text: String?): String? {
        val cleaned = stripMarkdownCodeFence(text?.trim().orEmpty())
        if (cleaned.isBlank()) return null
        if (cleaned.startsWith("file://")) {
            val path = cleaned.removePrefix("file://")
            return File(path).takeIf { it.exists() }?.readText()
        }
        if (cleaned.endsWith(".sxcu") && File(cleaned).exists()) {
            return File(cleaned).readText()
        }
        return cleaned
    }

    private fun stripMarkdownCodeFence(input: String): String {
        if (!input.startsWith("```")) return input
        val lines = input.lines()
        if (lines.size < 3) return input
        return lines.drop(1).dropLast(1).joinToString("\n")
    }

    private fun clipboardDiagnostics(payload: String?, cleanedString: String?, error: Exception?): String {
        val lines = mutableListOf(
            "SXCU clipboard import failed",
            "Clipboard has string: ${!cleanedString.isNullOrBlank()}",
            "Payload chars: ${payload?.length ?: 0}"
        )
        if (error != null) {
            lines.add("Error: ${error::class.java.name}: ${error.message.orEmpty()}")
        }
        val preview = payload ?: cleanedString
        if (!preview.isNullOrBlank()) {
            lines.add("Clipboard preview:")
            lines.add(redactSensitiveText(preview).take(400))
        }
        return lines.joinToString("\n")
    }

    private fun isSecureRequestUrl(requestUrl: String): Boolean =
        requestUrl.trim().startsWith("https://", ignoreCase = true)

    private fun redactSensitiveText(value: String): String =
        value
            .replace(Regex("""(?i)("(?:Authorization|Cookie|X-Api-Key|api_key|token|secret|password|access_token)"\s*:\s*")([^"]*)(")""")) {
                "${it.groupValues[1]}<redacted>${it.groupValues[3]}"
            }
            .replace(Regex("""(?i)((?:Authorization|Cookie|X-Api-Key|api_key|token|secret|password|access_token)\s*[=:]\s*)([^\n\r&]+)""")) {
                "${it.groupValues[1]}<redacted>"
            }

    private fun setStatus(message: String, isError: Boolean = false, details: String? = null) {
        _statusMessage.value = message
        _isStatusError.value = isError
        _importErrorDetails.value = if (isError) details else null
    }
}

object KeyValueCodec {
    fun encode(value: Map<String, String>): String =
        value.keys.sorted().joinToString("\n") { key -> "$key=${value[key].orEmpty()}" }

    fun decode(text: String): Map<String, String> {
        val result = linkedMapOf<String, String>()
        text.lines().forEach { raw ->
            val line = raw.trim()
            if (line.isBlank()) return@forEach
            val separator = listOf(line.indexOf('='), line.indexOf(':')).filter { it >= 0 }.minOrNull() ?: return@forEach
            val key = line.substring(0, separator).trim()
            val value = line.substring(separator + 1).trim()
            if (key.isNotBlank()) result[key] = value
        }
        return result
    }
}
