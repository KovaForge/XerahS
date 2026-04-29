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

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderBodyType
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderRequestMethod
import androidx.lifecycle.viewmodel.compose.viewModel

@Composable
fun CustomUploaderConfigScreen(
    settingsRepository: SettingsRepository?,
    onBack: () -> Unit
) {
    if (settingsRepository == null) {
        Button(onClick = onBack) { Text("Back") }
        return
    }
    val viewModel: CustomUploaderConfigViewModel = viewModel(
        factory = object : androidx.lifecycle.ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T =
                CustomUploaderConfigViewModel(settingsRepository) as T
        }
    )
    val uploaders by viewModel.uploaders.collectAsState()
    val editing by viewModel.editingEntry.collectAsState()
    val statusMessage by viewModel.statusMessage.collectAsState()
    val isStatusError by viewModel.isStatusError.collectAsState()
    val importErrorDetails by viewModel.importErrorDetails.collectAsState()
    val context = LocalContext.current
    val clipboardManager = context.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
    fun copyToClipboard(label: String, text: String) {
        clipboardManager?.setPrimaryClip(ClipData.newPlainText(label, text))
    }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Button(onClick = onBack) { Text("Back") }
            Button(
                onClick = {
                    viewModel.importFromClipboardText(clipboardManager?.primaryClip?.getItemAt(0)?.coerceToText(context)?.toString())
                }
            ) {
                Text("Import Clipboard")
            }
        }
        Spacer(modifier = Modifier.height(16.dp))
        Text(
            text = "Custom Uploader",
            style = MaterialTheme.typography.titleLarge
        )
        Text(
            text = "Import and edit .sxcu definitions using the same request, body, and response shape as desktop.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        Spacer(modifier = Modifier.height(8.dp))
        if (!statusMessage.isNullOrBlank()) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Text(
                        text = statusMessage.orEmpty(),
                        color = if (isStatusError) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.onSurface,
                        style = MaterialTheme.typography.bodySmall
                    )
                    if (isStatusError && !importErrorDetails.isNullOrBlank()) {
                        OutlinedButton(
                            onClick = {
                                copyToClipboard("sxcu import error", importErrorDetails.orEmpty())
                                viewModel.markImportErrorCopied()
                            }
                        ) {
                            Text("Copy Error")
                        }
                    }
                }
            }
            Spacer(modifier = Modifier.height(8.dp))
        }
        LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            items(uploaders, key = { it.id }) { entry ->
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors()
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text(
                                text = entry.displayName,
                                style = MaterialTheme.typography.titleSmall
                            )
                            Text(
                                text = entry.requestUrl.ifBlank { "No URL" },
                                style = MaterialTheme.typography.bodySmall
                            )
                            Text(
                                text = "${entry.requestMethod.name} • ${entry.bodyType.name} • ${entry.destinationType}",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                        OutlinedButton(onClick = { viewModel.edit(entry) }) { Text("Edit") }
                        Spacer(modifier = Modifier.padding(4.dp))
                        OutlinedButton(
                            onClick = {
                                copyToClipboard("sxcu", viewModel.sxcuJson(entry))
                                viewModel.markExported(entry)
                            }
                        ) { Text("Copy .sxcu") }
                        Spacer(modifier = Modifier.padding(4.dp))
                        OutlinedButton(onClick = { viewModel.delete(entry) }) { Text("Delete") }
                    }
                }
            }
        }
        Spacer(modifier = Modifier.height(8.dp))
        FloatingActionButton(
            onClick = { viewModel.addNew() }
        ) {
            Text("Add")
        }
    }

    editing?.let { entry ->
        CustomUploaderEditDialog(
            entry = entry,
            onDismiss = { viewModel.cancelEdit() },
            onSave = { updated -> viewModel.saveEdit(updated) }
        )
    }
}

