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

package com.getsharex.xerahs.mobile.navigation

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarDuration
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavDestination
import androidx.navigation.NavHostController
import kotlinx.coroutines.launch
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.getsharex.xerahs.mobile.XerahSApplication
import com.getsharex.xerahs.mobile.ui.screens.LoadingScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderSettingsScreen
import com.getsharex.xerahs.mobile.feature.upload.UploadScreen
import com.getsharex.xerahs.mobile.ui.screens.PlaceholderUploadScreen
import com.getsharex.xerahs.mobile.feature.settings.AboutScreen
import com.getsharex.xerahs.mobile.feature.settings.SettingsHubScreen
import com.getsharex.xerahs.mobile.feature.settings.S3ConfigScreen
import com.getsharex.xerahs.mobile.feature.settings.CustomUploaderConfigScreen
import com.getsharex.xerahs.mobile.feature.history.CloudHistoryScreen
import com.getsharex.xerahs.mobile.core.data.cloud.CLOUD_SETTINGS_URL
import com.getsharex.xerahs.mobile.core.domain.UploadResultItem

@Composable
fun XerahSNavGraph(
    navController: NavHostController = rememberNavController()
) {
    val context = LocalContext.current
    val app = context.applicationContext as? XerahSApplication
    val clipboardManager = context.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val onCopyToClipboard: (String) -> Unit = { text ->
        clipboardManager?.setPrimaryClip(ClipData.newPlainText("url", text))
        scope.launch {
            snackbarHostState.showSnackbar(
                message = "Copied to clipboard",
                duration = SnackbarDuration.Short
            )
        }
    }
    val onAutoShareUploadFinished: (List<UploadResultItem>) -> Unit = { results ->
        val successes = results.filter { it.success }
        val failures = results.filterNot { it.success }
        val urls = successes.mapNotNull { it.url }.filter { it.isNotBlank() }
        if (urls.isNotEmpty()) {
            clipboardManager?.setPrimaryClip(ClipData.newPlainText("urls", urls.joinToString("\n")))
        }
        val message = when {
            failures.isEmpty() && urls.size == 1 -> "Upload complete. Link copied to clipboard."
            failures.isEmpty() && urls.size > 1 -> "Uploads complete. ${urls.size} links copied to clipboard."
            successes.isEmpty() -> "Upload failed. Open XerahS to review the error details."
            else -> "Upload finished with errors. ${successes.size} completed, ${failures.size} failed."
        }
        scope.launch {
            snackbarHostState.showSnackbar(
                message = message,
                duration = SnackbarDuration.Short
            )
        }
    }

    val hasPendingShare = app?.pendingSharedPaths?.isNotEmpty() == true
    val navBackStackEntry = navController.currentBackStackEntryAsState()
    val currentDestination = navBackStackEntry.value?.destination
    val selectedTopLevelRoute = currentDestination?.selectedTopLevelRoute()
    val showTopLevelNavigation = selectedTopLevelRoute != null

    Scaffold(
        snackbarHost = { SnackbarHost(hostState = snackbarHostState) },
        bottomBar = {
            if (showTopLevelNavigation) {
                XerahSNavigationBar(
                    selectedRoute = selectedTopLevelRoute,
                    onNavigateHome = {
                        navController.navigateTopLevel(Screen.Upload.route)
                    },
                    onNavigateSettings = {
                        navController.navigateTopLevel(Screen.Settings.route)
                    },
                    onNavigateAbout = {
                        navController.navigateTopLevel(Screen.About.route)
                    }
                )
            }
        }
    ) { paddingValues ->
    NavHost(
        modifier = Modifier.padding(paddingValues),
        navController = navController,
        startDestination = if (hasPendingShare) Screen.Upload.route else Screen.Loading.route
    ) {
        composable(Screen.Loading.route) {
            LoadingScreen(
                onInitComplete = {
                    navController.navigate(Screen.Upload.route) {
                        popUpTo(Screen.Loading.route) { inclusive = true }
                    }
                }
            )
        }
        composable(Screen.Upload.route) {
            val worker = app?.uploadQueueWorker
            if (worker != null) {
                val pending = synchronized(app.pendingSharedPaths) {
                    app.pendingSharedPaths.removeFirstOrNull()
                }
                UploadScreen(
                    worker = worker,
                    onPickFiles = null,
                    onCopyToClipboard = onCopyToClipboard,
                    onAutoShareUploadFinished = onAutoShareUploadFinished,
                    initialPaths = pending,
                    historyRepository = app.historyRepository,
                    settingsRepository = app.settingsRepository,
                    cloudRepository = app.cloudRepository
                )
            } else {
                PlaceholderUploadScreen()
            }
        }
        composable(Screen.Settings.route) {
            val settingsRepo = app?.settingsRepository
            if (settingsRepo != null) {
                SettingsHubScreen(
                    settingsRepository = settingsRepo,
                    onBack = null,
                    onNavigateToS3 = { navController.navigate(Screen.S3Config.route) },
                    onNavigateToCustomUploader = { navController.navigate(Screen.CustomUploaderConfig.route) },
                    cloudRepository = app.cloudRepository,
                    onStartCloudSignIn = { url -> context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) },
                    onOpenCloudSettings = { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(CLOUD_SETTINGS_URL))) },
                    onNavigateCloudHistory = { navController.navigate(Screen.CloudHistory.route) },
                    onRefresh = { }
                )
            } else {
                PlaceholderSettingsScreen(onBack = null)
            }
        }
        composable(Screen.S3Config.route) {
            S3ConfigScreen(
                settingsRepository = app?.settingsRepository,
                onBack = { navController.popBackStack() }
            )
        }
        composable(Screen.CustomUploaderConfig.route) {
            CustomUploaderConfigScreen(
                settingsRepository = app?.settingsRepository,
                onBack = { navController.popBackStack() }
            )
        }
        composable(Screen.CloudHistory.route) {
            val cloudRepository = app?.cloudRepository
            if (cloudRepository != null) {
                CloudHistoryScreen(
                    repository = cloudRepository,
                    onBack = { navController.popBackStack() },
                    onOpenUrl = { url -> context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) },
                    onCopyUrl = onCopyToClipboard
                )
            } else {
                Text("XerahS Cloud is unavailable.")
            }
        }
        composable(Screen.About.route) {
            AboutScreen(onBack = null)
        }
    }
    }
}

