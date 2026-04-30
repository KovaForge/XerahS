//
//  RootView.swift
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
import UIKit

private enum AppPhase {
    case loading
    case main
}

private struct TransientToast: Equatable {
    let title: String?
    let message: String
    let isError: Bool
}

struct RootView: View {
    @EnvironmentObject var appState: AppState
    @State private var phase: AppPhase = .loading
    @State private var navPath: [Screen] = []
    @State private var transientToast: TransientToast?
    @State private var settingsRevision: Int = 0

    private func copyToClipboard(_ text: String) {
        UIPasteboard.general.string = text
        showToast(TransientToast(title: nil, message: "Copied to clipboard", isError: false))
    }

    private func showToast(_ toast: TransientToast) {
        transientToast = toast
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) {
            if transientToast == toast {
                transientToast = nil
            }
        }
    }

    private func handleAutoShareUploadFinished(_ results: [UploadResultItem]) {
        guard !results.isEmpty else { return }

        let successes = results.filter(\.success)
        let failures = results.filter { !$0.success }
        let copiedUrls = successes.compactMap(\.url).filter { !$0.isEmpty }

        if !copiedUrls.isEmpty {
            UIPasteboard.general.string = copiedUrls.joined(separator: "\n")
        }

        if failures.isEmpty {
            let title = results.count == 1 ? "Upload Complete" : "Uploads Complete"
            let message: String
            if copiedUrls.count == 1 {
                message = "Link copied to clipboard."
            } else {
                message = "\(copiedUrls.count) links copied to clipboard."
            }
            showToast(TransientToast(title: title, message: message, isError: false))
            return
        }

        navPath = []

        if successes.isEmpty {
            showToast(TransientToast(
                title: "Upload Failed",
                message: "Shared item could not be uploaded. Open XerahS to review the error details.",
                isError: true
            ))
        } else {
            showToast(TransientToast(
                title: "Upload Finished With Errors",
                message: "\(successes.count) completed, \(failures.count) failed. Successful links were copied to clipboard.",
                isError: true
            ))
        }
    }

    private func navigate(to screen: Screen) {
        switch screen {
        case .customUploaderConfig:
            if !navPath.contains(.settings) {
                navPath.append(.settings)
            }
            if navPath.last != .customUploaderConfig {
                navPath.append(.customUploaderConfig)
            }
        default:
            if navPath.last != screen {
                navPath.append(screen)
            }
        }
    }

    var body: some View {
        Group {
            if phase == .loading {
                LoadingScreen { phase = .main }
            } else {
                mainNav
            }
        }
        .overlay(alignment: .bottom) {
            if let toast = transientToast {
                XerahSGlassCard(padding: 0) {
                    VStack(alignment: .leading, spacing: 4) {
                        if let title = toast.title, !title.isEmpty {
                            Text(title)
                                .font(.subheadline.weight(.semibold))
                                .foregroundStyle(.white)
                        }
                        Text(toast.message)
                            .font(.footnote)
                            .foregroundStyle(toast.isError ? Color(red: 1.0, green: 0.82, blue: 0.82) : .white.opacity(0.88))
                    }
                    .padding(.horizontal, 20)
                    .padding(.vertical, 12)
                }
                .frame(maxWidth: 340)
                .padding(.bottom, 32)
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
        .animation(.easeInOut(duration: 0.25), value: transientToast)
        .sheet(item: $appState.pendingDestinationConfigImport) { pending in
            DestinationConfigPassphraseSheet(
                sourceLabel: pending.sourceLabel,
                onImport: { passphrase in
                    appState.importPendingDestinationConfig(passphrase: passphrase)
                },
                onCancel: {
                    appState.cancelPendingDestinationConfigImport()
                }
            )
        }
        .onReceive(NotificationCenter.default.publisher(for: .xerahSSettingsDidChange)) { _ in
            settingsRevision += 1
        }
        .onAppear {
            if let banner = appState.bannerMessage, !banner.isEmpty {
                showToast(TransientToast(title: nil, message: banner, isError: false))
                appState.bannerMessage = nil
            }
            if let pending = appState.pendingNavigation {
                navigate(to: pending)
                appState.pendingNavigation = nil
            }
        }
        .onChange(of: appState.bannerMessage) { _, newValue in
            guard let newValue, !newValue.isEmpty else { return }
            showToast(TransientToast(title: nil, message: newValue, isError: false))
            appState.bannerMessage = nil
        }
        .onChange(of: appState.pendingNavigation) { _, newValue in
            guard let newValue else { return }
            navigate(to: newValue)
            appState.pendingNavigation = nil
        }
        .onChange(of: appState.pendingSharedPaths) { _, newValue in
            guard !newValue.isEmpty else { return }
            navPath = []
        }
    }

    private var mainNav: some View {
        NavigationStack(path: $navPath) {
            uploadRoot
                .navigationDestination(for: Screen.self) { screen in
                    destination(for: screen)
                }
        }
    }

    private var uploadRoot: some View {
        let pending = appState.pendingSharedPaths
        let activeLabel = appState.settingsRepository.load().activeDestinationDisplayName()
        return UploadScreen(
            worker: appState.uploadQueueWorker,
            onOpenHistory: { navPath.append(.history) },
            onOpenSettings: { navPath.append(.settings) },
            onCopyToClipboard: copyToClipboard,
            onAutoShareUploadFinished: handleAutoShareUploadFinished,
            onInitialPathsConsumed: { appState.pendingSharedPaths = [] },
            initialPaths: pending.isEmpty ? nil : pending,
            activeDestinationLabel: activeLabel
        )
        .id(settingsRevision)
        .onAppear {
            if pending.isEmpty {
                // If app was opened by Share Extension, paths may be in app group before onOpenURL runs (e.g. cold start)
                let fromGroup = ShareGroup.consumePendingPaths()
                if !fromGroup.isEmpty {
                    appState.pendingSharedPaths = fromGroup
                }
            }
        }
    }

    @ViewBuilder
    private func destination(for screen: Screen) -> some View {
        switch screen {
        case .loading:
            EmptyView()
        case .upload:
            uploadRoot
        case .history:
            HistoryScreen(
                viewModel: HistoryViewModel(historyRepository: appState.historyRepository),
                onBack: { _ = navPath.popLast() },
                onCopyToClipboard: copyToClipboard
            )
        case .settings:
            SettingsHubScreen(
                settingsRepository: appState.settingsRepository,
                onBack: { _ = navPath.popLast() },
                onNavigateToS3: { navPath.append(.s3Config) },
                onNavigateToCustomUploader: { navPath.append(.customUploaderConfig) },
                onNavigateToAbout: { navPath.append(.about) }
            )
        case .s3Config:
            S3ConfigScreen(
                viewModel: S3ConfigViewModel(settingsRepository: appState.settingsRepository),
                onBack: { _ = navPath.popLast() }
            )
        case .customUploaderConfig:
            CustomUploaderConfigScreen(
                viewModel: CustomUploaderConfigViewModel(settingsRepository: appState.settingsRepository),
                onBack: { _ = navPath.popLast() }
            )
        case .about:
            AboutScreen()
        }
    }
}

private struct DestinationConfigPassphraseSheet: View {
    let sourceLabel: String
    let onImport: (String) -> Void
    let onCancel: () -> Void

    @State private var passphrase: String = ""

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Text(sourceLabel)
                        .font(.subheadline)
                    SecureField("Passphrase", text: $passphrase)
                        .textContentType(.password)
                }
            }
            .navigationTitle("Import .xsdc")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel", action: onCancel)
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Import") {
                        onImport(passphrase)
                    }
                    .disabled(passphrase.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }
}
