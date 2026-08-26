//
//  UploadQueueItem.swift
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

/// One item in the persistent upload queue. Matches C# UploadQueueItem for JSON compatibility.
struct UploadQueueItem: Codable, Equatable {
    let filePath: String
    let enqueuedUtc: String  // ISO 8601
    /// Stable Cloud idempotency key. Older queue snapshots derive one when decoded.
    let clientItemId: UUID

    init(filePath: String, enqueuedUtc: String, clientItemId: UUID = UUID()) {
        self.filePath = filePath
        self.enqueuedUtc = enqueuedUtc
        self.clientItemId = clientItemId
    }

    private enum CodingKeys: String, CodingKey { case filePath, enqueuedUtc, clientItemId }

    init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        filePath = try values.decode(String.self, forKey: .filePath)
        enqueuedUtc = try values.decode(String.self, forKey: .enqueuedUtc)
        clientItemId = try values.decodeIfPresent(UUID.self, forKey: .clientItemId) ?? UUID()
    }
}