@Composable
private fun XerahSNavigationBar(
    selectedRoute: String?,
    onNavigateHome: () -> Unit,
    onNavigateSettings: () -> Unit,
    onNavigateAbout: () -> Unit
) {
    NavigationBar {
        NavigationBarItem(
            selected = selectedRoute == Screen.Upload.route,
            onClick = onNavigateHome,
            icon = {
                Icon(
                    imageVector = Icons.Filled.Home,
                    contentDescription = "Home"
                )
            },
            label = { Text("Home") }
        )
        NavigationBarItem(
            selected = selectedRoute == Screen.Settings.route,
            onClick = onNavigateSettings,
            icon = {
                Icon(
                    imageVector = Icons.Filled.Settings,
                    contentDescription = "Settings"
                )
            },
            label = { Text("Settings") }
        )
        NavigationBarItem(
            selected = selectedRoute == Screen.About.route,
            onClick = onNavigateAbout,
            icon = {
                Icon(
                    imageVector = Icons.Filled.Info,
                    contentDescription = "About"
                )
            },
            label = { Text("About") }
        )
    }
}

private fun NavDestination.selectedTopLevelRoute(): String? =
    when {
        route == Screen.Loading.route -> null
        route?.startsWith("settings") == true -> Screen.Settings.route
        route == Screen.About.route -> Screen.About.route
        else -> Screen.Upload.route
    }

private fun NavHostController.navigateTopLevel(route: String) {
    navigate(route) {
        popUpTo(Screen.Upload.route) {
            saveState = true
        }
        launchSingleTop = true
        restoreState = true
    }
}
