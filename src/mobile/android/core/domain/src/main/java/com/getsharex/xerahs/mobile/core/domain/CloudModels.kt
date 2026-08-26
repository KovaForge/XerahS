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

package com.getsharex.xerahs.mobile.core.domain

data class CloudAccount(
    val slug: String,
    val timeZone: String,
    val strongAuth: Boolean,
    val trialStatus: String,
    val trialEndsAt: String?,
    val subscriptionStatus: String?,
    val paidThrough: String?,
    val canPublish: Boolean,
    val disputeSuspended: Boolean
)

data class CloudGalleryItem(
    val id: String,
    val clientItemId: String,
    val url: String,
    val thumbnailUrl: String?,
    val kind: String,
    val fileName: String,
    val title: String,
    val capturedAt: String,
    val publishedAt: String,
    val host: String?,
    val contentType: String?
)

data class CloudGalleryPage(
    val items: List<CloudGalleryItem>,
    val nextCursor: String?
)

data class CloudPublishRequest(
    val clientItemId: String,
    val url: String,
    val thumbnailUrl: String? = null,
    val kind: String,
    val fileName: String,
    val capturedAt: String,
    val host: String?,
    val contentType: String?
)

data class CloudConnectionState(
    val restoring: Boolean = false,
    val account: CloudAccount? = null,
    val error: String? = null
) {
    val isSignedIn: Boolean get() = account != null
}
