//
//  ApplicationConfig.swift
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

/// Minimal app config for mobile: default destination, S3, and custom uploaders.
/// Persisted as JSON in settings folder (ApplicationConfig.json).
struct ApplicationConfig: Codable {
    var defaultDestinationInstanceId: String?
    var s3Config: S3Config = S3Config()
    var customUploaders: [CustomUploaderEntry] = []
    /// Convert HEIC/HEIF images to PNG before upload (global; applies to S3 and custom uploaders). Default true.
    var convertHeicToPng: Bool = true
}

struct S3Config: Codable, Equatable {
    var accessKeyId: String = ""
    var secretAccessKey: String = ""
    var bucketName: String = ""
    var region: String = ""
    var customEndpoint: String = ""
    /// Force path-style S3 URLs: endpoint/bucket/key instead of bucket.endpoint/key.
    var usePathStyle: Bool = false
    /// Use custom domain (CDN) for result URLs.
    var useCustomDomain: Bool = false
    var customDomain: String = ""
    /// Sign request body; recommended when bucket blocks public ACLs. Default true.
    var signedPayload: Bool = true
    /// Set public-read ACL on uploaded objects. Default false.
    var setPublicAcl: Bool = false

    var isConfigured: Bool {
        !accessKeyId.isEmpty && !secretAccessKey.isEmpty && !bucketName.isEmpty && !region.isEmpty
    }
}

enum CustomUploaderRequestMethod: String, Codable, CaseIterable {
    case GET
    case POST
    case PUT
    case PATCH
    case DELETE
    case HEAD
}

enum CustomUploaderBodyType: String, Codable, CaseIterable {
    case none = "None"
    case multipartFormData = "MultipartFormData"
    case formUrlEncoded = "FormURLEncoded"
    case json = "JSON"
    case xml = "XML"
    case binary = "Binary"
}

struct SxcuDefinition: Codable, Equatable {
    var version: String = currentMobileVersion()
    var name: String = ""
    var destinationType: String = "FileUploader"
    var requestMethod: CustomUploaderRequestMethod = .POST
    var requestURL: String = ""
    var parameters: [String: String] = [:]
    var headers: [String: String] = [:]
    var body: CustomUploaderBodyType = .multipartFormData
    var arguments: [String: String] = [:]
    var fileFormName: String = "file"
    var data: String = ""
    var url: String = ""
    var thumbnailURL: String = ""
    var deletionURL: String = ""
    var errorMessage: String = ""

    enum CodingKeys: String, CodingKey {
        case version = "Version"
        case name = "Name"
        case destinationType = "DestinationType"
        case requestMethod = "RequestMethod"
        case requestURL = "RequestURL"
        case parameters = "Parameters"
        case headers = "Headers"
        case body = "Body"
        case arguments = "Arguments"
        case fileFormName = "FileFormName"
        case data = "Data"
        case url = "URL"
        case thumbnailURL = "ThumbnailURL"
        case deletionURL = "DeletionURL"
        case errorMessage = "ErrorMessage"
    }

    init(
        version: String = currentMobileVersion(),
        name: String = "",
        destinationType: String = "FileUploader",
        requestMethod: CustomUploaderRequestMethod = .POST,
        requestURL: String = "",
        parameters: [String: String] = [:],
        headers: [String: String] = [:],
        body: CustomUploaderBodyType = .multipartFormData,
        arguments: [String: String] = [:],
        fileFormName: String = "file",
        data: String = "",
        url: String = "",
        thumbnailURL: String = "",
        deletionURL: String = "",
        errorMessage: String = ""
    ) {
        self.version = version
        self.name = name
        self.destinationType = destinationType
        self.requestMethod = requestMethod
        self.requestURL = requestURL
        self.parameters = parameters
        self.headers = headers
        self.body = body
        self.arguments = arguments
        self.fileFormName = fileFormName
        self.data = data
        self.url = url
        self.thumbnailURL = thumbnailURL
        self.deletionURL = deletionURL
        self.errorMessage = errorMessage
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        version = try container.decodeIfPresent(String.self, forKey: .version) ?? currentMobileVersion()
        name = try container.decodeIfPresent(String.self, forKey: .name) ?? ""
        destinationType = try container.decodeIfPresent(String.self, forKey: .destinationType) ?? "FileUploader"
        requestMethod = try container.decodeIfPresent(CustomUploaderRequestMethod.self, forKey: .requestMethod) ?? .POST
        requestURL = try container.decodeIfPresent(String.self, forKey: .requestURL) ?? ""
        parameters = try container.decodeIfPresent([String: String].self, forKey: .parameters) ?? [:]
        headers = try container.decodeIfPresent([String: String].self, forKey: .headers) ?? [:]
        body = try container.decodeIfPresent(CustomUploaderBodyType.self, forKey: .body) ?? .multipartFormData
        arguments = try container.decodeIfPresent([String: String].self, forKey: .arguments) ?? [:]
        fileFormName = try container.decodeIfPresent(String.self, forKey: .fileFormName) ?? "file"
        data = try container.decodeIfPresent(String.self, forKey: .data) ?? ""
        url = try container.decodeIfPresent(String.self, forKey: .url) ?? ""
        thumbnailURL = try container.decodeIfPresent(String.self, forKey: .thumbnailURL) ?? ""
        deletionURL = try container.decodeIfPresent(String.self, forKey: .deletionURL) ?? ""
        errorMessage = try container.decodeIfPresent(String.self, forKey: .errorMessage) ?? ""
    }
}

