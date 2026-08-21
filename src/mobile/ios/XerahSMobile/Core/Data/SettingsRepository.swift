//
//  SettingsRepository.swift
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

private let configFileName = "ApplicationConfig.json"

extension Notification.Name {
    static let xerahSSettingsDidChange = Notification.Name("XerahS.SettingsDidChange")
}

/// Load/save ApplicationConfig as JSON in settings folder. Thread-safe via serial queue.
final class SettingsRepository {
    private let queue = DispatchQueue(label: "SettingsRepository")
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    private var configFile: URL? {
        Paths.settingsFolder?.appendingPathComponent(configFileName)
    }

    func load() -> ApplicationConfig {
        queue.sync {
            guard let file = configFile, FileManager.default.fileExists(atPath: file.path) else {
                return ApplicationConfig()
            }
            do {
                let data = try Data(contentsOf: file)
                let decoded = (try? decoder.decode(ApplicationConfig.self, from: data)) ?? ApplicationConfig()
                let secured = secureForStorage(decoded)
                if secured.didChange {
                    writeConfig(secured.config, to: file)
                }
                return hydrateSecrets(in: secured.config)
            } catch {
                return ApplicationConfig()
            }
        }
    }

    func save(_ config: ApplicationConfig) {
        queue.sync {
            guard let file = configFile else { return }
            writeConfig(secureForStorage(config).config, to: file)
        }
        NotificationCenter.default.post(name: .xerahSSettingsDidChange, object: nil)
    }

    func loadS3Config() -> S3Config { load().s3Config }
    func saveS3Config(_ config: S3Config) {
        var c = load()
        c.s3Config = config
        save(c)
    }

    func loadCustomUploaders() -> [CustomUploaderEntry] { load().customUploaders }
    func saveCustomUploaders(_ list: [CustomUploaderEntry]) {
        var c = load()
        c.customUploaders = list
        save(c)
    }

    func getDefaultDestinationInstanceId() -> String? { load().defaultDestinationInstanceId }
    func setDefaultDestinationInstanceId(_ id: String?) {
        var c = load()
        c.defaultDestinationInstanceId = id
        save(c)
    }

    func getConvertHeicToPng() -> Bool { load().convertHeicToPng }
    func setConvertHeicToPng(_ value: Bool) {
        var c = load()
        c.convertHeicToPng = value
        save(c)
    }

    private func writeConfig(_ config: ApplicationConfig, to file: URL) {
        Paths.settingsFolder.flatMap { try? FileManager.default.createDirectory(at: $0, withIntermediateDirectories: true) }
        try? encoder.encode(config).write(to: file, options: [.atomic])
    }

    private func secureForStorage(_ config: ApplicationConfig) -> (config: ApplicationConfig, didChange: Bool) {
        var secured = config
        var didChange = false

        if replaceWithKeychainMarker(&secured.s3Config.secretAccessKey, account: "s3.secretAccessKey") {
            didChange = true
        }

        for index in secured.customUploaders.indices {
            let id = secured.customUploaders[index].id.isEmpty ? "custom_\(index)" : secured.customUploaders[index].id
            didChange = secureDictionary(&secured.customUploaders[index].parameters, uploaderId: id, section: "parameters") || didChange
            didChange = secureDictionary(&secured.customUploaders[index].headers, uploaderId: id, section: "headers") || didChange
            didChange = secureDictionary(&secured.customUploaders[index].arguments, uploaderId: id, section: "arguments") || didChange
        }

        return (secured, didChange)
    }

    private func hydrateSecrets(in config: ApplicationConfig) -> ApplicationConfig {
        var hydrated = config

        hydrateKeychainMarker(&hydrated.s3Config.secretAccessKey)

        for index in hydrated.customUploaders.indices {
            hydrateDictionary(&hydrated.customUploaders[index].parameters)
            hydrateDictionary(&hydrated.customUploaders[index].headers)
            hydrateDictionary(&hydrated.customUploaders[index].arguments)
        }

        return hydrated
    }

    private func secureDictionary(_ dictionary: inout [String: String], uploaderId: String, section: String) -> Bool {
        var didChange = false
        for key in dictionary.keys.sorted() where isSensitiveCustomUploaderKey(key) {
            guard var value = dictionary[key] else { continue }
            let account = "customUploader.\(uploaderId).\(section).\(key)"
            if replaceWithKeychainMarker(&value, account: account) {
                dictionary[key] = value
                didChange = true
            }
        }
        return didChange
    }

    private func hydrateDictionary(_ dictionary: inout [String: String]) {
        for key in dictionary.keys {
            guard var value = dictionary[key] else { continue }
            hydrateKeychainMarker(&value)
            dictionary[key] = value
        }
    }

    private func replaceWithKeychainMarker(_ value: inout String, account: String) -> Bool {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, keychainAccount(fromMarker: value) == nil else { return false }
        guard KeychainStore.writeString(value, account: account) else { return false }
        value = keychainMarker(for: account)
        return true
    }

    private func hydrateKeychainMarker(_ value: inout String) {
        guard
            let account = keychainAccount(fromMarker: value),
            let stored = KeychainStore.readString(account: account)
        else { return }
        value = stored
    }

    private func keychainMarker(for account: String) -> String {
        "__xerahs_keychain__:\(account)"
    }

    private func keychainAccount(fromMarker value: String) -> String? {
        let prefix = "__xerahs_keychain__:"
        guard value.hasPrefix(prefix) else { return nil }
        return String(value.dropFirst(prefix.count))
    }

    private func isSensitiveCustomUploaderKey(_ key: String) -> Bool {
        let normalized = key.lowercased()
            .replacingOccurrences(of: "-", with: "")
            .replacingOccurrences(of: "_", with: "")
            .replacingOccurrences(of: " ", with: "")

        if normalized == "authorization" || normalized == "auth" {
            return true
        }

        return [
            "apikey",
            "accesstoken",
            "bearer",
            "clientsecret",
            "password",
            "passwd",
            "secret",
            "signature",
            "token"
        ].contains { normalized.contains($0) }
    }
}
