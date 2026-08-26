//
//  UploadQueueWorker.swift
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

/// Processes the persistent upload queue: dequeue one item, resolve destination (S3 or custom),
/// upload, append to history, emit result.
final class UploadQueueWorker: ObservableObject {
    struct QueueState {
        var processing: Bool
        var pendingCount: Int
    }

    private let settingsRepository: SettingsRepository
    private let queueRepository: QueueRepository
    private let historyRepository: HistoryRepository
    private let s3Uploader: S3Uploader
    private let customUploader: CustomUploader
    private let cloudClient: XerahSCloudClient
    private let queue = DispatchQueue(label: "UploadQueueWorker")
    private var processing = false

    let state = CurrentValueSubject<QueueState, Never>(QueueState(processing: false, pendingCount: 0))
    let itemCompleted = PassthroughSubject<UploadResultItem?, Never>()
    let cloudPublishResult = PassthroughSubject<String, Never>()

    init(
        settingsRepository: SettingsRepository,
        queueRepository: QueueRepository,
        historyRepository: HistoryRepository,
        cloudClient: XerahSCloudClient,
        s3Uploader: S3Uploader = S3Uploader(),
        customUploader: CustomUploader = CustomUploader()
    ) {
        self.settingsRepository = settingsRepository
        self.queueRepository = queueRepository
        self.historyRepository = historyRepository
        self.cloudClient = cloudClient
        self.s3Uploader = s3Uploader
        self.customUploader = customUploader
    }

    func startProcessing() {
        queue.async { [weak self] in
            guard let self = self, !self.processing else { return }
            self.processing = true
            self.runLoop()
            self.processing = false
        }
    }

    private func runLoop() {
        updateState()
        while let item = queueRepository.dequeue() {
            let fileName = (item.filePath as NSString).lastPathComponent
            let result = uploadOne(filePath: item.filePath)
            if result.success, let url = result.url {
                var tags: [String: String?] = [:]
                if XerahSCloudSettings.automaticallyPublishesEligibleUploads,
                   XerahSCloudClient.eligibleMetadata(fileName: fileName) != nil,
                   let publicURL = URL(string: url), publicURL.scheme?.lowercased() == "https" {
                    if cloudClient.hasStoredCredential, let originalOwner = cloudClient.currentOwnerSubject, !originalOwner.isEmpty {
                        tags["XerahSCloudClientItemId"] = item.clientItemId.uuidString.lowercased()
                        tags["XerahSCloudOwnerSubject"] = originalOwner
                        do {
                            try waitForCloudPublish(clientItemID: item.clientItemId, url: publicURL, fileName: fileName, expectedOwnerSubject: originalOwner)
                            tags["XerahSCloudPublishState"] = "published"
                            cloudPublishResult.send("Published \(fileName) to XerahS Cloud.")
                        } catch {
                            tags["XerahSCloudPublishState"] = "failed"
                            cloudPublishResult.send("Upload succeeded, but Cloud publish failed: \(error.localizedDescription) Retry from local history when signed in to the same account.")
                        }
                    } else {
                        cloudPublishResult.send("Upload succeeded. Cloud publish was skipped because no secure Cloud session is available; sign in from Settings.")
                    }
                }
                _ = historyRepository.insertEntry(fileName: fileName, filePath: item.filePath, type: "File", host: "upload", url: url, tags: tags)
            }
            itemCompleted.send(result)
            itemCompleted.send(nil)
        }
        updateState()
    }

    func retryCloudPublish(_ entry: HistoryEntry) {
        guard entry.tags["XerahSCloudPublishState"] ?? nil == "failed",
              let idValue = entry.tags["XerahSCloudClientItemId"] ?? nil,
              let originalOwner = entry.tags["XerahSCloudOwnerSubject"] ?? nil,
              !originalOwner.isEmpty,
              let clientItemID = UUID(uuidString: idValue),
              let publicURL = URL(string: entry.url) else { return }
        guard cloudClient.currentOwnerSubject == originalOwner else {
            cloudPublishResult.send("Cloud publish retry rejected: sign in to the original XerahS Cloud account for this history item.")
            return
        }
        queue.async { [weak self] in
            guard let self else { return }
            guard self.cloudClient.currentOwnerSubject == originalOwner else {
                self.cloudPublishResult.send("Cloud publish retry rejected: the active account changed before retry.")
                return
            }
            do {
                try self.waitForCloudPublish(clientItemID: clientItemID, url: publicURL, fileName: entry.fileName, expectedOwnerSubject: originalOwner)
                var tags = entry.tags
                tags["XerahSCloudPublishState"] = "published"
                _ = self.historyRepository.updateTags(id: entry.id, tags: tags)
                self.cloudPublishResult.send("Published \(entry.fileName) to XerahS Cloud.")
            } catch {
                self.cloudPublishResult.send("Cloud publish retry failed: \(error.localizedDescription) This operation is online-only.")
            }
        }
    }

    private func waitForCloudPublish(clientItemID: UUID, url: URL, fileName: String, expectedOwnerSubject: String) throws {
        let semaphore = DispatchSemaphore(value: 0)
        var failure: Error?
        Task {
            do { try await cloudClient.publish(clientItemID: clientItemID, publicURL: url, fileName: fileName, expectedOwnerSubject: expectedOwnerSubject) }
            catch { failure = error }
            semaphore.signal()
        }
        semaphore.wait()
        if let failure { throw failure }
    }

    func updateState() {
        let count = queueRepository.pendingCount()
        state.send(QueueState(processing: count > 0, pendingCount: count))
    }

    func enqueueFiles(_ filePaths: [String]) -> Int {
        let valid = filePaths.filter { FileManager.default.fileExists(atPath: $0) }
        let added = queueRepository.enqueue(filePaths: valid)
        if added > 0 {
            updateState()
            startProcessing()
        }
        return added
    }

    private func uploadOne(filePath: String) -> UploadResultItem {
        let fileName = (filePath as NSString).lastPathComponent
        guard FileManager.default.fileExists(atPath: filePath) else {
            return UploadResultItem(fileName: fileName, success: false, url: nil, error: "File not found", errorDetails: nil)
        }
        let config = settingsRepository.load()
        let pathToUpload = convertHeicToPngIfNeeded(filePath: filePath, convertEnabled: config.convertHeicToPng)
        let destId = config.defaultDestinationInstanceId

        if config.s3Config.isConfigured && (destId == nil || destId == "amazons3" || (destId?.hasPrefix("amazons3") ?? false)) {
            switch s3Uploader.uploadFile(filePath: pathToUpload, config: config.s3Config) {
            case .success(let url):
                return UploadResultItem(fileName: fileName, success: true, url: url, error: nil, errorDetails: nil)
            case .failure(let failure):
                return UploadResultItem(fileName: fileName, success: false, url: nil, error: failure.message, errorDetails: failure.details)
            }
        }
        if !config.customUploaders.isEmpty {
            let entry = config.customUploaders.first { $0.id == destId } ?? config.customUploaders[0]
            switch customUploader.uploadFile(filePath: pathToUpload, entry: entry) {
            case .success(let url):
                return UploadResultItem(fileName: fileName, success: true, url: url, error: nil, errorDetails: nil)
            case .failure(let failure):
                return UploadResultItem(fileName: fileName, success: false, url: nil, error: failure.message, errorDetails: failure.details)
            }
        }
        return UploadResultItem(
            fileName: fileName,
            success: false,
            url: nil,
            error: "No upload destination configured. Configure S3 or a custom uploader in Settings.",
            errorDetails: nil
        )
    }
}
