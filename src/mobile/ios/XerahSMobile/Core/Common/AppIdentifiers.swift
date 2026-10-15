//
//  AppIdentifiers.swift
//  XerahS Mobile (Swift)
//
//  XerahS - The Avalonia UI implementation of ShareX
//  Copyright (c) 2007-2026 ShareX Team
//
//  This program is free software; you can redistribute it and/or
//  modify it under the terms of the GNU General Public License
//  as published by the Free Software Foundation; either version 2
//  of the License, or (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
//
//  Optionally you can also view the license at <http://www.gnu.org/licenses/>.
//

import Foundation

/// App Group and keychain identifiers shared by the app and the Share Extension.
///
/// xtool development builds are re-signed under `XTL-<id>.`-prefixed bundle IDs, and xtool registers
/// the App Group as `group.XTL-<id>.com.xerahs.xerahs`. Derive the identifiers from the running bundle
/// so those builds use the IDs they were provisioned with, while Xcode/App Store builds keep the real ones.
enum AppIdentifiers {
    private static let baseAppGroupId = "group.com.xerahs.xerahs"
    private static let baseKeychainAccessGroup = "7C5AS6VPUH.com.xerahs.xerahs.mobile"

    /// `XTL-<id>` when running an xtool-signed build; otherwise nil.
    static let xtoolPrefix: String? = {
        guard let first = Bundle.main.bundleIdentifier?.split(separator: ".").first,
              first.hasPrefix("XTL-") else { return nil }
        return String(first)
    }()

    static let appGroupId: String = {
        guard let xtoolPrefix else { return baseAppGroupId }
        return "group.\(xtoolPrefix).com.xerahs.xerahs"
    }()

    /// xtool gives the app and the extension separate keychain-access-groups, so share
    /// the keychain through the App Group, which iOS also accepts as an access group.
    static let keychainAccessGroup: String = xtoolPrefix == nil ? baseKeychainAccessGroup : appGroupId
}
