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

import android.content.Intent
import android.net.Uri
import android.provider.OpenableColumns
import android.util.Log
import android.webkit.MimeTypeMap
import java.io.File
import java.util.UUID

private const val TAG = "ShareIntentHandler"
private const val MAX_SHARED_BYTES = 512L * 1024L * 1024L

object ShareIntentHandler {

    fun handleIntent(activity: MainActivity, intent: Intent?): Array<String>? {
        if (intent == null) return null
        val action = intent.action
        if (action != Intent.ACTION_SEND && action != Intent.ACTION_SEND_MULTIPLE && action != Intent.ACTION_VIEW) return null
        // Allow type to be null; many share senders (e.g. Photos) set data in ClipData only
        val type = intent.type
        if (type.isNullOrEmpty() && intent.clipData == null && intent.data == null) return null

        val app = activity.application as? XerahSApplication ?: return null
        val cacheDir = app.cacheDir ?: return null
        val localPaths = mutableListOf<String>()

        when (action) {
            Intent.ACTION_VIEW -> {
                intent.data?.let { uri ->
                    copyUriToCache(activity, uri, cacheDir, type)?.let { localPaths.add(it) }
                        ?: Log.w(TAG, "copyUriToCache failed for $uri")
                }
            }
            Intent.ACTION_SEND -> {
                @Suppress("DEPRECATION")
                var uri: Uri? = intent.getParcelableExtra(Intent.EXTRA_STREAM)
                if (uri == null && intent.clipData != null && intent.clipData!!.itemCount > 0) {
                    uri = intent.clipData!!.getItemAt(0).uri
                }
                if (uri != null) {
                    copyUriToCache(activity, uri, cacheDir, type)?.let { localPaths.add(it) }
                        ?: Log.w(TAG, "copyUriToCache failed for $uri")
                } else {
                    intent.getStringExtra(Intent.EXTRA_TEXT)?.let { text ->
                        writeTextToCache(text, cacheDir, type)?.let { localPaths.add(it) }
                    } ?: Log.w(TAG, "ACTION_SEND: no URI in EXTRA_STREAM or clipData")
                }
            }
            Intent.ACTION_SEND_MULTIPLE -> {
                @Suppress("DEPRECATION")
                var uris = intent.getParcelableArrayListExtra<Uri>(Intent.EXTRA_STREAM)
                if (uris.isNullOrEmpty() && intent.clipData != null) {
                    uris = ArrayList((0 until intent.clipData!!.itemCount).map { intent.clipData!!.getItemAt(it).uri })
                }
                uris?.forEach { uri ->
                    copyUriToCache(activity, uri, cacheDir, type)?.let { localPaths.add(it) }
                        ?: Log.w(TAG, "copyUriToCache failed for $uri")
                }
                if (uris.isNullOrEmpty()) Log.w(TAG, "ACTION_SEND_MULTIPLE: no URIs in EXTRA_STREAM or clipData")
            }
        }

        return if (localPaths.isEmpty()) null else localPaths.toTypedArray()
    }

    private fun copyUriToCache(activity: MainActivity, uri: Uri, cacheDir: File, mimeType: String? = null): String? {
        return try {
            if (!uri.scheme.equals("content", ignoreCase = true)) return null
            val resolvedMimeType = activity.contentResolver.getType(uri) ?: mimeType
            if (!isSupportedMimeType(resolvedMimeType, uri)) return null
            val size = contentSize(activity, uri)
            if (size != null && size > MAX_SHARED_BYTES) return null

            var fileName = getFileNameFromUri(activity, uri)
            if (fileName.isNullOrBlank()) {
                val ext = extensionFromMimeType(activity, uri, resolvedMimeType)
                fileName = "share_${UUID.randomUUID().toString().take(8)}${if (ext != null) ".$ext" else ""}"
            }
            val cachePath = uniqueCacheFile(cacheDir, fileName)
            activity.contentResolver.openInputStream(uri)?.use { input ->
                cachePath.outputStream().use { output ->
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    var copied = 0L
                    while (true) {
                        val read = input.read(buffer)
                        if (read < 0) break
                        copied += read
                        if (copied > MAX_SHARED_BYTES) {
                            cachePath.delete()
                            return null
                        }
                        output.write(buffer, 0, read)
                    }
                }
            } ?: return null
            cachePath.absolutePath
        } catch (e: Exception) {
            null
        }
    }