@Composable
private fun CustomUploaderEditDialog(
    entry: CustomUploaderEntry,
    onDismiss: () -> Unit,
    onSave: (CustomUploaderEntry) -> Unit
) {
    var name by remember { mutableStateOf(entry.name) }
    var destinationType by remember { mutableStateOf(entry.destinationType) }
    var requestMethod by remember { mutableStateOf(entry.requestMethod.name) }
    var requestUrl by remember { mutableStateOf(entry.requestUrl) }
    var bodyType by remember { mutableStateOf(entry.bodyType.name) }
    var fileFormName by remember { mutableStateOf(entry.fileFormName) }
    var parametersText by remember { mutableStateOf(KeyValueCodec.encode(entry.parameters)) }
    var headersText by remember { mutableStateOf(KeyValueCodec.encode(entry.headers)) }
    var argumentsText by remember { mutableStateOf(KeyValueCodec.encode(entry.arguments)) }
    var dataText by remember { mutableStateOf(entry.data) }
    var urlText by remember { mutableStateOf(entry.url.ifBlank { entry.urlExpression }) }
    var deletionUrlText by remember { mutableStateOf(entry.deletionUrl) }
    var errorMessageText by remember { mutableStateOf(entry.errorMessage) }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (entry.id.isNotEmpty()) "Edit .sxcu" else "New .sxcu") },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState())
            ) {
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Name") },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = destinationType,
                    onValueChange = { destinationType = it },
                    label = { Text("Destination Type") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = requestMethod,
                    onValueChange = { requestMethod = it.uppercase() },
                    label = { Text("Request Method") },
                    supportingText = { Text("GET, POST, PUT, PATCH, DELETE, or HEAD") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = requestUrl,
                    onValueChange = { requestUrl = it },
                    label = { Text("Request URL") },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = bodyType,
                    onValueChange = { bodyType = it },
                    label = { Text("Body Type") },
                    supportingText = { Text("None, MultipartFormData, FormURLEncoded, JSON, XML, or Binary") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = fileFormName,
                    onValueChange = { fileFormName = it },
                    label = { Text("File form name") },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = parametersText,
                    onValueChange = { parametersText = it },
                    label = { Text("Parameters") },
                    supportingText = { Text("key=value, one per line") },
                    minLines = 3,
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = headersText,
                    onValueChange = { headersText = it },
                    label = { Text("Headers") },
                    supportingText = { Text("Header=Value, one per line") },
                    minLines = 3,
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = argumentsText,
                    onValueChange = { argumentsText = it },
                    label = { Text("Arguments") },
                    supportingText = { Text("field=value, one per line") },
                    minLines = 3,
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = dataText,
                    onValueChange = { dataText = it },
                    label = { Text("Body Data") },
                    minLines = 4,
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = urlText,
                    onValueChange = { urlText = it },
                    label = { Text("URL template") },
                    minLines = 2,
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = deletionUrlText,
                    onValueChange = { deletionUrlText = it },
                    label = { Text("Deletion URL template") },
                    minLines = 2,
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(modifier = Modifier.height(8.dp))
                OutlinedTextField(
                    value = errorMessageText,
                    onValueChange = { errorMessageText = it },
                    label = { Text("Error message template") },
                    minLines = 2,
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            Button(
                onClick = {
                    onSave(
                        entry.copy(
                            name = name.trim(),
                            destinationType = destinationType.trim(),
                            requestMethod = CustomUploaderRequestMethod.entries.firstOrNull {
                                it.name.equals(requestMethod.trim(), ignoreCase = true)
                            } ?: entry.requestMethod,
                            requestUrl = requestUrl.trim(),
                            bodyType = CustomUploaderBodyType.entries.firstOrNull {
                                it.name.equals(bodyType.trim(), ignoreCase = true)
                            } ?: entry.bodyType,
                            fileFormName = fileFormName.ifBlank { "file" },
                            parameters = KeyValueCodec.decode(parametersText),
                            headers = KeyValueCodec.decode(headersText),
                            arguments = KeyValueCodec.decode(argumentsText),
                            data = dataText,
                            url = urlText.trim(),
                            deletionUrl = deletionUrlText.trim(),
                            errorMessage = errorMessageText.trim(),
                            body = ""
                        )
                    )
                }
            ) {
                Text("Save")
            }
        },
        dismissButton = {
            OutlinedButton(onClick = onDismiss) { Text("Cancel") }
        }
    )
}
