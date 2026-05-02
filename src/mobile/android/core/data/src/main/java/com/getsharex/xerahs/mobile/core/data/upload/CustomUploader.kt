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

package com.getsharex.xerahs.mobile.core.data.upload

import android.util.Base64
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderBodyType
import com.getsharex.xerahs.mobile.core.domain.CustomUploaderEntry
import okhttp3.FormBody
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.net.URLEncoder
import java.time.Instant
import java.util.concurrent.TimeUnit

/**
 * Upload a file using an .sxcu-compatible definition.
 */
class CustomUploader(
    private val client: OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .writeTimeout(60, TimeUnit.SECONDS)
        .readTimeout(60, TimeUnit.SECONDS)
        .build()
) {
    private val supportedFunctions = setOf(
        "base64",
        "filename",
        "header",
        "input",
        "json",
        "regex",
        "response",
        "responseurl"
    )

    fun uploadFile(filePath: String, entry: CustomUploaderEntry): UploadOutcome {
        if (entry.requestUrl.isBlank()) return UploadOutcome.Failure("Request URL is empty")
        if (!entry.requestUrl.trim().startsWith("https://", ignoreCase = true)) {
            return UploadOutcome.Failure(
                "HTTP upload endpoints are not supported because uploads may contain private user files or credentials. Use HTTPS."
            )
        }
        val file = File(filePath)
        if (!file.exists()) return UploadOutcome.Failure("File not found")

        val unsupported = findUnsupportedFunctions(entry)
        if (unsupported.isNotEmpty()) {
            return UploadOutcome.Failure(
                "Unsupported .sxcu function(s) on Android: ${unsupported.sorted().joinToString(", ")}",
                "Supported functions: ${supportedFunctions.sorted().joinToString(", ")}"
            )
        }

        return try {
            val request = buildRequest(entry, file) ?: return UploadOutcome.Failure("Invalid RequestURL")
            val response = client.newCall(request).execute()
            val responseBody = response.body?.string().orEmpty()
            val responseContext = TemplateContext(
                input = request.tag(String::class.java).orEmpty(),
                fileName = UploadFileNameGenerator.uploadFileName(file.absolutePath),
                responseText = responseBody,
                responseUrl = response.request.url.toString(),
                responseHeaders = response.headers.names().associate { it.lowercase() to response.header(it).orEmpty() }
            )

            if (!response.isSuccessful) {
                val parsedError = renderTemplate(entry.errorMessage, responseContext).trim()
                val message = parsedError.ifBlank { "HTTP ${response.code}: ${responseBody.take(200)}" }
                return UploadOutcome.Failure(message, makeFailureDetails(entry, file, request, response.code, responseBody, null))
            }

            val resolvedUrl = resolveSuccessUrl(entry, responseContext)
            if (resolvedUrl.isBlank()) {
                UploadOutcome.Failure("No URL in response", makeFailureDetails(entry, file, request, response.code, responseBody, null))
            } else {
                UploadOutcome.Success(resolvedUrl)
            }
        } catch (e: Exception) {
            UploadOutcome.Failure(e.message ?: "Upload failed", makeFailureDetails(entry, file, null, null, null, e))
        }
    }

    private fun buildRequest(entry: CustomUploaderEntry, file: File): Request? {
        val uploadFileName = UploadFileNameGenerator.uploadFileName(file.absolutePath)
        val bodyInput = if (requiresEncodedInput(entry.bodyType)) {
            Base64.encodeToString(file.readBytes(), Base64.NO_WRAP)
        } else {
            ""
        }
        val context = TemplateContext(input = bodyInput, fileName = uploadFileName)
        val url = buildRequestUrl(entry, context) ?: return null
        val renderedArguments = renderDictionary(entry.mergedArgumentsForExecution(), context)

        val body = when (entry.bodyType) {
            CustomUploaderBodyType.None -> null
            CustomUploaderBodyType.MultipartFormData -> multipartBody(entry, file, uploadFileName, renderedArguments)
            CustomUploaderBodyType.Binary -> file.asRequestBody("application/octet-stream".toMediaType())
            CustomUploaderBodyType.FormURLEncoded -> formBody(renderedArguments)
            CustomUploaderBodyType.JSON -> renderBody(entry.data, context, entry.bodyType)
                .toRequestBody("application/json".toMediaType())
            CustomUploaderBodyType.XML -> renderBody(entry.data, context, entry.bodyType)
                .toRequestBody("application/xml".toMediaType())
        }

        val builder = Request.Builder().url(url)
        renderDictionary(entry.headers, context).forEach { (key, value) ->
            if (value.isNotBlank()) builder.addHeader(key, value)
        }

        val method = entry.requestMethod.name
        if (method == "GET" || method == "HEAD") {
            builder.method(method, null)
        } else {
            builder.method(method, body ?: ByteArray(0).toRequestBody(null))
        }
        builder.tag(String::class.java, bodyInput)
        return builder.build()
    }

    private fun multipartBody(
        entry: CustomUploaderEntry,
        file: File,
        uploadFileName: String,
        arguments: Map<String, String>
    ): RequestBody {
        val builder = MultipartBody.Builder().setType(MultipartBody.FORM)
        arguments.keys.sorted().forEach { key ->
            builder.addFormDataPart(key, arguments[key].orEmpty())
        }
        builder.addFormDataPart(
            entry.fileFormName.ifBlank { "file" },
            uploadFileName,
            file.asRequestBody("application/octet-stream".toMediaType())
        )
        return builder.build()
    }

    private fun formBody(arguments: Map<String, String>): RequestBody {
        val builder = FormBody.Builder()
        arguments.keys.sorted().forEach { key -> builder.add(key, arguments[key].orEmpty()) }
        return builder.build()
    }

    private fun buildRequestUrl(entry: CustomUploaderEntry, context: TemplateContext): String? {
        val rendered = renderTemplate(entry.requestUrl, context, urlEncodeInput = true)
        val builder = rendered.toHttpUrlOrNull()?.newBuilder() ?: return null
        if (builder.build().scheme != "https") return null
        renderDictionary(entry.parameters, context).forEach { (key, value) ->
            builder.addQueryParameter(key, value)
        }
        return builder.build().toString()
    }

    private fun resolveSuccessUrl(entry: CustomUploaderEntry, context: TemplateContext): String {
        val rendered = renderTemplate(entry.url, context).trim()
        if (rendered.isNotBlank()) return rendered

        val legacy = extractRegexMatch(context.responseText.orEmpty(), entry.urlExpression, null)
        if (!legacy.isNullOrBlank()) return legacy

        return context.responseText.orEmpty().trim().take(500)
    }

    private fun renderDictionary(source: Map<String, String>, context: TemplateContext): Map<String, String> =
        source.keys.sorted().associateWith { key -> renderTemplate(source[key].orEmpty(), context) }

    private fun renderBody(template: String, context: TemplateContext, mode: CustomUploaderBodyType): String {
        val adjusted = context.copy(
            input = encodeBodyInput(context.input, mode),
            fileName = encodeBodyInput(context.fileName, mode)
        )
        return renderTemplate(template, adjusted)
    }

    private fun requiresEncodedInput(bodyType: CustomUploaderBodyType): Boolean =
        bodyType == CustomUploaderBodyType.FormURLEncoded ||
            bodyType == CustomUploaderBodyType.JSON ||
            bodyType == CustomUploaderBodyType.XML

    private fun encodeBodyInput(value: String, mode: CustomUploaderBodyType): String =
        when (mode) {
            CustomUploaderBodyType.JSON -> jsonEscaped(value)
            CustomUploaderBodyType.XML -> xmlEscaped(value)
            else -> value
        }

    private fun renderTemplate(template: String, context: TemplateContext, urlEncodeInput: Boolean = false): String {
        if (template.isBlank()) return ""
        val regex = Regex("""\{([A-Za-z][^{}]*)}""")
        var result = template
        repeat(12) {
            val updated = regex.replace(result) { match ->
                evaluateToken(match.groupValues[1], context, urlEncodeInput)
            }
            if (updated == result) return result
            result = updated
        }
        return result
    }

    private fun evaluateToken(token: String, context: TemplateContext, urlEncodeInput: Boolean): String {
        val parts = token.split(":", limit = 2)
        val function = parts[0].lowercase()
        val arguments = if (parts.size > 1) parts[1].split("|") else emptyList()

        return when (function) {
            "input" -> if (urlEncodeInput) strictUrlEncode(context.input) else context.input
            "filename" -> if (urlEncodeInput) strictUrlEncode(context.fileName) else context.fileName
            "response" -> context.responseText.orEmpty()
            "responseurl" -> context.responseUrl.orEmpty()
            "header" -> context.responseHeaders[arguments.firstOrNull()?.lowercase().orEmpty()].orEmpty()
            "json" -> {
                val input = if (arguments.size > 1) arguments[0] else context.responseText.orEmpty()
                val path = if (arguments.size > 1) arguments[1] else arguments.firstOrNull().orEmpty()
                jsonValue(input, path)
            }
            "regex" -> when {
                arguments.size > 2 -> extractRegexMatch(arguments[0], arguments[1], arguments[2]).orEmpty()
                arguments.size > 1 -> extractRegexMatch(context.responseText.orEmpty(), arguments[0], arguments[1]).orEmpty()
                else -> extractRegexMatch(context.responseText.orEmpty(), arguments.firstOrNull().orEmpty(), null).orEmpty()
            }
            "base64" -> Base64.encodeToString(arguments.firstOrNull().orEmpty().toByteArray(), Base64.NO_WRAP)
            else -> "{$token}"
        }
    }

    private fun extractRegexMatch(input: String, expression: String, group: String?): String? {
        if (input.isBlank() || expression.isBlank()) return null
        val match = runCatching { Regex(expression).find(input) }.getOrNull() ?: return null
        if (!group.isNullOrBlank()) {
            val index = group.toIntOrNull() ?: return null
            return if (index < match.groups.size) match.groups[index]?.value else null
        }
        return if (match.groups.size > 1) match.groups[1]?.value ?: match.value else match.value
    }

    private fun jsonValue(input: String, path: String): String {
        if (input.isBlank() || path.isBlank()) return ""
        val root = runCatching {
            val trimmed = input.trim()
            if (trimmed.startsWith("[")) JSONArray(trimmed) else JSONObject(trimmed)
        }.getOrNull() ?: return ""
        val normalized = path.removePrefix("$.")
        if (normalized.isBlank()) return ""

        var current: Any? = root
        normalized.split('.').forEach { component ->
            current = descendJson(current, component) ?: return ""
        }
        return stringifyJsonValue(current)
    }

    private fun descendJson(value: Any?, component: String): Any? {
        var current = value
        val parts = Regex("""[^\[\]]+|\[\d+]""").findAll(component).map { it.value }.toList()
        if (parts.isEmpty()) return null
        parts.forEach { part ->
            current = if (part.startsWith("[") && part.endsWith("]")) {
                val index = part.drop(1).dropLast(1).toIntOrNull() ?: return null
                (current as? JSONArray)?.opt(index)
            } else {
                (current as? JSONObject)?.opt(part)
            }
        }
        return current
    }

    private fun stringifyJsonValue(value: Any?): String =
        when (value) {
            null, JSONObject.NULL -> ""
            is JSONObject, is JSONArray -> value.toString()
            else -> value.toString()
        }

    private fun findUnsupportedFunctions(entry: CustomUploaderEntry): Set<String> {
        val templates = listOf(
            entry.requestUrl,
            entry.data,
            entry.url,
            entry.thumbnailUrl,
            entry.deletionUrl,
            entry.errorMessage,
            entry.urlExpression
        ) + entry.parameters.values + entry.headers.values + entry.arguments.values

        val regex = Regex("""\{([A-Za-z][^:{}|}]*)""")
        return templates
            .filter { it.isNotBlank() }
            .flatMap { template -> regex.findAll(template).map { it.groupValues[1].lowercase() } }
            .filterNot { it in supportedFunctions }
            .toSet()
    }

    private fun strictUrlEncode(value: String): String =
        URLEncoder.encode(value, "UTF-8")
            .replace("+", "%20")
            .replace("%7E", "~")

    private fun jsonEscaped(value: String): String =
        value
            .replace("\\", "\\\\")
            .replace("\"", "\\\"")
            .replace("\n", "\\n")
            .replace("\r", "\\r")
            .replace("\t", "\\t")

    private fun xmlEscaped(value: String): String =
        value
            .replace("&", "&amp;")
            .replace("<", "&lt;")
            .replace(">", "&gt;")
            .replace("\"", "&quot;")
            .replace("'", "&apos;")

    private fun makeFailureDetails(
        entry: CustomUploaderEntry,
        file: File,
        request: Request?,
        responseCode: Int?,
        responseBody: String?,
        error: Exception?
    ): String {
        val requestHeaders = request?.headers?.toMultimap()?.mapValues { (key, values) ->
            if (isSensitiveName(key)) "<redacted>" else sanitizeText(values.joinToString(", "))
        }.orEmpty()
        return buildString {
            appendLine("Request:")
            appendLine("Uploader: Custom Uploader (.sxcu)")
            appendLine("Timestamp: ${Instant.now()}")
            appendLine("File Name: ${file.name}")
            appendLine("Upload Name: ${UploadFileNameGenerator.uploadFileName(file.absolutePath)}")
            appendLine("Request Method: ${entry.requestMethod.name}")
            appendLine("Request URL: ${redactUrl(request?.url?.toString() ?: entry.requestUrl)}")
            appendLine("Destination Type: ${entry.destinationType}")
            appendLine("Body Type: ${entry.bodyType.name}")
            appendLine("File Form Name: ${entry.fileFormName}")
            appendLine("Configured Parameters: ${entry.parameters.size}")
            appendLine("Configured Headers: ${entry.headers.size}")
            appendLine("Configured Arguments: ${entry.arguments.size}")
            if (requestHeaders.isNotEmpty()) {
                appendLine()
                appendLine("Request Headers:")
                requestHeaders.keys.sorted().forEach { key -> appendLine("$key: ${requestHeaders[key]}") }
            }
            if (responseCode != null) {
                appendLine()
                appendLine("Response:")
                appendLine("HTTP: $responseCode")
            }
            if (!responseBody.isNullOrBlank()) {
                appendLine()
                appendLine("Response Body:")
                appendLine(sanitizeText(responseBody).take(1200))
            }
            if (error != null) {
                appendLine()
                appendLine("Exception:")
                appendLine(error::class.java.name)
                appendLine(sanitizeText(error.message.orEmpty()))
            }
        }.trim()
    }

    private fun redactUrl(value: String): String {
        val parsed = value.toHttpUrlOrNull() ?: return sanitizeText(value).take(1024)
        val builder = parsed.newBuilder().query(null)
        parsed.queryParameterNames.forEach { name ->
            parsed.queryParameterValues(name).forEach { parameterValue ->
                builder.addQueryParameter(
                    name,
                    if (isSensitiveName(name)) "<redacted>" else sanitizeText(parameterValue.orEmpty())
                )
            }
        }
        return builder.build().toString().take(1024)
    }

    private fun sanitizeText(value: String): String =
        value
            .replace(Regex("""(?i)(authorization|cookie|x-api-key|api[_-]?key|access[_-]?token|token|secret|password)=([^&\s]+)""")) {
                "${it.groupValues[1]}=<redacted>"
            }
            .replace(Regex("""(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]+"""), "$1<redacted>")
            .take(2048)

    private fun isSensitiveName(name: String): Boolean {
        val normalized = name.lowercase()
            .replace("-", "")
            .replace("_", "")
            .replace(" ", "")
        return normalized == "authorization" ||
            normalized == "cookie" ||
            normalized == "xapikey" ||
            listOf("apikey", "accesstoken", "token", "secret", "password").any { normalized.contains(it) }
    }
}

private data class TemplateContext(
    val input: String,
    val fileName: String,
    val responseText: String? = null,
    val responseUrl: String? = null,
    val responseHeaders: Map<String, String> = emptyMap()
)
