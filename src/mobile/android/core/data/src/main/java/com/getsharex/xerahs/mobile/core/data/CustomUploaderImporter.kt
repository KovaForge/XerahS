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

import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import com.getsharex.xerahs.mobile.core.domain.SxcuDefinition
import com.google.gson.Gson
import java.nio.charset.StandardCharsets

class CustomUploaderImportException(message: String, cause: Throwable? = null) : Exception(message, cause)

data class CustomUploaderImportResult(
    val displayName: String,
    val updatedExisting: Boolean
)

class CustomUploaderImporter(
    private val settingsRepository: SettingsRepository,
    private val gson: Gson = Gson()
) {
    fun import(data: ByteArray, sourceLabel: String = ".sxcu"): CustomUploaderImportResult {
        val text = String(data, StandardCharsets.UTF_8)
        val definition = try {
            gson.fromJson(text, SxcuDefinition::class.java)
        } catch (e: Exception) {
            throw CustomUploaderImportException("The file $sourceLabel is not a valid .sxcu definition.", e)
        } ?: throw CustomUploaderImportException("The file $sourceLabel is empty.")

        if (definition.RequestURL.isBlank() && definition.Name.isBlank()) {
            throw CustomUploaderImportException("The file $sourceLabel is not a valid .sxcu definition.")
        }

        val imported = CustomUploaderEntry.from(definition)
        val list = settingsRepository.loadCustomUploaders().toMutableList()
        val existingIndex = list.indexOfFirst {
            it.requestUrl.equals(imported.requestUrl, ignoreCase = true) &&
                it.name.equals(imported.name, ignoreCase = true)
        }

        val updatedExisting = existingIndex >= 0
        if (updatedExisting) {
            imported.id = list[existingIndex].id
            list[existingIndex] = imported
        } else {
            list.add(imported)
        }

        settingsRepository.saveCustomUploaders(list)
        return CustomUploaderImportResult(
            displayName = imported.displayName,
            updatedExisting = updatedExisting
        )
    }
}
