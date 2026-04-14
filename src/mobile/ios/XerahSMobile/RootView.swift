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

struct RootView: View {
    @EnvironmentObject var appState: AppState
    @State private var phase: AppPhase = .loading
    @State private var navPath: [Screen] = []
    @State private var transientBanner: String?
    @State private var settingsRevision: Int = 0

    private func copyToClipboard(_ text: String) {
        UIPasteboard.general.string = text
        showBanner("Copied to clipboard")
    }

    private func showBanner(_ text: String) {
        transientBanner = text
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) {
            if transientBanner == text {
                transientBanner = nil
            }
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
            if let banner = transientBanner {
                XerahSGlassCard(padding: 0) {
                    Text(banner)
                        .font(.subheadline.weight(.medium))
                        .foregroundStyle(.white)
                        .padding(.horizontal, 20)
                        .padding(.vertical, 12)
                }
                .frame(maxWidth: 340)
                    .padding(.bottom, 32)
                    .transition(.move(edge: .bottom).combined(with: .opacity))
            }
        }
        .animation(.easeInOut(duration: 0.25), value: transientBanner)
        .onReceive(NotificationCenter.default.publisher(for: .xerahSSettingsDidChange)) { _ in
            settingsRevision += 1
        }
        .onAppear {
            if let banner = appState.bannerMessage, !banner.isEmpty {
                showBanner(banner)
                appState.bannerMessage = nil
            }
            if let pending = appState.pendingNavigation {
                navigate(to: pending)
                appState.pendingNavigation = nil
            }
        }
        .onChange(of: appState.bannerMessage) { _, newValue in
            guard let newValue, !newValue.isEmpty else { return }
            showBanner(newValue)
            appState.bannerMessage = nil
        }
        .onChange(of: appState.pendingNavigation) { _, newValue in
            guard let newValue else { return }
            navigate(to: newValue)
            appState.pendingNavigation = nil
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
            initialPaths: pending.isEmpty ? nil : pending,
            activeDestinationLabel: activeLabel
        )
        .id(settingsRevision)
        .onAppear {
            if !pending.isEmpty {
                appState.pendingSharedPaths = []
            } else {
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
                onNavigateToCustomUploader: { navPath.append(.customUploaderConfig) }
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
        }
    }
}
