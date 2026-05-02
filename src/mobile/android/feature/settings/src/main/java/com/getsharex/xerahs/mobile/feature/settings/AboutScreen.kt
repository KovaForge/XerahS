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

import android.os.Build
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp

private data class AboutLink(val title: String, val url: String)

private val aboutLinks = listOf(
    AboutLink("Website", "https://xerahs.com"),
    AboutLink("GitHub Project", "https://github.com/ShareX/XerahS"),
    AboutLink("Issues", "https://github.com/ShareX/XerahS/issues/"),
    AboutLink("Contributors", "https://github.com/ShareX/XerahS/graphs/contributors"),
    AboutLink("Changelog", "https://xerahs.com/changelog.html"),
    AboutLink("Privacy Policy", "https://xerahs.com/privacy-policy/")
)

private val socialLinks = listOf(
    AboutLink("X", "https://x.com/ShareX"),
    AboutLink("Discord", "https://discord.gg/ShareX"),
    AboutLink("Reddit", "https://www.reddit.com/r/sharex")
)

@Composable
fun AboutScreen(onBack: (() -> Unit)?) {
    val context = LocalContext.current
    val uriHandler = LocalUriHandler.current
    val packageInfo = context.packageManager.getPackageInfo(context.packageName, 0)
    val appName = context.applicationInfo.loadLabel(context.packageManager).toString()
    val versionName = packageInfo.versionName ?: "Unknown"
    val versionCode = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
        packageInfo.longVersionCode.toString()
    } else {
        @Suppress("DEPRECATION")
        packageInfo.versionCode.toString()
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = if (onBack != null) Arrangement.SpaceBetween else Arrangement.Start,
            verticalAlignment = Alignment.CenterVertically
        ) {
            if (onBack != null) {
                Button(onClick = onBack) { Text("Back") }
            }
            Text(text = "About", style = MaterialTheme.typography.titleLarge)
        }
        Spacer(modifier = Modifier.height(16.dp))
        Column(
            modifier = Modifier
                .weight(1f)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors()
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(20.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Image(
                        painter = painterResource(id = R.drawable.logo),
                        contentDescription = "$appName logo",
                        modifier = Modifier
                            .size(96.dp)
                            .clip(RoundedCornerShape(22.dp))
                    )
                    Text(text = appName, style = MaterialTheme.typography.headlineSmall)
                    Text(
                        text = "Version $versionName",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = "Copyright (c) 2007-2026 ShareX Team",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }

            InfoCard(
                title = "Version",
                rows = listOf(
                    "Version" to versionName,
                    "Build" to versionCode,
                    "Package ID" to context.packageName,
                    "Android" to Build.VERSION.RELEASE
                )
            )

            LinkCard(title = "Links", links = aboutLinks, onOpen = uriHandler::openUri)
            LinkCard(title = "Social", links = socialLinks, onOpen = uriHandler::openUri)
        }
    }
}

@Composable
private fun InfoCard(title: String, rows: List<Pair<String, String>>) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(text = title, style = MaterialTheme.typography.titleMedium)
            rows.forEach { (label, value) ->
                Row(modifier = Modifier.fillMaxWidth()) {
                    Text(
                        text = label,
                        modifier = Modifier.weight(1f),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = value,
                        modifier = Modifier.weight(1f),
                        style = MaterialTheme.typography.bodyMedium
                    )
                }
            }
        }
    }
}

@Composable
private fun LinkCard(title: String, links: List<AboutLink>, onOpen: (String) -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors()
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(text = title, style = MaterialTheme.typography.titleMedium)
            links.forEach { link ->
                OutlinedButton(
                    onClick = { onOpen(link.url) },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalAlignment = Alignment.Start
                    ) {
                        Text(text = link.title)
                        Text(
                            text = link.url,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }
    }
}
