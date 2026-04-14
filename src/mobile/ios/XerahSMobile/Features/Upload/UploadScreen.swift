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
    var onOpenHistory: () -> Void
    var onOpenSettings: () -> Void
    var onCopyToClipboard: (String) -> Void
    var initialPaths: [String]?
    /// Human-readable label for the active upload destination (e.g. "Amazon S3"). Shown so user knows where files will go.
    var activeDestinationLabel: String? = nil

    @State private var statusText = "Share files to XerahS to upload them."
    @State private var isUploading = false
    @State private var results: [UploadResultItem] = []

    var body: some View {
        ZStack {
            XerahSPageBackground()

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    HStack(spacing: 12) {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("XerahS")
                                .font(.system(size: 33, weight: .bold, design: .rounded))
                                .foregroundStyle(.white)
                            Text("Share files into the app and push them straight to your active destination.")
                                .font(.subheadline)
                                .foregroundStyle(.white.opacity(0.72))
                        }
                        Spacer(minLength: 12)
                        Button("History", action: onOpenHistory)
                            .xerahSGlassButton()
                        Button("Settings", action: onOpenSettings)
                            .xerahSGlassButton()
                    }

                    XerahSGlassGroup {
                        XerahSGlassCard {
                            VStack(alignment: .leading, spacing: 14) {
                                HStack(alignment: .top) {
                                    VStack(alignment: .leading, spacing: 8) {
                                        XerahSStatusPill(text: isUploading ? "UPLOADING" : "READY")
                                        Text("Share & Upload")
                                            .font(.title3.weight(.semibold))
                                            .foregroundStyle(.white)
                                        Text(statusText)
                                            .font(.body)
                                            .foregroundStyle(.white.opacity(0.76))
                                    }
                                    Spacer(minLength: 12)
                                    if isUploading {
                                        ProgressView()
                                            .tint(.white)
                                    }
                                }

                                if let label = activeDestinationLabel {
                                    Text("Destination: \(label)")
                                        .font(.subheadline.weight(.medium))
                                        .foregroundStyle(.white.opacity(0.82))
                                }
                            }
                        }

                        if !results.isEmpty {
                            XerahSSectionIntro(
                                title: "Recent Results",
                                detail: "Successful uploads expose a copy action. Failed uploads keep full diagnostics in Copy Error."
                            )
                        }

                        ForEach(Array(results.enumerated()), id: \.offset) { _, item in
                            ResultCard(item: item, onCopyToClipboard: onCopyToClipboard)
                        }
                    }
                }
                .padding(.horizontal, 20)
                .padding(.top, 20)
                .padding(.bottom, 28)
            }
            .scrollIndicators(.hidden)
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
        }
        .onAppear {
            worker.updateState()
            if let paths = initialPaths, !paths.isEmpty {
                _ = worker.enqueueFiles(paths)
            }
        }
        .onChange(of: initialPaths) { _, newValue in
            if let paths = newValue, !paths.isEmpty {
                _ = worker.enqueueFiles(paths)
            }
        }
    }
}

private struct ResultCard: View {
    let item: UploadResultItem
    var onCopyToClipboard: (String) -> Void

    var body: some View {
        XerahSGlassCard {
            VStack(alignment: .leading, spacing: 10) {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(item.fileName)
                            .font(.headline)
                            .foregroundStyle(.white)
                        Text(item.success ? "Upload completed" : "Upload failed")
                            .font(.subheadline)
                            .foregroundStyle(item.success ? .white.opacity(0.72) : Color(red: 1.0, green: 0.82, blue: 0.82))
                    }
                    Spacer()
                    Image(systemName: item.success ? "checkmark.circle.fill" : "exclamationmark.triangle.fill")
                        .foregroundStyle(item.success ? Color(red: 0.52, green: 0.95, blue: 0.72) : Color(red: 1.0, green: 0.73, blue: 0.58))
                }

            if item.hasUrl, let url = item.url {
                Text(url)
                    .font(.caption)
                    .foregroundStyle(.white.opacity(0.78))
                    .lineLimit(3)
                Button("Copy URL") { onCopyToClipboard(url) }
                    .xerahSGlassButton(prominent: true)
            }
            if !item.success, let err = item.error {
                Text(err)
                    .font(.caption)
                    .foregroundStyle(Color(red: 1.0, green: 0.82, blue: 0.82))
                    .lineLimit(4)
                if let details = item.errorDetails, !details.isEmpty, details != err {
                    Text("Copy Error includes request, response, and transport diagnostics.")
                        .font(.caption2)
                        .foregroundStyle(.white.opacity(0.64))
                }
                Button("Copy Error") { onCopyToClipboard(item.errorClipboardText ?? err) }
                    .xerahSGlassButton()
            }
        }
        }
    }
}
