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

import java.net.URI
import java.net.URLDecoder

internal data class ParsedCloudOAuthCallback(val code: String?, val state: String, val error: String?)

internal object CloudOAuthCallbackParser {
    private val allowedError = Regex("^[A-Za-z0-9_]{1,128}$")
    private val forbidden = setOf("access_token", "refresh_token", "id_token")

    fun parse(value: String): ParsedCloudOAuthCallback {
        val uri = runCatching { URI(value) }.getOrElse {
            throw CloudSecurityException("The XerahS Cloud OAuth callback was invalid.")
        }
        if (!uri.scheme.equals("xerahs", ignoreCase = true) ||
            !uri.host.equals("oauth", ignoreCase = true) ||
            uri.path != "/callback" || uri.fragment != null || uri.userInfo != null) {
            throw CloudSecurityException("The XerahS Cloud OAuth callback was invalid.")
        }

        val components = uri.rawQuery?.split('&')?.filter { it.isNotEmpty() }.orEmpty()
        if (components.size != 2) throw CloudSecurityException("The XerahS Cloud OAuth callback was invalid.")
        val values = linkedMapOf<String, String>()
        for (component in components) {
            val pair = component.split('=', limit = 2)
            val key = decode(pair[0])
            val item = decode(pair.getOrElse(1) { "" })
            if (key.lowercase() in forbidden || values.put(key, item) != null) {
                throw CloudSecurityException("The XerahS Cloud OAuth callback contained duplicate or forbidden parameters.")
            }
        }

        val state = values["state"]?.takeIf { it.isNotBlank() }
            ?: throw CloudSecurityException("The XerahS Cloud OAuth callback did not contain one state value.")
        val code = values["code"]?.takeIf { it.isNotBlank() }
        val error = values["error"]?.takeIf { allowedError.matches(it) }
        if ((code == null) == (error == null) || values.keys != setOf("state", if (code != null) "code" else "error")) {
            throw CloudSecurityException("The XerahS Cloud OAuth callback must contain exactly one code or error value.")
        }
        return ParsedCloudOAuthCallback(code, state, error)
    }

    private fun decode(value: String): String = try {
        URLDecoder.decode(value, Charsets.UTF_8.name())
    } catch (error: Exception) {
        throw CloudSecurityException("The XerahS Cloud OAuth callback encoding was invalid.", error)
    }
}
