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

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.getsharex.xerahs.mobile.core.data.SettingsRepository
import com.getsharex.xerahs.mobile.core.data.cloud.CloudRepository
import com.getsharex.xerahs.mobile.core.domain.ApplicationConfig
import com.getsharex.xerahs.mobile.core.domain.selectableDestinations

@Composable
fun SettingsHubScreen(
    settingsRepository: SettingsRepository,
    onBack: (() -> Unit)?,
    onNavigateToS3: () -> Unit,
    onNavigateToCustomUploader: () -> Unit,
    cloudRepository: CloudRepository? = null,
    onStartCloudSignIn: (String) -> Unit = {},
    onOpenCloudSettings: () -> Unit = {},
    onNavigateCloudHistory: () -> Unit = {},
    onRefresh: () -> Unit = {}
) {
    val config = settingsRepository.load()
    val s3Configured = config.s3Config.isConfigured
    val customCount = config.customUploaders.size
    var convertHeicToPng by remember { mutableStateOf(config.convertHeicToPng) }
    var cloudAutoPublish by remember { mutableStateOf(config.cloudAutoPublish) }
    val cloudConnection by cloudRepository?.connection?.collectAsState()
        ?: remember { mutableStateOf(com.getsharex.xerahs.mobile.core.domain.CloudConnectionState()) }
    var selectedDestinationId by remember(config.defaultDestinationInstanceId) {
        mutableStateOf(config.defaultDestinationInstanceId)
    }
    var showResetConfirm by remember { mutableStateOf(false) }
    var resetMessage by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(cloudRepository) {
        if (cloudRepository?.apiClient?.hasCredential == true && cloudConnection.account == null) {
            cloudRepository.restore()
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = if (onBack != null) Arrangement.SpaceBetween else Arrangement.End,
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (onBack != null) {
                Button(onClick = onBack) { Text("Back") }
            }
            OutlinedButton(onClick = onRefresh) { Text("Refresh") }
        }
        Spacer(modifier = Modifier.height(16.dp))
        Text(
            text = "Settings",
            style = MaterialTheme.typography.titleLarge
        )
        Spacer(modifier = Modifier.height(16.dp))
        Column(
            modifier = Modifier
                .weight(1f)
                .verticalScroll(rememberScrollState())
        ) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Column(
                    modifier = Modifier.padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Text("XerahS Cloud", style = MaterialTheme.typography.titleMedium)
                    val account = cloudConnection.account
                    Text(
                        text = when {
                            cloudConnection.restoring -> "Restoring your secure Cloud session..."
                            account != null -> "Connected as @${account.slug}"
                            else -> "Not connected"
                        },
                        style = MaterialTheme.typography.bodyMedium
                    )
                    cloudConnection.error?.let {
                        Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                    }
                    if (account == null) {
                        Button(
                            enabled = cloudRepository != null && !cloudConnection.restoring,
                            onClick = {
                                cloudRepository?.oauthManager?.begin()?.authorizationUrl?.let(onStartCloudSignIn)
                            }
                        ) { Text("Sign in to Cloud") }
                    } else {
                        Text(
                            text = "Trial: ${account.trialStatus} · ${if (account.canPublish) "Publishing enabled" else "Publishing unavailable"}",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Checkbox(
                                checked = cloudAutoPublish,
                                enabled = account.canPublish,
                                onCheckedChange = {
                                    cloudAutoPublish = it
                                    settingsRepository.setCloudAutoPublish(it)
                                }
                            )
                            Text("Automatically publish image and video uploads")
                        }
                        Text(
                            "Cloud publishing is online-only. Failed publishes stay in local history for manual retry.",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            OutlinedButton(onClick = onNavigateCloudHistory) { Text("Cloud History") }
                            OutlinedButton(onClick = onOpenCloudSettings) { Text("Account Settings") }
                        }
                        OutlinedButton(onClick = {
                            cloudRepository?.signOut()
                            cloudAutoPublish = false
                            settingsRepository.setCloudAutoPublish(false)
                        }) { Text("Sign out") }
                    }
                }
            }
            Spacer(modifier = Modifier.height(12.dp))
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = "Upload options",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Checkbox(
                            checked = convertHeicToPng,
                            onCheckedChange = {
                                convertHeicToPng = it
                                settingsRepository.setConvertHeicToPng(it)
                            }
                        )
                        Text(
                            text = "Convert HEIC/HEIF images to PNG before upload",
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                    Text(
                        text = "So images display in browsers instead of prompting download.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
            Spacer(modifier = Modifier.height(12.dp))
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = "Active upload destination",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "Choose where shared files will be uploaded. This destination is used when you share to XerahS.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    val options = config.selectableDestinations()
                    val effectiveId = selectedDestinationId ?: config.defaultDestinationInstanceId ?: options.firstOrNull()?.second
                    if (options.isEmpty()) {
                        Text(
                            text = "No destination configured. Set up Amazon S3 or a custom uploader below.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    } else {
                        options.forEach { (displayName, instanceId) ->
                            val isSelected = effectiveId == instanceId
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 4.dp),
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                RadioButton(
                                    selected = isSelected,
                                    onClick = {
                                        selectedDestinationId = instanceId
                                        settingsRepository.setDefaultDestinationInstanceId(instanceId)
                                    }
                                )
                                Text(
                                    text = displayName,
                                    style = MaterialTheme.typography.bodyLarge,
                                    modifier = Modifier.padding(start = 8.dp)
                                )
                            }
                        }
                    }
                }
            }
            Spacer(modifier = Modifier.height(12.dp))
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = "Upload Destinations",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "Configure where your files will be uploaded. Amazon S3 and custom uploaders are supported.",
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
            Spacer(modifier = Modifier.height(12.dp))
            Card(
                onClick = onNavigateToS3,
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Row(
                    modifier = Modifier.padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(text = "Amazon S3", style = MaterialTheme.typography.titleSmall)
                        Text(
                            text = if (s3Configured) "Bucket: ${config.s3Config.bucketName}" else "Not configured - tap to set up",
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }
            }
            Spacer(modifier = Modifier.height(8.dp))
            Card(
                onClick = onNavigateToCustomUploader,
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Row(
                    modifier = Modifier.padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(text = "Custom Uploader", style = MaterialTheme.typography.titleSmall)
                        Text(
                            text = if (customCount > 0) "$customCount uploader(s)" else "Not configured - tap to add",
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }
            }
            Spacer(modifier = Modifier.height(12.dp))
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = "Privacy and local data",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "Remove S3 keys, custom uploader secrets, selected destination, and local upload settings from this device.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    OutlinedButton(onClick = { showResetConfirm = true }) {
                        Text("Delete Credentials / Reset Settings")
                    }
                    resetMessage?.let {
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = it,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }
    }

    if (showResetConfirm) {
        AlertDialog(
            onDismissRequest = { showResetConfirm = false },
            title = { Text("Reset upload settings?") },
            text = { Text("This clears S3 credentials, custom uploaders, selected destination, and upload preferences stored on this device.") },
            confirmButton = {
                Button(
                    onClick = {
                        settingsRepository.save(ApplicationConfig())
                        convertHeicToPng = true
                        selectedDestinationId = null
                        resetMessage = "Upload settings and credentials were cleared."
                        showResetConfirm = false
                        onRefresh()
                    }
                ) {
                    Text("Reset")
                }
            },
            dismissButton = {
                OutlinedButton(onClick = { showResetConfirm = false }) {
                    Text("Cancel")
                }
            }
        )
    }
}
