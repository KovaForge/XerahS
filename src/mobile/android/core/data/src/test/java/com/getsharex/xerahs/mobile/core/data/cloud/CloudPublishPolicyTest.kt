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

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.assertThrows
import org.junit.Test

class CloudPublishPolicyTest {
    @Test
    fun `image and video uploads map to server contract kinds`() {
        assertEquals("screenshot" to "image/png", CloudPublishPolicy.eligibleMedia("capture.png", "/cache/capture.png"))
        assertEquals("screencast" to "video/mp4", CloudPublishPolicy.eligibleMedia("recording.mp4", "/cache/recording.mp4"))
    }

    @Test
    fun `non media upload remains local only`() {
        assertNull(CloudPublishPolicy.eligibleMedia("notes.txt", "/cache/notes.txt"))
    }

    @Test
    fun `publish destination must be credential free https`() {
        assertTrue(CloudPublishPolicy.isCredentialFreeHttps("https://cdn.example.test/image.png"))
        assertFalse(CloudPublishPolicy.isCredentialFreeHttps("http://cdn.example.test/image.png"))
        assertFalse(CloudPublishPolicy.isCredentialFreeHttps("https://user:secret@cdn.example.test/image.png"))
    }

    @Test
    fun `cloud retry remains bound to original account`() {
        assertTrue(CloudPublishPolicy.canRetryForOwner("owner-a", "owner-a"))
        assertFalse(CloudPublishPolicy.canRetryForOwner("owner-a", "owner-b"))
        assertFalse(CloudPublishPolicy.canRetryForOwner("owner-a", null))
    }

    @Test
    fun `authenticated publish rejects account switch during session acquisition`() {
        assertTrue(CloudOwnerBinding.matchesExpected("owner-a", "owner-a"))
        assertFalse(CloudOwnerBinding.matchesExpected("owner-b", "owner-a"))
        assertTrue(CloudOwnerBinding.matchesExpected("owner-b", null))
    }

    @Test
    fun `access token expiry matches desktop one-hour policy`() {
        assertTrue(CloudTokenPolicy.hasAcceptedMaximumExpiry(4_660, 1_000))
        assertFalse(CloudTokenPolicy.hasAcceptedMaximumExpiry(4_661, 1_000))
    }

    @Test
    fun `oauth callback accepts one code and state`() {
        val callback = CloudOAuthCallbackParser.parse("xerahs://oauth/callback?code=abc&state=expected")
        assertEquals("abc", callback.code)
        assertEquals("expected", callback.state)
        assertNull(callback.error)
    }

    @Test
    fun `oauth callback rejects duplicate and forbidden parameters`() {
        assertThrows(CloudSecurityException::class.java) {
            CloudOAuthCallbackParser.parse("xerahs://oauth/callback?code=abc&state=one&state=two")
        }
        assertThrows(CloudSecurityException::class.java) {
            CloudOAuthCallbackParser.parse("xerahs://oauth/callback?access_token=secret&state=expected")
        }
        assertThrows(CloudSecurityException::class.java) {
            CloudOAuthCallbackParser.parse("xerahs://oauth/callback?code=abc&code=def")
        }
    }
}
