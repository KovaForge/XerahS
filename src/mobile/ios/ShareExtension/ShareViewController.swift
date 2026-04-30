//
//  ShareViewController.swift
//  XerahS Share Extension
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

import UIKit
import UniformTypeIdentifiers
import UserNotifications

private let appGroupId = "group.com.xerahs.xerahs"
private let pendingPathsKey = "PendingSharedPaths"
private let pendingSxcuImportsKey = "PendingSxcuImports"
private let openAppURLString = "xerahs://share"

final class ShareViewController: UIViewController {
    private let uploadService = ShareExtensionUploadService()

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        handleSharedItems()
    }

    private func handleSharedItems() {
        guard let extensionItems = extensionContext?.inputItems as? [NSExtensionItem] else {
            finishWithError()
            return
        }
        guard let groupContainer = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroupId) else {
            finishWithError()
            return
        }
        let caches = FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask).first
            ?? groupContainer.appendingPathComponent("Caches", isDirectory: true)
        Paths.configure(applicationSupport: groupContainer, caches: caches, appGroupContainer: groupContainer)
        Paths.ensureDirectoriesExist()

        let inbox = groupContainer.appendingPathComponent("ShareInbox", isDirectory: true)
        try? FileManager.default.createDirectory(at: inbox, withIntermediateDirectories: true)

        // Order: file-url first (preserves name), then URL, then specific types, then generic data so any file is accepted.
        let supportedTypes: [String] = [
            "public.file-url",
            "public.url",
            UTType.image.identifier,
            UTType.jpeg.identifier,
            UTType.png.identifier,
            UTType.gif.identifier,
            UTType.webP.identifier,
            UTType.heic.identifier,
            UTType.pdf.identifier,
            UTType.movie.identifier,
            UTType.mpeg4Movie.identifier,
            UTType.quickTimeMovie.identifier,
            UTType.avi.identifier,
            UTType.audio.identifier,
            UTType.mp3.identifier,
            UTType.mpeg4Audio.identifier,
            UTType.wav.identifier,
            UTType.plainText.identifier,
            UTType.utf8PlainText.identifier,
            UTType.content.identifier,
            UTType.data.identifier,
            "public.data",
            "public.content"
        ]
        var savedPaths: [String] = []
        let group = DispatchGroup()
        let lock = NSLock()

        for item in extensionItems {
            guard let attachments = item.attachments else { continue }
            for provider in attachments {
                for typeId in supportedTypes {
                    if provider.hasItemConformingToTypeIdentifier(typeId) {
                        group.enter()
                        provider.loadItem(forTypeIdentifier: typeId, options: nil) { data, _ in
                            defer { group.leave() }
                            guard let data = data else { return }
                            var path: String?
                            if let url = data as? URL {
                                path = self.copyToInbox(url: url, inbox: inbox)
                            } else if let image = data as? UIImage, let d = image.jpegData(compressionQuality: 0.9) {
                                path = self.writeData(d, to: inbox, ext: "jpg")
                            } else if let d = data as? Data {
                                path = self.writeData(d, to: inbox, ext: "bin")
                            }
                            if let p = path {
                                lock.lock()
                                savedPaths.append(p)
                                lock.unlock()
                            }
                        }
                        break
                    }
                }
            }
        }

        group.notify(queue: .main) { [weak self] in
            self?.finalizeShare(savedPaths: savedPaths)
        }
    }

    private func copyToInbox(url: URL, inbox: URL) -> String? {
        let isSecurityScoped = url.startAccessingSecurityScopedResource()
        defer { if isSecurityScoped { url.stopAccessingSecurityScopedResource() } }
        let name = url.lastPathComponent.isEmpty ? "shared_\(UUID().uuidString.prefix(8))" : url.lastPathComponent
        let dest = inbox.appendingPathComponent(name)
        do {
            if FileManager.default.fileExists(atPath: dest.path) { try FileManager.default.removeItem(at: dest) }
            try FileManager.default.copyItem(at: url, to: dest)
            return dest.path
        } catch {
            return nil
        }
    }

    private func writeData(_ data: Data, to inbox: URL, ext: String) -> String? {
        let name = "shared_\(UUID().uuidString.prefix(8)).\(ext)"
        let dest = inbox.appendingPathComponent(name)
        do {
            try data.write(to: dest)
            return dest.path
        } catch {
            return nil
        }
    }

    private func finalizeShare(savedPaths: [String]) {
        if savedPaths.isEmpty {
            finishWithError()
            return
        }
        let sxcuPaths = savedPaths.filter { ($0 as NSString).pathExtension.lowercased() == "sxcu" }
        let uploadPaths = savedPaths.filter { ($0 as NSString).pathExtension.lowercased() != "sxcu" }

        if !sxcuPaths.isEmpty {
            enqueueForMainApp(paths: sxcuPaths, key: pendingSxcuImportsKey)
        }

        if uploadPaths.isEmpty {
            openContainingApp()
            extensionContext?.completeRequest(returningItems: nil, completionHandler: nil)
            return
        }

        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self else { return }
            let results = self.uploadService.uploadFiles(uploadPaths)
            DispatchQueue.main.async {
                self.finishUploadShare(uploadResults: results, hasPendingImport: !sxcuPaths.isEmpty)
            }
        }
    }

    private func finishUploadShare(uploadResults: [ShareExtensionUploadResult], hasPendingImport: Bool) {
        let uploadedUrls = uploadResults.compactMap(\.url)
        let failedPaths = uploadResults.filter { !$0.succeeded }.map(\.filePath)

        if !uploadedUrls.isEmpty {
            UIPasteboard.general.string = uploadedUrls.joined(separator: "\n")
            removeUploadedInboxFiles(uploadResults)
        }

        if !failedPaths.isEmpty {
            enqueueForMainApp(paths: failedPaths, key: pendingPathsKey)
        }

        if hasPendingImport || !failedPaths.isEmpty {
            openContainingApp()
        }

        let notificationTitle: String
        let notificationBody: String
        if uploadedUrls.count == uploadResults.count {
            notificationTitle = "XerahS upload complete"
            notificationBody = uploadedUrls.count == 1
                ? "Link copied to Clipboard."
                : "\(uploadedUrls.count) links copied to Clipboard."
        } else if !uploadedUrls.isEmpty {
            notificationTitle = "XerahS upload partially complete"
            notificationBody = "\(uploadedUrls.count) link(s) copied. \(failedPaths.count) file(s) queued for XerahS."
        } else {
            notificationTitle = "XerahS upload queued"
            let firstError = uploadResults.compactMap(\.error).first ?? "Open XerahS to finish uploading."
            notificationBody = firstError
        }

        scheduleNotification(title: notificationTitle, body: notificationBody) { [weak self] in
            DispatchQueue.main.async {
                self?.extensionContext?.completeRequest(returningItems: nil, completionHandler: nil)
            }
        }
    }

    private func enqueueForMainApp(paths: [String], key: String) {
        guard !paths.isEmpty else { return }
        let defaults = UserDefaults(suiteName: appGroupId)
        var pending = (defaults?.array(forKey: key) as? [String]) ?? []
        pending.append(contentsOf: paths)
        defaults?.set(pending, forKey: key)
        defaults?.synchronize()
    }

    private func removeUploadedInboxFiles(_ results: [ShareExtensionUploadResult]) {
        for result in results where result.succeeded {
            try? FileManager.default.removeItem(atPath: result.filePath)
        }
    }

    private func openContainingApp() {
        if let url = URL(string: openAppURLString) {
            extensionContext?.open(url, completionHandler: nil)
        }
    }

    private func scheduleNotification(title: String, body: String, completion: @escaping () -> Void) {
        let center = UNUserNotificationCenter.current()
        center.getNotificationSettings { settings in
            switch settings.authorizationStatus {
            case .authorized, .provisional, .ephemeral:
                self.addNotification(center: center, title: title, body: body, completion: completion)
            case .notDetermined:
                center.requestAuthorization(options: [.alert, .sound]) { granted, _ in
                    if granted {
                        self.addNotification(center: center, title: title, body: body, completion: completion)
                    } else {
                        completion()
                    }
                }
            case .denied:
                completion()
            @unknown default:
                completion()
            }
        }
    }

    private func addNotification(
        center: UNUserNotificationCenter,
        title: String,
        body: String,
        completion: @escaping () -> Void
    ) {
        let content = UNMutableNotificationContent()
        content.title = title
        content.body = body
        content.sound = .default
        let request = UNNotificationRequest(
            identifier: "xerahs-share-upload-\(UUID().uuidString)",
            content: content,
            trigger: nil
        )
        center.add(request) { _ in completion() }
    }

    private func finishWithError() {
        extensionContext?.cancelRequest(withError: NSError(domain: "XerahS.Share", code: -1, userInfo: [NSLocalizedDescriptionKey: "No supported items to share."]))
    }
}
