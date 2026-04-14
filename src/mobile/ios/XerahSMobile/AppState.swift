//
//  AppState.swift
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
import Combine

/// Global app state: repositories and upload worker. Injected via environment.
final class AppState: ObservableObject {
    let settingsRepository: SettingsRepository
    let historyRepository: HistoryRepository
    let uploadQueueWorker: UploadQueueWorker

    /// Paths from share intent to process when Upload screen is ready. Consumed once.
    @Published var pendingSharedPaths: [String] = []
    @Published var pendingNavigation: Screen?
    @Published var bannerMessage: String?

    init(
        settingsRepository: SettingsRepository,
        historyRepository: HistoryRepository,
        uploadQueueWorker: UploadQueueWorker
    ) {
        self.settingsRepository = settingsRepository
        self.historyRepository = historyRepository
        self.uploadQueueWorker = uploadQueueWorker
    }

    func handleIncomingURL(_ url: URL) {
        if url.isFileURL && url.pathExtension.lowercased() == "sxcu" {
            importSxcuFile(from: url)
            return
        }

        guard url.scheme == "xerahs" else { return }

        let normalizedPath = url.path.trimmingCharacters(in: CharacterSet(charactersIn: "/")).lowercased()
        let normalizedHost = (url.host ?? "").lowercased()
        if normalizedHost == "import-sxcu" || normalizedPath == "import-sxcu" {
            importRemoteSxcu(from: url)
            return
        }

        pendingSharedPaths = ShareGroup.consumePendingPaths()
    }

    private func importSxcuFile(from url: URL) {
        let accessing = url.startAccessingSecurityScopedResource()
        defer {
            if accessing {
                url.stopAccessingSecurityScopedResource()
            }
        }

        guard let data = try? Data(contentsOf: url) else {
            publishImportResult(message: "Failed to read .sxcu file.", navigate: false)
            return
        }

        importSxcuData(data, sourceLabel: url.lastPathComponent)
    }

    private func importRemoteSxcu(from deepLink: URL) {
        guard
            let components = URLComponents(url: deepLink, resolvingAgainstBaseURL: false),
            let rawTarget = components.queryItems?.first(where: { $0.name == "url" })?.value,
            let targetUrl = URL(string: rawTarget),
            ["https", "http"].contains(targetUrl.scheme?.lowercased() ?? "")
        else {
            publishImportResult(message: "Invalid import link. Missing or invalid remote .sxcu URL.", navigate: false)
            return
        }

        Task {
            do {
                let (data, _) = try await URLSession.shared.data(from: targetUrl)
                importSxcuData(data, sourceLabel: targetUrl.lastPathComponent.isEmpty ? targetUrl.absoluteString : targetUrl.lastPathComponent)
            } catch {
                publishImportResult(message: "Failed to download .sxcu: \(error.localizedDescription)", navigate: false)
            }
        }
    }

    private func importSxcuData(_ data: Data, sourceLabel: String) {
        let decoder = JSONDecoder()

        guard let definition = try? decoder.decode(SxcuDefinition.self, from: data) else {
            publishImportResult(message: "The file \(sourceLabel) is not a valid .sxcu definition.", navigate: false)
            return
        }

        var imported = CustomUploaderEntry.from(sxcu: definition)
        var list = settingsRepository.loadCustomUploaders()

        if let existingIndex = list.firstIndex(where: {
            $0.requestUrl.caseInsensitiveCompare(imported.requestUrl) == .orderedSame &&
            $0.name.caseInsensitiveCompare(imported.name) == .orderedSame
        }) {
            imported.id = list[existingIndex].id
            list[existingIndex] = imported
            settingsRepository.saveCustomUploaders(list)
            publishImportResult(message: "Updated custom uploader: \(imported.displayName)", navigate: true)
            return
        }

        list.append(imported)
        settingsRepository.saveCustomUploaders(list)
        publishImportResult(message: "Imported custom uploader: \(imported.displayName)", navigate: true)
    }

    private func publishImportResult(message: String, navigate: Bool) {
        DispatchQueue.main.async {
            self.bannerMessage = message
            if navigate {
                self.pendingNavigation = .customUploaderConfig
            }
        }
    }
}
