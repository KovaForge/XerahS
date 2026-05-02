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

package com.getsharex.xerahs.mobile.feature.upload

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.getsharex.xerahs.mobile.core.data.HistoryRepository
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.data.UploadQueueWorker
import com.getsharex.xerahs.mobile.core.domain.HistoryEntry
import com.getsharex.xerahs.mobile.core.domain.UploadResultItem
import com.getsharex.xerahs.mobile.core.domain.activeDestinationDisplayName
import java.io.File

private const val MAX_HOME_HISTORY_ITEMS = 100

@Composable
fun UploadScreen(
    worker: UploadQueueWorker,
    onPickFiles: (() -> Unit)? = null,
    onCopyToClipboard: (String) -> Unit = {},
    onAutoShareUploadFinished: (List<UploadResultItem>) -> Unit = {},
    initialPaths: Array<String>? = null,
    historyRepository: HistoryRepository? = null,
    settingsRepository: SettingsRepository? = null,
    viewModel: UploadViewModel = androidx.lifecycle.viewmodel.compose.viewModel(
        factory = object : androidx.lifecycle.ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T =
                UploadViewModel(worker) as T
        }
    )
) {
    var pendingAutoShareUploads by remember { mutableIntStateOf(0) }
    var autoShareResults by remember { mutableStateOf<List<UploadResultItem>>(emptyList()) }

    LaunchedEffect(viewModel) {
        viewModel.completedResult.collect { result ->
            if (pendingAutoShareUploads > 0) {
                pendingAutoShareUploads -= 1
                autoShareResults = autoShareResults + result
                if (pendingAutoShareUploads == 0) {
                    onAutoShareUploadFinished(autoShareResults)
                    autoShareResults = emptyList()
                }
            }
        }
    }

    if (!initialPaths.isNullOrEmpty()) {
        LaunchedEffect(initialPaths) {
            val expected = initialPaths.count { File(it).exists() }
            if (expected > 0) pendingAutoShareUploads += expected
            val added = viewModel.processFiles(initialPaths)
            if (added < expected) {
                pendingAutoShareUploads -= expected - added
            }
        }
    }
    val statusText by viewModel.statusText.collectAsState()
    val isUploading by viewModel.isUploading.collectAsState()
    val results by viewModel.results.collectAsState()
    val destinationLabel = settingsRepository?.load()?.activeDestinationDisplayName()
    var historyEntries by remember(historyRepository) { mutableStateOf<List<HistoryEntry>>(emptyList()) }
    var historyQuery by remember { mutableStateOf("") }
    val refreshHistory: () -> Unit = {
        historyEntries = historyRepository?.getRecentEntries(MAX_HOME_HISTORY_ITEMS).orEmpty()
    }
    val filteredHistory = remember(historyEntries, historyQuery) {
        val query = historyQuery.trim()
        if (query.isEmpty()) {
            historyEntries
        } else {
            historyEntries.filter {
                it.fileName.contains(query, ignoreCase = true) ||
                    it.url.contains(query, ignoreCase = true) ||
                    it.host.contains(query, ignoreCase = true)
            }
        }
    }

    LaunchedEffect(historyRepository) {
        refreshHistory()
    }

    LaunchedEffect(results.size, isUploading) {
        if (!isUploading) {
            refreshHistory()
        }
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 20.dp, vertical = 18.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item {
            Text(
                text = "XerahS",
                style = MaterialTheme.typography.titleLarge
            )
        }

        item {
            UploadStatusCard(
                destinationLabel = destinationLabel,
                statusText = statusText,
                isUploading = isUploading,
                hasResults = results.isNotEmpty(),
                onPickFiles = onPickFiles
            )
        }

        if (results.isNotEmpty()) {
            item {
                SectionHeader(title = "Current upload")
            }
            items(results) { item ->
                val itemUrl = item.url
                val itemError = item.error
                ResultCard(
                    item = item,
                    onCopyUrl = if (item.hasUrl && itemUrl != null) ({ onCopyToClipboard(itemUrl) }) else null,
                    onCopyError = if (!item.success && itemError != null) ({ onCopyToClipboard(item.errorClipboardText ?: itemError) }) else null
                )
            }
        }

        item {
            HistoryHeader(
                query = historyQuery,
                onQueryChange = { historyQuery = it },
                canClear = historyEntries.isNotEmpty(),
                onRefresh = refreshHistory,
                onClear = {
                    historyRepository?.clearEntries()
                    refreshHistory()
                }
            )
        }

        if (filteredHistory.isEmpty()) {
            item {
                EmptyHistoryCard(hasQuery = historyQuery.isNotBlank())
            }
        } else {
            items(filteredHistory, key = { it.id }) { entry ->
                HistoryCard(
                    entry = entry,
                    onCopyUrl = { onCopyToClipboard(entry.url) },
                    onDelete = {
                        historyRepository?.deleteEntry(entry.id)
                        refreshHistory()
                    }
                )
            }
        }
    }
}