    private fun writeTextToCache(text: String, cacheDir: File, mimeType: String? = null): String? {
        if (text.isBlank()) return null
        if (!isSupportedMimeType(mimeType, null)) return null
        if (text.toByteArray(Charsets.UTF_8).size > MAX_SHARED_BYTES) return null
        val ext = extensionFromMimeType(null, null, mimeType) ?: "txt"
        val cachePath = uniqueCacheFile(cacheDir, "shared_${UUID.randomUUID().toString().take(8)}.$ext")
        return try {
            cachePath.writeText(text)
            cachePath.absolutePath
        } catch (e: Exception) {
            null
        }
    }

    @Suppress("DEPRECATION")
    private fun getFileNameFromUri(activity: MainActivity, uri: Uri): String? {
        if (uri.scheme != "content") return uri.lastPathSegment?.substringAfterLast('/')
        activity.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) {
                val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                if (nameIndex >= 0) return cursor.getString(nameIndex)
            }
        }
        return uri.lastPathSegment?.substringAfterLast('/')
    }

    private fun extensionFromMimeType(activity: MainActivity?, uri: Uri?, mimeType: String?): String? {
        if (activity != null && uri != null) {
            activity.contentResolver.getType(uri)?.let { resolved ->
                MimeTypeMap.getSingleton().getExtensionFromMimeType(resolved)?.let { return it }
            }
        }
        if (mimeType.isNullOrBlank()) return null
        return when (mimeType) {
            "application/x-sxcu+json" -> "sxcu"
            "application/x-xsdc+json" -> "xsdc"
            "application/pdf" -> "pdf"
            "video/mp4", "video/x-m4v" -> "mp4"
            "video/3gpp" -> "3gp"
            "audio/mpeg" -> "mp3"
            "audio/mp4", "audio/x-m4a" -> "m4a"
            "audio/wav", "audio/x-wav" -> "wav"
            "audio/ogg" -> "ogg"
            "audio/webm" -> "weba"
            "image/jpeg" -> "jpg"
            "image/png" -> "png"
            "image/gif" -> "gif"
            "image/webp" -> "webp"
            "text/plain" -> "txt"
            "text/html" -> "html"
            else -> mimeType.substringAfterLast('/').takeIf { it != "*" && it.isNotBlank() }?.take(4)
        }
    }

    private fun isSupportedMimeType(mimeType: String?, uri: Uri?): Boolean {
        val normalized = mimeType?.substringBefore(';')?.trim()?.lowercase()
        if (normalized == null) {
            val ext = uri?.lastPathSegment?.substringAfterLast('.', "")?.lowercase().orEmpty()
            return ext in setOf("sxcu", "xsdc")
        }
        return normalized == "text/plain" ||
            normalized == "application/json" ||
            normalized == "application/x-sxcu+json" ||
            normalized == "application/x-xsdc+json" ||
            normalized.startsWith("image/") ||
            normalized.startsWith("video/") ||
            normalized.startsWith("audio/")
    }

    private fun contentSize(activity: MainActivity, uri: Uri): Long? {
        activity.contentResolver.query(uri, arrayOf(OpenableColumns.SIZE), null, null, null)?.use { cursor ->
            if (cursor.moveToFirst()) {
                val sizeIndex = cursor.getColumnIndex(OpenableColumns.SIZE)
                if (sizeIndex >= 0 && !cursor.isNull(sizeIndex)) return cursor.getLong(sizeIndex)
            }
        }
        return null
    }

    private fun uniqueCacheFile(cacheDir: File, rawName: String): File {
        val sanitized = rawName
            .substringAfterLast('/')
            .substringAfterLast('\\')
            .replace(Regex("[\\r\\n\\t]"), "_")
            .ifBlank { "shared" }
        val file = File(cacheDir, sanitized)
        if (!file.exists()) return file

        val base = file.nameWithoutExtension.ifBlank { "shared" }
        val ext = file.extension
        val suffix = UUID.randomUUID().toString().take(8)
        val uniqueName = if (ext.isBlank()) "${base}_$suffix" else "${base}_$suffix.$ext"
        return File(cacheDir, uniqueName)
    }
}
