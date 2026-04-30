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
import com.getsharex.xerahs.mobile.core.data.DestinationConfigImporter
import java.io.File

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
        val paths = ShareIntentHandler.handleIntent(this, intent) ?: return
        val app = application as? XerahSApplication ?: return
        val xsdcPaths = paths.filter { it.substringAfterLast('.', "").equals("xsdc", ignoreCase = true) }
        val uploadPaths = paths.filterNot { it.substringAfterLast('.', "").equals("xsdc", ignoreCase = true) }

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
