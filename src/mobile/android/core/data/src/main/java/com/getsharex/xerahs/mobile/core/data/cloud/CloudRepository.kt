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

import com.getsharex.xerahs.mobile.core.data.HistoryRepository
import com.getsharex.xerahs.mobile.core.domain.CloudConnectionState
import com.getsharex.xerahs.mobile.core.domain.CloudGalleryPage
import com.getsharex.xerahs.mobile.core.domain.CloudPublishRequest
import com.getsharex.xerahs.mobile.core.domain.HistoryEntry
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import java.time.Instant
import java.util.UUID

const val CLOUD_TAG_CLIENT_ITEM_ID = "xerahsCloudClientItemId"
const val CLOUD_TAG_OWNER_SUBJECT = "xerahsCloudOwnerSubject"
const val CLOUD_TAG_STATUS = "xerahsCloudStatus"
const val CLOUD_TAG_ERROR = "xerahsCloudError"
const val CLOUD_TAG_KIND = "xerahsCloudKind"
const val CLOUD_TAG_CONTENT_TYPE = "xerahsCloudContentType"
const val CLOUD_STATUS_PENDING = "pending"
const val CLOUD_STATUS_PUBLISHED = "published"
const val CLOUD_STATUS_FAILED = "failed"

class CloudRepository(
    val apiClient: CloudApiClient,
    val oauthManager: CloudOAuthManager,
    private val historyRepository: HistoryRepository
) {
    private val _connection = MutableStateFlow(CloudConnectionState())
    val connection: StateFlow<CloudConnectionState> = _connection.asStateFlow()

    suspend fun restore() {
        _connection.value = CloudConnectionState(restoring = true)
        try {
            if (apiClient.restoreSession()) refreshAccount() else _connection.value = CloudConnectionState()
        } catch (error: Exception) {
            _connection.value = CloudConnectionState(error = safeMessage(error))
        }
    }

    suspend fun refreshAccount() {
        try {
            _connection.value = CloudConnectionState(account = apiClient.getAccount())
        } catch (error: Exception) {
            _connection.value = CloudConnectionState(error = safeMessage(error))
            throw error
        }
    }

    suspend fun completeOAuth(callback: android.net.Uri) {
        try {
            oauthManager.complete(callback)
            refreshAccount()
        } catch (error: Exception) {
            _connection.value = CloudConnectionState(error = safeMessage(error))
            throw error
        }
    }

    fun signOut() {
        apiClient.signOut()
        _connection.value = CloudConnectionState()
    }

    suspend fun listItems(cursor: String? = null): CloudGalleryPage = apiClient.listItems(cursor)

    suspend fun unpublish(clientItemId: String): Boolean = apiClient.unpublish(clientItemId)

    suspend fun publishUploaded(historyId: Long, fileName: String, sourcePath: String, url: String): Result<Unit> {
        val media = CloudPublishPolicy.eligibleMedia(fileName, sourcePath) ?: return Result.success(Unit)
        val ownerSubject = apiClient.currentOwnerSubject
            ?: return Result.failure(CloudSecurityException("Sign in to XerahS Cloud before publishing."))
        val clientItemId = UUID.randomUUID().toString()
        val tags = mapOf(
            CLOUD_TAG_CLIENT_ITEM_ID to clientItemId,
            CLOUD_TAG_OWNER_SUBJECT to ownerSubject,
            CLOUD_TAG_STATUS to CLOUD_STATUS_PENDING,
            CLOUD_TAG_KIND to media.first,
            CLOUD_TAG_CONTENT_TYPE to media.second
        )
        historyRepository.updateTags(historyId, tags)
        return publishAndRecord(historyId, tags, fileName, url, Instant.now().toString())
    }

    suspend fun retry(entry: HistoryEntry): Result<Unit> {
        val clientItemId = entry.tags[CLOUD_TAG_CLIENT_ITEM_ID]
            ?: return Result.failure(CloudException("This history item has no Cloud publish identifier."))
        val storedOwner = entry.tags[CLOUD_TAG_OWNER_SUBJECT]
            ?: return Result.failure(CloudSecurityException("This history item has no Cloud account owner and cannot be retried safely."))
        val currentOwner = apiClient.currentOwnerSubject
        if (!CloudPublishPolicy.canRetryForOwner(storedOwner, currentOwner)) {
            return Result.failure(CloudSecurityException("This history item belongs to a different XerahS Cloud account."))
        }
        val kind = entry.tags[CLOUD_TAG_KIND]
            ?: return Result.failure(CloudException("This history item is missing its Cloud media kind."))
        val contentType = entry.tags[CLOUD_TAG_CONTENT_TYPE]
        val tags = entry.tags + (CLOUD_TAG_STATUS to CLOUD_STATUS_PENDING) - CLOUD_TAG_ERROR
        historyRepository.updateTags(entry.id, tags)
        return publishAndRecord(entry.id, tags + (CLOUD_TAG_CLIENT_ITEM_ID to clientItemId) + (CLOUD_TAG_KIND to kind), entry.fileName, entry.url, entry.dateTime, contentType)
    }

    private suspend fun publishAndRecord(
        historyId: Long,
        tags: Map<String, String?>,
        fileName: String,
        url: String,
        capturedAt: String,
        contentTypeOverride: String? = tags[CLOUD_TAG_CONTENT_TYPE]
    ): Result<Unit> = runCatching {
        val parsed = url.toHttpUrlOrNull()
        val request = CloudPublishRequest(
            clientItemId = tags[CLOUD_TAG_CLIENT_ITEM_ID]!!,
            url = url,
            kind = tags[CLOUD_TAG_KIND]!!,
            fileName = fileName.substringAfterLast('/').substringAfterLast('\\'),
            capturedAt = capturedAt,
            host = parsed?.host,
            contentType = contentTypeOverride
        )
        apiClient.publish(request, tags[CLOUD_TAG_OWNER_SUBJECT]!!)
        historyRepository.updateTags(historyId, tags + (CLOUD_TAG_STATUS to CLOUD_STATUS_PUBLISHED) - CLOUD_TAG_ERROR)
        Unit
    }.onFailure { error ->
        historyRepository.updateTags(
            historyId,
            tags + (CLOUD_TAG_STATUS to CLOUD_STATUS_FAILED) + (CLOUD_TAG_ERROR to safeMessage(error))
        )
    }

    private fun safeMessage(error: Throwable): String =
        (error.message ?: "XerahS Cloud request failed.").replace('\r', ' ').replace('\n', ' ').trim().take(320)
}
