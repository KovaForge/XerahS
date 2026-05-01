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

import android.app.AlertDialog
import android.content.Intent
import android.os.Bundle
import android.text.InputType
import android.widget.EditText
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import androidx.navigation.compose.rememberNavController
import com.getsharex.xerahs.mobile.navigation.Screen
import com.getsharex.xerahs.mobile.ui.theme.XerahSTheme
import com.getsharex.xerahs.mobile.navigation.XerahSNavGraph
import com.getsharex.xerahs.mobile.core.data.CustomUploaderImporter
import com.getsharex.xerahs.mobile.core.data.DestinationConfigImporter
import java.io.File
import java.net.URL

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        // Parse share intent and store paths; navigation to Upload happens in NavGraph
        // (when startDestination is Upload if pending paths exist, or after Loading completes)
        handleShareIntent(intent)
        setContent {
            XerahSTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    val navController = rememberNavController()
                    (application as? XerahSApplication)?.navController = navController
                    XerahSNavGraph(navController = navController)
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleShareIntent(intent)
    }

    private fun handleShareIntent(intent: Intent?) {
        if (handleXerahsDeepLink(intent)) return

        val paths = ShareIntentHandler.handleIntent(this, intent) ?: return
        val app = application as? XerahSApplication ?: return
        val sxcuPaths = paths.filter { it.substringAfterLast('.', "").equals("sxcu", ignoreCase = true) }
        val xsdcPaths = paths.filter { it.substringAfterLast('.', "").equals("xsdc", ignoreCase = true) }
        val uploadPaths = paths.filterNot {
            val ext = it.substringAfterLast('.', "")
            ext.equals("sxcu", ignoreCase = true) || ext.equals("xsdc", ignoreCase = true)
        }

        if (sxcuPaths.isNotEmpty()) {
            importCustomUploaderFiles(app, sxcuPaths)
        }

        xsdcPaths.firstOrNull()?.let { path ->
            promptForDestinationConfigPassphrase(app, path)
        }

        if (uploadPaths.isEmpty()) {
            return
        }

        synchronized(app.pendingSharedPaths) {
            app.pendingSharedPaths.add(uploadPaths.toTypedArray())
        }
        app.navController?.navigate(Screen.Upload.route) {
            popUpTo(Screen.Upload.route) { inclusive = true }
            launchSingleTop = true
        }
    }

    private fun handleXerahsDeepLink(intent: Intent?): Boolean {
        val uri = intent?.data ?: return false
        if (!uri.scheme.equals("xerahs", ignoreCase = true)) return false

        val normalizedHost = uri.host.orEmpty().lowercase()
        val normalizedPath = uri.path.orEmpty().trim('/').lowercase()
        if (normalizedHost != "import-sxcu" && normalizedPath != "import-sxcu") {
            return false
        }

        val target = uri.getQueryParameter("url")
        if (target.isNullOrBlank()) {
            Toast.makeText(this, "Invalid import link. Missing remote .sxcu URL.", Toast.LENGTH_LONG).show()
            return true
        }

        val parsed = try {
            URL(target)
        } catch (e: Exception) {
            Toast.makeText(this, "Invalid import link. Missing or invalid remote .sxcu URL.", Toast.LENGTH_LONG).show()
            return true
        }
        if (parsed.protocol.lowercase() !in setOf("http", "https")) {
            Toast.makeText(this, "Invalid import link. Missing or invalid remote .sxcu URL.", Toast.LENGTH_LONG).show()
            return true
        }

        val app = application as? XerahSApplication ?: return true
        Thread {
            try {
                val bytes = parsed.openStream().use { it.readBytes() }
                val result = CustomUploaderImporter(app.settingsRepository).import(
                    bytes,
                    parsed.path.substringAfterLast('/').ifBlank { parsed.toString() }
                )
                runOnUiThread {
                    showCustomUploaderImportResult(result.updatedExisting, result.displayName)
                }
            } catch (e: Exception) {
                runOnUiThread {
                    Toast.makeText(this, e.message ?: "Failed to download .sxcu", Toast.LENGTH_LONG).show()
                }
            }
        }.start()
        return true
    }

    private fun importCustomUploaderFiles(app: XerahSApplication, paths: List<String>) {
        val importer = CustomUploaderImporter(app.settingsRepository)
        var lastImportedName: String? = null
        var updatedCount = 0
        var importedCount = 0

        for (path in paths) {
            try {
                val result = importer.import(File(path).readBytes(), File(path).name)
                lastImportedName = result.displayName
                if (result.updatedExisting) {
                    updatedCount++
                } else {
                    importedCount++
                }
            } catch (e: Exception) {
                Toast.makeText(this, e.message ?: "Failed to import .sxcu", Toast.LENGTH_LONG).show()
            }
        }

        if (lastImportedName != null) {
            val message = when {
                importedCount + updatedCount == 1 -> if (updatedCount == 1) {
                    "Updated custom uploader: $lastImportedName"
                } else {
                    "Imported custom uploader: $lastImportedName"
                }
                else -> "Imported $importedCount and updated $updatedCount custom uploader(s)."
            }
            Toast.makeText(this, message, Toast.LENGTH_LONG).show()
            app.navController?.navigate(Screen.CustomUploaderConfig.route) {
                launchSingleTop = true
            }
        }
    }

    private fun showCustomUploaderImportResult(updatedExisting: Boolean, displayName: String) {
        val message = if (updatedExisting) {
            "Updated custom uploader: $displayName"
        } else {
            "Imported custom uploader: $displayName"
        }
        Toast.makeText(this, message, Toast.LENGTH_LONG).show()
        (application as? XerahSApplication)?.navController?.navigate(Screen.CustomUploaderConfig.route) {
            launchSingleTop = true
        }
    }

    private fun promptForDestinationConfigPassphrase(app: XerahSApplication, path: String) {
        val input = EditText(this).apply {
            hint = "Passphrase"
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD
        }

        AlertDialog.Builder(this)
            .setTitle("Import .xsdc")
            .setMessage(File(path).name)
            .setView(input)
            .setNegativeButton("Cancel", null)
            .setPositiveButton("Import") { _, _ ->
                try {
                    val displayName = DestinationConfigImporter(app.settingsRepository).import(
                        File(path).readBytes(),
                        input.text?.toString().orEmpty()
                    )
                    Toast.makeText(this, "Imported destination config: $displayName", Toast.LENGTH_LONG).show()
                    app.navController?.navigate(Screen.S3Config.route) {
                        launchSingleTop = true
                    }
                } catch (e: Exception) {
                    Toast.makeText(this, e.message ?: "Failed to import .xsdc", Toast.LENGTH_LONG).show()
                }
            }
            .show()
    }
}
