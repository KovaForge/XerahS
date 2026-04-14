//
//  UploadOutcome.swift
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

struct UploadFailure: Equatable {
    let message: String
    let details: String

    init(message: String, details: String? = nil) {
        self.message = message.trimmingCharacters(in: .whitespacesAndNewlines)
        self.details = (details ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var clipboardText: String {
        guard !details.isEmpty, details != message else {
            return message
        }

        return "\(message)\n\n\(details)"
    }
}

enum UploadOutcome {
    case success(url: String)
    case failure(UploadFailure)
}

enum UploadDebugTools {
    static func formatKeyValueLines(_ values: [(String, String?)]) -> String {
        values.compactMap { key, value in
            guard let trimmed = trimmed(value) else { return nil }
            return "\(key): \(trimmed)"
        }
        .joined(separator: "\n")
    }

    static func formatSections(_ sections: [(String, String?)]) -> String {
        sections.compactMap { title, body in
            guard let trimmed = trimmed(body) else { return nil }
            return "\(title):\n\(trimmed)"
        }
        .joined(separator: "\n\n")
    }

    static func formatHeaders(_ headers: [String: String]) -> String? {
        guard !headers.isEmpty else { return nil }
        let lines = headers.keys.sorted().compactMap { key -> String? in
            guard let value = trimmed(headers[key]) else { return nil }
            return "\(key): \(value)"
        }
        return lines.isEmpty ? nil : lines.joined(separator: "\n")
    }

    static func sanitizedHeaders(_ headers: [String: String]) -> [String: String] {
        var sanitized: [String: String] = [:]

        for (key, value) in headers {
            let lowerKey = key.lowercased()
            if lowerKey.contains("authorization")
                || lowerKey.contains("secret")
                || lowerKey.contains("token")
                || lowerKey.contains("cookie")
                || lowerKey.contains("api-key")
                || lowerKey == "apikey"
            {
                sanitized[key] = "<redacted>"
            } else {
                sanitized[key] = truncate(value, limit: 256)
            }
        }

        return sanitized
    }

    static func responseBodySnippet(_ data: Data?, limit: Int = 1200) -> String? {
        guard let data, !data.isEmpty else { return nil }

        if let text = String(data: data, encoding: .utf8) ?? String(data: data, encoding: .ascii) {
            return truncate(text, limit: limit)
        }

        return "Non-text response body (\(data.count) bytes)"
    }

    static func describe(error: Error) -> String {
        describe(nsError: error as NSError)
    }

    static func httpHeaders(from response: HTTPURLResponse?) -> [String: String] {
        guard let response else { return [:] }

        var headers: [String: String] = [:]
        for (key, value) in response.allHeaderFields {
            headers[String(describing: key)] = truncate(String(describing: value), limit: 512)
        }
        return headers
    }

    private static func describe(nsError: NSError, indent: String = "") -> String {
        var lines: [String] = [
            "\(indent)Domain: \(nsError.domain)",
            "\(indent)Code: \(nsError.code)",
            "\(indent)Description: \(nsError.localizedDescription)"
        ]

        let interestingKeys: [String] = [
            NSURLErrorFailingURLStringErrorKey,
            NSDebugDescriptionErrorKey,
            NSLocalizedFailureReasonErrorKey,
            "NSErrorPeerAddressKey",
            "_NSURLErrorNWPathKey",
            "_kCFStreamErrorDomainKey",
            "_kCFStreamErrorCodeKey",
            "kCFStreamErrorDomainKey",
            "kCFStreamErrorCodeKey"
        ]

        for key in interestingKeys {
            if let value = nsError.userInfo[key] {
                lines.append("\(indent)\(key): \(truncate(String(describing: value), limit: 512))")
            }
        }

        if let failingURL = nsError.userInfo[NSURLErrorFailingURLErrorKey] as? URL {
            lines.append("\(indent)Failing URL: \(failingURL.absoluteString)")
        }

        if let underlying = nsError.userInfo[NSUnderlyingErrorKey] as? NSError {
            lines.append("\(indent)Underlying Error:")
            lines.append(describe(nsError: underlying, indent: indent + "  "))
        }

        return lines.joined(separator: "\n")
    }

    private static func trimmed(_ value: String?) -> String? {
        guard let value else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    private static func truncate(_ value: String, limit: Int) -> String {
        guard value.count > limit else { return value }
        return String(value.prefix(limit)) + "…"
    }
}
