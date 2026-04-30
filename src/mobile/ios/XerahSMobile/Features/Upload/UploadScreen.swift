//
//  UploadScreen.swift
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

import SwiftUI
import Combine

struct UploadScreen: View {
    @ObservedObject var worker: UploadQueueWorker
    var onCopyToClipboard: (String) -> Void
    var onAutoShareUploadFinished: ([UploadResultItem]) -> Void
    var onInitialPathsConsumed: () -> Void
    var initialPaths: [String]?
    /// Human-readable label for the active upload destination (e.g. "Amazon S3"). Shown so user knows where files will go.
    var activeDestinationLabel: String? = nil

    @State private var statusText = "Share files to XerahS to upload them."
    @State private var isUploading = false
    @State private var results: [UploadResultItem] = []
    @State private var pendingAutoShareUploads = 0
    @State private var autoShareResults: [UploadResultItem] = []

    var body: some View {
        List {
            Section {
                HStack(alignment: .top, spacing: 14) {
                    Image(systemName: isUploading ? "arrow.triangle.2.circlepath" : "checkmark.circle")
                        .font(.title2)
                        .foregroundStyle(isUploading ? .blue : .green)
                        .symbolEffect(.pulse, isActive: isUploading)

                    VStack(alignment: .leading, spacing: 6) {
                        Text(isUploading ? "Uploading" : "Ready")
                            .font(.headline)
                        Text(statusText)
                            .foregroundStyle(.secondary)
                        if let label = activeDestinationLabel {
                            Text("Destination: \(label)")
                                .font(.subheadline.weight(.medium))
                        }
                    }
                }
            } footer: {
                Text("Share photos, files, or `.sxcu` definitions to XerahS from the iOS share sheet.")
            }

            if !results.isEmpty {
                Section("Recent Results") {
                    ForEach(Array(results.enumerated()), id: \.offset) { _, item in
                        ResultRow(item: item, onCopyToClipboard: onCopyToClipboard)
                    }
                }
            }
        }
        .navigationTitle("XerahS")
        .toolbar {
            if isUploading {
                ToolbarItem(placement: .topBarTrailing) {
                    ProgressView()
                }
            }
        }
        .onReceive(worker.state.receive(on: DispatchQueue.main)) { state in
            isUploading = state.processing
            statusText = state.processing
                ? "Uploading \(state.pendingCount) file(s)..."
                : state.pendingCount > 0
                    ? "Queued \(state.pendingCount) file(s)."
                    : results.isEmpty ? "Share files to XerahS to upload them." : "Done."
        }
        .onReceive(worker.itemCompleted.receive(on: DispatchQueue.main).compactMap { $0 }) { result in
            results.append(result)
            if pendingAutoShareUploads > 0 {
                pendingAutoShareUploads -= 1
                autoShareResults.append(result)
                if pendingAutoShareUploads == 0 {
                    let completed = autoShareResults
                    autoShareResults = []
                    onAutoShareUploadFinished(completed)
                }
            }
        }
        .onAppear {
            worker.updateState()
            enqueueInitialPathsIfNeeded(initialPaths)
        }
        .onChange(of: initialPaths) { _, newValue in
            enqueueInitialPathsIfNeeded(newValue)
        }
    }

    private func enqueueInitialPathsIfNeeded(_ paths: [String]?) {
        guard let paths, !paths.isEmpty else { return }
        let added = worker.enqueueFiles(paths)
        onInitialPathsConsumed()
        if added > 0 {
            pendingAutoShareUploads += added
        }
    }
}

private struct ResultRow: View {
    let item: UploadResultItem
    var onCopyToClipboard: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label(item.fileName, systemImage: item.success ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                .font(.headline)
                .foregroundStyle(item.success ? Color.primary : Color.red)

            if item.hasUrl, let url = item.url {
                Text(url)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(3)
                Button {
                    onCopyToClipboard(url)
                } label: {
                    Label("Copy URL", systemImage: "doc.on.doc")
                }
            }

            if !item.success, let err = item.error {
                Text(err)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .lineLimit(4)
                if let details = item.errorDetails, !details.isEmpty, details != err {
                    Text("Copy Error includes request, response, and transport diagnostics.")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
                Button {
                    onCopyToClipboard(item.errorClipboardText ?? err)
                } label: {
                    Label("Copy Error", systemImage: "exclamationmark.doc")
                }
            }
        }
    }
}
