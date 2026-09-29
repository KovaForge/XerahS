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

package com.getsharex.xerahs.mobile

import android.app.Application
import com.getsharex.xerahs.mobile.core.common.Paths
import com.getsharex.xerahs.mobile.core.data.HistoryRepository
import com.getsharex.xerahs.mobile.core.data.QueueRepository
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.data.UploadQueueWorker
import com.getsharex.xerahs.mobile.core.data.cloud.CloudApiClient
import com.getsharex.xerahs.mobile.core.data.cloud.CloudCredentialStore
import com.getsharex.xerahs.mobile.core.data.cloud.CloudOAuthManager
import com.getsharex.xerahs.mobile.core.data.cloud.CloudRepository
import java.io.File

private const val MAX_CACHE_FILE_AGE_MILLIS = 24L * 60L * 60L * 1000L

class XerahSApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        Paths.init(filesDir, cacheDir)
        Paths.ensureDirectoriesExist()
        cleanupStaleCacheFiles()
    }

    val settingsRepository: SettingsRepository by lazy { SettingsRepository() }
    val historyRepository: HistoryRepository by lazy { HistoryRepository() }
    val queueRepository: QueueRepository by lazy { QueueRepository() }
    val cloudApiClient: CloudApiClient by lazy { CloudApiClient(CloudCredentialStore(this)) }
    val cloudOAuthManager: CloudOAuthManager by lazy { CloudOAuthManager(cloudApiClient) }
    val cloudRepository: CloudRepository by lazy {
        CloudRepository(cloudApiClient, cloudOAuthManager, historyRepository)
    }
    val uploadQueueWorker: UploadQueueWorker by lazy {
        UploadQueueWorker(settingsRepository, queueRepository, historyRepository, cloudRepository)
    }

    /** Paths from share intent (or other) to process when Upload screen is ready. Cleared after consumed. */
    val pendingSharedPaths: MutableList<Array<String>> = mutableListOf()

    /** Set by NavGraph so MainActivity can navigate to Upload when share intent arrives while app is running. */
    var navController: androidx.navigation.NavController? = null

    private fun cleanupStaleCacheFiles() {
        val cutoff = System.currentTimeMillis() - MAX_CACHE_FILE_AGE_MILLIS
        cacheDir.listFiles()?.forEach { file ->
            deleteIfStaleCacheFile(file, cutoff)
        }
    }

    private fun deleteIfStaleCacheFile(file: File, cutoff: Long) {
        if (file.isDirectory) {
            file.listFiles()?.forEach { deleteIfStaleCacheFile(it, cutoff) }
            return
        }
        if (file.lastModified() < cutoff) {
            runCatching { file.delete() }
        }
    }
}