@Composable
private fun UploadStatusCard(
    destinationLabel: String?,
    statusText: String,
    isUploading: Boolean,
    hasResults: Boolean,
    onPickFiles: (() -> Unit)?
) {
    val readyText = if (destinationLabel.isNullOrBlank()) {
        "Configure a destination in Settings before uploading."
    } else {
        "Ready for shared files."
    }
    val message = when {
        isUploading -> statusText
        hasResults && statusText != "Share files to XerahS to upload them." -> statusText
        else -> readyText
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = "Home",
                style = MaterialTheme.typography.titleMedium
            )
            Text(
                text = if (destinationLabel.isNullOrBlank()) "Destination: Not configured" else "Destination: $destinationLabel",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium
            )
            if (onPickFiles != null) {
                Button(onClick = onPickFiles) { Text("Choose Photo or File") }
            }
            if (isUploading) {
                CircularProgressIndicator()
            }
        }
    }
}

@Composable
private fun SectionHeader(title: String) {
    Text(
        text = title,
        style = MaterialTheme.typography.titleMedium,
        modifier = Modifier.padding(top = 4.dp)
    )
}

@Composable
private fun ResultCard(
    item: UploadResultItem,
    onCopyUrl: ((String) -> Unit)? = null,
    onCopyError: ((String) -> Unit)? = null
) {
    val url = item.url
    val err = item.error
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Text(
                text = item.fileName,
                style = MaterialTheme.typography.titleSmall
            )
            if (item.hasUrl && url != null) {
                Text(
                    text = url,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.primary
                )
                if (onCopyUrl != null) {
                    OutlinedButton(onClick = { onCopyUrl(url) }) { Text("Copy URL") }
                }
            }
            if (!item.success && err != null) {
                Text(
                    text = err,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error
                )
                if (onCopyError != null) {
                    OutlinedButton(onClick = { onCopyError(err) }) { Text("Copy Error") }
                }
            }
        }
    }
}

@Composable
private fun HistoryHeader(
    query: String,
    onQueryChange: (String) -> Unit,
    canClear: Boolean,
    onRefresh: () -> Unit,
    onClear: () -> Unit
) {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = "History",
                style = MaterialTheme.typography.titleMedium
            )
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = onRefresh) { Text("Refresh") }
                OutlinedButton(
                    enabled = canClear,
                    onClick = onClear
                ) {
                    Text("Clear")
                }
            }
        }
        OutlinedTextField(
            value = query,
            onValueChange = onQueryChange,
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text("Search history") },
            singleLine = true
        )
    }
}

@Composable
private fun EmptyHistoryCard(hasQuery: Boolean) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Text(
            text = if (hasQuery) "No matching history items." else "Uploaded links will appear here.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(16.dp)
        )
    }
}

@Composable
private fun HistoryCard(
    entry: HistoryEntry,
    onCopyUrl: () -> Unit,
    onDelete: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Column(
            modifier = Modifier.padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            Text(
                text = entry.fileName.ifBlank { "Uploaded file" },
                style = MaterialTheme.typography.titleSmall,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            if (entry.url.isNotBlank()) {
                Text(
                    text = entry.url,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.primary,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
            if (entry.host.isNotBlank()) {
                Text(
                    text = entry.host,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                if (entry.url.isNotBlank()) {
                    OutlinedButton(onClick = onCopyUrl) { Text("Copy URL") }
                }
                OutlinedButton(onClick = onDelete) { Text("Delete") }
            }
        }
    }
}
