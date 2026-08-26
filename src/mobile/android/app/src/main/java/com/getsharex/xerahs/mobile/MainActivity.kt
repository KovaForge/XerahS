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
import androidx.lifecycle.lifecycleScope
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
import com.getsharex.xerahs.mobile.core.data.CustomUploaderImportPreview
import com.getsharex.xerahs.mobile.core.data.DestinationConfigImporter
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.launch

private const val MAX_REMOTE_SXCU_BYTES = 1024L * 1024L
private const val MAX_REMOTE_SXCU_REDIRECTS = 5

class MainActivity : ComponentActivity() {
    private val remoteImportClient: OkHttpClient by lazy {
        OkHttpClient.Builder()
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(10, TimeUnit.SECONDS)
            .followRedirects(false)
            .followSslRedirects(false)
            .build()
    }

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
        if (handleCloudOAuthCallback(intent)) return
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

        enqueueUploadPathsWithConsent(app, uploadPaths)
    }

    private fun handleCloudOAuthCallback(intent: Intent?): Boolean {
        val uri = intent?.data ?: return false
        if (!uri.scheme.equals("xerahs", ignoreCase = true) ||
            !uri.host.equals("oauth", ignoreCase = true) || uri.path != "/callback") return false
        val app = application as? XerahSApplication ?: return true
        // Do not retain authorization codes in the Activity intent or replay them after recreation.
        setIntent(Intent(Intent.ACTION_MAIN))
        lifecycleScope.launch {
            try {
                app.cloudRepository.completeOAuth(uri)
                Toast.makeText(this@MainActivity, "Signed in to XerahS Cloud.", Toast.LENGTH_LONG).show()
                app.navController?.navigate(Screen.Settings.route) { launchSingleTop = true }
            } catch (error: Exception) {
                Toast.makeText(this@MainActivity, error.message ?: "XerahS Cloud sign-in failed.", Toast.LENGTH_LONG).show()
            }
        }
        return true
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

        val parsed = target.toHttpUrlOrNull()
        if (parsed == null || parsed.scheme != "https") {
            Toast.makeText(this, "Invalid import link. Missing or invalid remote .sxcu URL.", Toast.LENGTH_LONG).show()
            return true
        }

        val app = application as? XerahSApplication ?: return true
        Thread {
            try {
                val bytes = downloadRemoteSxcu(parsed)
                val sourceLabel = parsed.encodedPath.substringAfterLast('/').ifBlank { parsed.toString() }
                val importer = CustomUploaderImporter(app.settingsRepository)
                val preview = importer.preview(bytes, sourceLabel)
                runOnUiThread {
                    confirmRemoteCustomUploaderImport(parsed.toString(), preview) {
                        try {
                            val result = importer.import(bytes, sourceLabel)
                            showCustomUploaderImportResult(result.updatedExisting, result.displayName)
                        } catch (e: Exception) {
                            Toast.makeText(this, e.message ?: "Failed to import .sxcu", Toast.LENGTH_LONG).show()
                        }
                    }
                }
            } catch (e: Exception) {
                runOnUiThread {
                    Toast.makeText(this, e.message ?: "Failed to download .sxcu", Toast.LENGTH_LONG).show()
                }
            }
        }.start()
        return true
    }

    private fun enqueueUploadPathsWithConsent(app: XerahSApplication, uploadPaths: List<String>) {
        if (app.settingsRepository.hasAcceptedFirstUploadWarning()) {
            enqueueUploadPaths(app, uploadPaths)
            return
        }

        var shouldCleanup = true
        val dialog = AlertDialog.Builder(this)
            .setTitle("Confirm upload")
            .setMessage(
                "XerahS uploads files you select or share to the destination you configure, such as S3 or a custom uploader. " +
                    "XerahS does not host these files by default. The destination service may store, process, or expose your uploaded content according to its own settings and privacy policy."
            )
            .setNegativeButton("Cancel") { _, _ ->
                cleanupUploadPaths(app, uploadPaths)
                Toast.makeText(this, "Upload cancelled.", Toast.LENGTH_SHORT).show()
            }
            .setPositiveButton("Upload") { _, _ ->
                shouldCleanup = false
                app.settingsRepository.setFirstUploadWarningAccepted(true)
                enqueueUploadPaths(app, uploadPaths)
            }
            .create()
        dialog.setOnCancelListener {
            if (shouldCleanup) {
                cleanupUploadPaths(app, uploadPaths)
                Toast.makeText(this, "Upload cancelled.", Toast.LENGTH_SHORT).show()
            }
        }
        dialog.show()
    }

    private fun enqueueUploadPaths(app: XerahSApplication, uploadPaths: List<String>) {
        synchronized(app.pendingSharedPaths) {
            app.pendingSharedPaths.add(uploadPaths.toTypedArray())
        }
        app.navController?.navigate(Screen.Upload.route) {
            popUpTo(Screen.Upload.route) { inclusive = true }
            launchSingleTop = true
        }
    }

    private fun cleanupUploadPaths(app: XerahSApplication, uploadPaths: List<String>) {
        val cacheRoot = app.cacheDir.canonicalPath
        uploadPaths.forEach { path ->
            runCatching {
                val file = File(path)
                if (file.exists() && file.parentFile?.canonicalPath == cacheRoot) {
                    file.delete()
                }
            }
        }
    }

    private fun downloadRemoteSxcu(initialUrl: HttpUrl): ByteArray {
        var currentUrl = initialUrl
        repeat(MAX_REMOTE_SXCU_REDIRECTS + 1) { redirectCount ->
            val request = Request.Builder().url(currentUrl).get().build()
            remoteImportClient.newCall(request).execute().use { response ->
                if (response.isRedirect) {
                    if (redirectCount >= MAX_REMOTE_SXCU_REDIRECTS) {
                        throw IllegalStateException("Too many redirects while downloading .sxcu.")
                    }
                    val location = response.header("Location")
                        ?: throw IllegalStateException("Remote .sxcu redirect did not include a Location header.")
                    val redirected = currentUrl.resolve(location)
                        ?: throw IllegalStateException("Remote .sxcu redirected to an invalid URL.")
                    if (redirected.scheme != "https") {
                        throw IllegalStateException("Remote .sxcu redirects must stay on HTTPS.")
                    }
                    currentUrl = redirected
                    return@use
                }

                if (!response.isSuccessful) {
                    throw IllegalStateException("Failed to download .sxcu: HTTP ${response.code}.")
                }
                val contentType = response.body?.contentType()?.let { "${it.type}/${it.subtype}".lowercase() }
                if (contentType != null && contentType !in setOf("application/json", "application/x-sxcu+json", "text/plain")) {
                    throw IllegalStateException("Remote .sxcu has unsupported content type: $contentType.")
                }
                val contentLength = response.body?.contentLength() ?: -1L
                if (contentLength > MAX_REMOTE_SXCU_BYTES) {
                    throw IllegalStateException("Remote .sxcu is larger than 1 MB.")
                }
                val body = response.body ?: throw IllegalStateException("Remote .sxcu response was empty.")
                body.byteStream().use { input ->
                    val output = java.io.ByteArrayOutputStream()
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    var copied = 0L
                    while (true) {
                        val read = input.read(buffer)
                        if (read < 0) break
                        copied += read
                        if (copied > MAX_REMOTE_SXCU_BYTES) {
                            throw IllegalStateException("Remote .sxcu is larger than 1 MB.")
                        }
                        output.write(buffer, 0, read)
                    }
                    return output.toByteArray()
                }
            }
        }
        throw IllegalStateException("Too many redirects while downloading .sxcu.")
    }

    private fun confirmRemoteCustomUploaderImport(
        sourceUrl: String,
        preview: CustomUploaderImportPreview,
        onImport: () -> Unit
    ) {
        val requestHost = preview.requestUrl.toHttpUrlOrNull()?.host ?: preview.requestUrl.substringAfter("://", preview.requestUrl).substringBefore('/')
        val message = buildString {
            appendLine("Source: $sourceUrl")
            appendLine("Uploader: ${preview.displayName}")
            appendLine("Request domain: $requestHost")
            appendLine("Method: ${preview.requestMethod}")
            appendLine("Headers: ${if (preview.hasHeaders) "configured" else "none"}")
            appendLine("Parameters: ${if (preview.hasParameters) "configured" else "none"}")
            appendLine("Body values: ${if (preview.hasBodyData) "configured" else "none"}")
            appendLine("Can send files: ${if (preview.canSendFiles) "yes" else "no"}")
            appendLine("Can send text/URLs: ${if (preview.canSendTextOrUrls) "yes" else "no"}")
            appendLine()
            append("Imported uploaders can transmit user files to third-party endpoints.")
        }
        AlertDialog.Builder(this)
            .setTitle("Import custom uploader?")
            .setMessage(message)
            .setNegativeButton("Cancel", null)
            .setPositiveButton("Import", null)
            .create()
            .apply {
                setOnShowListener {
                    getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener {
                        dismiss()
                        onImport()
                    }
                }
            }
            .show()
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