/// One custom uploader definition stored in app config but shaped for .sxcu import/export.
struct CustomUploaderEntry: Codable, Equatable, Identifiable {
    var id: String = ""
    var version: String = currentMobileVersion()
    var name: String = ""
    var destinationType: String = "FileUploader"
    var requestMethod: CustomUploaderRequestMethod = .POST
    var requestUrl: String = ""
    var parameters: [String: String] = [:]
    var headers: [String: String] = [:]
    var bodyType: CustomUploaderBodyType = .multipartFormData
    var arguments: [String: String] = [:]
    var fileFormName: String = "file"
    var data: String = ""
    var url: String = ""
    var thumbnailUrl: String = ""
    var deletionUrl: String = ""
    var errorMessage: String = ""

    // Backward-compatibility with the earlier iOS-only custom uploader shape.
    var legacyMultipartBodyField: String = ""
    var legacyUrlExpression: String = ""

    enum CodingKeys: String, CodingKey {
        case id
        case version
        case name
        case destinationType
        case requestMethod
        case requestUrl
        case parameters
        case headers
        case bodyType
        case arguments
        case fileFormName
        case data
        case url
        case thumbnailUrl
        case deletionUrl
        case errorMessage
        case legacyMultipartBodyField
        case legacyUrlExpression
        case legacyBody = "body"
        case legacyUrlExpressionAlias = "urlExpression"
    }

    init(
        id: String = "",
        version: String = currentMobileVersion(),
        name: String = "",
        destinationType: String = "FileUploader",
        requestMethod: CustomUploaderRequestMethod = .POST,
        requestUrl: String = "",
        parameters: [String: String] = [:],
        headers: [String: String] = [:],
        bodyType: CustomUploaderBodyType = .multipartFormData,
        arguments: [String: String] = [:],
        fileFormName: String = "file",
        data: String = "",
        url: String = "",
        thumbnailUrl: String = "",
        deletionUrl: String = "",
        errorMessage: String = "",
        legacyMultipartBodyField: String = "",
        legacyUrlExpression: String = ""
    ) {
        self.id = id
        self.version = version
        self.name = name
        self.destinationType = destinationType
        self.requestMethod = requestMethod
        self.requestUrl = requestUrl
        self.parameters = parameters
        self.headers = headers
        self.bodyType = bodyType
        self.arguments = arguments
        self.fileFormName = fileFormName
        self.data = data
        self.url = url
        self.thumbnailUrl = thumbnailUrl
        self.deletionUrl = deletionUrl
        self.errorMessage = errorMessage
        self.legacyMultipartBodyField = legacyMultipartBodyField
        self.legacyUrlExpression = legacyUrlExpression
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)

        id = try container.decodeIfPresent(String.self, forKey: .id) ?? ""
        version = try container.decodeIfPresent(String.self, forKey: .version) ?? currentMobileVersion()
        name = try container.decodeIfPresent(String.self, forKey: .name) ?? ""
        destinationType = try container.decodeIfPresent(String.self, forKey: .destinationType) ?? "FileUploader"
        requestMethod = try container.decodeIfPresent(CustomUploaderRequestMethod.self, forKey: .requestMethod) ?? .POST
        requestUrl = try container.decodeIfPresent(String.self, forKey: .requestUrl) ?? ""
        parameters = try container.decodeIfPresent([String: String].self, forKey: .parameters) ?? [:]
        headers = try container.decodeIfPresent([String: String].self, forKey: .headers) ?? [:]
        bodyType = try container.decodeIfPresent(CustomUploaderBodyType.self, forKey: .bodyType) ?? .multipartFormData
        arguments = try container.decodeIfPresent([String: String].self, forKey: .arguments) ?? [:]
        fileFormName = try container.decodeIfPresent(String.self, forKey: .fileFormName) ?? "file"
        data = try container.decodeIfPresent(String.self, forKey: .data) ?? ""
        url = try container.decodeIfPresent(String.self, forKey: .url) ?? ""
        thumbnailUrl = try container.decodeIfPresent(String.self, forKey: .thumbnailUrl) ?? ""
        deletionUrl = try container.decodeIfPresent(String.self, forKey: .deletionUrl) ?? ""
        errorMessage = try container.decodeIfPresent(String.self, forKey: .errorMessage) ?? ""
        legacyMultipartBodyField = try container.decodeIfPresent(String.self, forKey: .legacyMultipartBodyField)
            ?? (try container.decodeIfPresent(String.self, forKey: .legacyBody) ?? "")
        legacyUrlExpression = try container.decodeIfPresent(String.self, forKey: .legacyUrlExpression)
            ?? (try container.decodeIfPresent(String.self, forKey: .legacyUrlExpressionAlias) ?? "")

