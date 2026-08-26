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

import java.net.URLConnection

internal object CloudPublishPolicy {
    fun eligibleMedia(fileName: String, sourcePath: String): Pair<String, String>? {
        val contentType = URLConnection.guessContentTypeFromName(sourcePath)
            ?: URLConnection.guessContentTypeFromName(fileName)
            ?: return null
        return when {
            contentType.startsWith("image/") -> "screenshot" to contentType
            contentType.startsWith("video/") -> "screencast" to contentType
            else -> null
        }
    }

    fun isCredentialFreeHttps(url: String): Boolean = runCatching {
        val parsed = java.net.URI(url)
        parsed.scheme == "https" && !parsed.host.isNullOrBlank() && parsed.userInfo == null
    }.getOrDefault(false)

    fun canRetryForOwner(storedOwner: String, currentOwner: String?): Boolean =
        storedOwner.isNotBlank() && currentOwner != null && storedOwner == currentOwner
}

internal object CloudTokenPolicy {
    fun hasAcceptedMaximumExpiry(expiresAtEpochSeconds: Long, nowEpochSeconds: Long): Boolean =
        expiresAtEpochSeconds <= nowEpochSeconds + 3660
}

internal object CloudOwnerBinding {
    fun matchesExpected(actualOwner: String, expectedOwner: String?): Boolean =
        expectedOwner == null || (expectedOwner.isNotBlank() && actualOwner == expectedOwner)
}
