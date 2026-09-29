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

package com.getsharex.xerahs.mobile.feature.history

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
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import com.getsharex.xerahs.mobile.core.data.cloud.CloudRepository
import com.getsharex.xerahs.mobile.core.domain.CloudGalleryItem
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class CloudHistoryState(
    val items: List<CloudGalleryItem> = emptyList(),
    val nextCursor: String? = null,
    val loading: Boolean = false,
    val error: String? = null,
    val pendingDeleteId: String? = null
)

class CloudHistoryViewModel(private val repository: CloudRepository) : ViewModel() {
    private val _state = MutableStateFlow(CloudHistoryState())
    val state: StateFlow<CloudHistoryState> = _state.asStateFlow()

    init { refresh() }

    fun refresh() = load(cursor = null)
    fun loadMore() = _state.value.nextCursor?.let { load(it) } ?: Unit

    fun unpublish(item: CloudGalleryItem) {
        viewModelScope.launch {
            _state.value = _state.value.copy(pendingDeleteId = item.clientItemId, error = null)
            try {
                val removed = repository.unpublish(item.clientItemId)
                _state.value = _state.value.copy(
                    items = if (removed) _state.value.items.filterNot { it.clientItemId == item.clientItemId } else _state.value.items,
                    pendingDeleteId = null,
                    error = if (removed) null else "Unpublish was accepted and is still being replicated. Refresh shortly."
                )
            } catch (error: Exception) {
                _state.value = _state.value.copy(pendingDeleteId = null, error = error.message ?: "Unpublish failed.")
            }
        }
    }

    private fun load(cursor: String?) {
        if (_state.value.loading) return
        viewModelScope.launch {
            _state.value = _state.value.copy(loading = true, error = null)
            try {
                val page = repository.listItems(cursor)
                _state.value = CloudHistoryState(
                    items = if (cursor == null) page.items else _state.value.items + page.items,
                    nextCursor = page.nextCursor
                )
            } catch (error: Exception) {
                _state.value = _state.value.copy(loading = false, error = error.message ?: "Cloud history failed to load.")
            }
        }
    }
}

@Composable
fun CloudHistoryScreen(
    repository: CloudRepository,
    onBack: () -> Unit,
    onOpenUrl: (String) -> Unit,
    onCopyUrl: (String) -> Unit,
    viewModel: CloudHistoryViewModel = viewModel(
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T = CloudHistoryViewModel(repository) as T
        }
    )
) {
    val state by viewModel.state.collectAsState()
    Column(Modifier.fillMaxSize().padding(16.dp)) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Button(onClick = onBack) { Text("Back") }
            OutlinedButton(onClick = viewModel::refresh, enabled = !state.loading) { Text("Refresh") }
        }
        Spacer(Modifier.height(16.dp))
        Text("Cloud History", style = MaterialTheme.typography.titleLarge)
        Text(
            "Remote items published to your XerahS Cloud gallery.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
        state.error?.let {
            Spacer(Modifier.height(8.dp))
            Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
        }
        Spacer(Modifier.height(12.dp))
        LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            if (state.items.isEmpty() && !state.loading) {
                item { Text("No Cloud items yet.") }
            }
            items(state.items, key = { it.clientItemId }) { item ->
                Card(Modifier.fillMaxWidth()) {
                    Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        Text(item.fileName, style = MaterialTheme.typography.titleSmall, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text(item.url, style = MaterialTheme.typography.bodySmall, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        Text("${item.kind} · ${item.publishedAt}", style = MaterialTheme.typography.bodySmall)
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedButton(onClick = { onOpenUrl(item.url) }) { Text("Open") }
                            OutlinedButton(onClick = { onCopyUrl(item.url) }) { Text("Copy") }
                            OutlinedButton(
                                enabled = state.pendingDeleteId != item.clientItemId,
                                onClick = { viewModel.unpublish(item) }
                            ) { Text(if (state.pendingDeleteId == item.clientItemId) "Removing..." else "Unpublish") }
                        }
                    }
                }
            }
            if (state.nextCursor != null) {
                item { Button(onClick = viewModel::loadMore, enabled = !state.loading) { Text("Load more") } }
            }
            if (state.loading) item { Text("Loading...") }
        }
    }
}