        if id.isEmpty {
            id = "custom_\(UUID().uuidString.prefix(8))"
        }
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(version, forKey: .version)
        try container.encode(name, forKey: .name)
        try container.encode(destinationType, forKey: .destinationType)
        try container.encode(requestMethod, forKey: .requestMethod)
        try container.encode(requestUrl, forKey: .requestUrl)
        try container.encode(parameters, forKey: .parameters)
        try container.encode(headers, forKey: .headers)
        try container.encode(bodyType, forKey: .bodyType)
        try container.encode(arguments, forKey: .arguments)
        try container.encode(fileFormName, forKey: .fileFormName)
        try container.encode(data, forKey: .data)
        try container.encode(url, forKey: .url)
        try container.encode(thumbnailUrl, forKey: .thumbnailUrl)
        try container.encode(deletionUrl, forKey: .deletionUrl)
        try container.encode(errorMessage, forKey: .errorMessage)
        try container.encode(legacyMultipartBodyField, forKey: .legacyMultipartBodyField)
        try container.encode(legacyUrlExpression, forKey: .legacyUrlExpression)
    }

    var displayName: String {
        if !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return name
        }
        if let host = URL(string: requestUrl)?.host, !host.isEmpty {
            return host
        }
        return id
    }

    func toSxcuDefinition() -> SxcuDefinition {
        SxcuDefinition(
            version: version.isEmpty ? currentMobileVersion() : version,
            name: name,
            destinationType: destinationType,
            requestMethod: requestMethod,
            requestURL: requestUrl,
            parameters: parameters,
            headers: headers,
            body: bodyType,
            arguments: mergedArgumentsForExecution(),
            fileFormName: fileFormName,
            data: data,
            url: url,
            thumbnailURL: thumbnailUrl,
            deletionURL: deletionUrl,
            errorMessage: errorMessage
        )
    }

    static func from(sxcu definition: SxcuDefinition, id: String? = nil) -> CustomUploaderEntry {
        CustomUploaderEntry(
            id: id ?? "custom_\(UUID().uuidString.prefix(8))",
            version: definition.version,
            name: definition.name,
            destinationType: definition.destinationType,
            requestMethod: definition.requestMethod,
            requestUrl: definition.requestURL,
            parameters: definition.parameters,
            headers: definition.headers,
            bodyType: definition.body,
            arguments: definition.arguments,
            fileFormName: definition.fileFormName,
            data: definition.data,
            url: definition.url,
            thumbnailUrl: definition.thumbnailURL,
            deletionUrl: definition.deletionURL,
            errorMessage: definition.errorMessage
        )
    }

    func mergedArgumentsForExecution() -> [String: String] {
        var result = arguments
        if !legacyMultipartBodyField.isEmpty && result["body"] == nil {
            result["body"] = legacyMultipartBodyField
        }
        return result
    }
}

// MARK: - Active upload destination

/// Stable id for the built-in S3 destination. Used as defaultDestinationInstanceId when user selects Amazon S3.
let kAmazonS3DestinationId = "amazons3"

extension ApplicationConfig {
    /// Human-readable label for the currently selected upload destination, or nil if none configured/selected.
    func activeDestinationDisplayName() -> String? {
        let id = defaultDestinationInstanceId
        if id == kAmazonS3DestinationId || (id?.hasPrefix("amazons3") ?? false) {
            return s3Config.isConfigured ? "Amazon S3" : nil
        }
        if let id = id, let custom = customUploaders.first(where: { $0.id == id }) {
            return custom.displayName
        }
        if s3Config.isConfigured { return "Amazon S3" }
        if let first = customUploaders.first { return first.displayName }
        return nil
    }

    /// All selectable destinations: (displayName, instanceId). Order: S3 first (if configured), then custom uploaders.
    func selectableDestinations() -> [(displayName: String, instanceId: String)] {
        var list: [(String, String)] = []
        if s3Config.isConfigured { list.append(("Amazon S3", kAmazonS3DestinationId)) }
        for entry in customUploaders where !entry.id.isEmpty {
            list.append((entry.displayName, entry.id))
        }
        return list
    }
}

private func currentMobileVersion() -> String {
    (Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String) ?? "0.0.0"
}
