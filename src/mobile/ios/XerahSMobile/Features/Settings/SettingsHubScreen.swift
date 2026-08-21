//
//  SettingsHubScreen.swift
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

struct SettingsHubScreen: View {
    let settingsRepository: SettingsRepository

    @State private var config: ApplicationConfig = ApplicationConfig()
    @State private var convertHeicToPng: Bool = true
    @State private var selectedDestinationId: String? = nil

    var body: some View {
        Form {
            Section {
                activeDestinationSection
            } header: {
                Text("Active Upload Destination")
            } footer: {
                Text("The selected destination is used by share uploads.")
            }

            Section("Upload Options") {
                Toggle(isOn: Binding(
                    get: { convertHeicToPng },
                    set: { newValue in
                        convertHeicToPng = newValue
                        settingsRepository.setConvertHeicToPng(newValue)
                    }
                )) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Convert HEIC/HEIF to PNG")
                        Text("Use PNG so images open directly in browsers.")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }

            Section("Destinations") {
                NavigationLink(value: Screen.s3Config) {
                    SettingsNavigationRow(
                        title: "Amazon S3",
                        subtitle: config.s3Config.isConfigured
                            ? "Bucket: \(config.s3Config.bucketName)"
                            : "Not configured",
                        systemImage: "shippingbox"
                    )
                }

                NavigationLink(value: Screen.customUploaderConfig) {
                    SettingsNavigationRow(
                        title: "Custom Uploader",
                        subtitle: config.customUploaders.isEmpty
                            ? "Not configured"
                            : "\(config.customUploaders.count) uploader(s)",
                        systemImage: "curlybraces"
                    )
                }
            }

            Section("Application") {
                NavigationLink(value: Screen.about) {
                    SettingsNavigationRow(
                        title: "About XerahS",
                        subtitle: "Version, build, and project links",
                        systemImage: "info.circle"
                    )
                }
            }
        }
        .navigationTitle("Settings")
        .onAppear(perform: reloadFromDisk)
        .onReceive(NotificationCenter.default.publisher(for: .xerahSSettingsDidChange)) { _ in
            reloadFromDisk()
        }
    }

    private var activeDestinationSection: some View {
        let options = config.selectableDestinations()
        let effectiveId = selectedDestinationId ?? config.defaultDestinationInstanceId ?? options.first?.instanceId

        return Group {
            if options.isEmpty {
                Text("No destination configured yet. Set up Amazon S3 or import a custom uploader below.")
                    .foregroundStyle(.secondary)
            } else {
                ForEach(Array(options.enumerated()), id: \.offset) { _, pair in
                    let isSelected = effectiveId == pair.instanceId
                    Button {
                        selectedDestinationId = pair.instanceId
                        settingsRepository.setDefaultDestinationInstanceId(pair.instanceId)
                    } label: {
                        HStack {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(pair.displayName)
                                Text(isSelected ? "Active destination" : "Tap to make active")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                            }

                            Spacer()
                            if isSelected {
                                Image(systemName: isSelected ? "checkmark.circle.fill" : "circle")
                                    .foregroundStyle(.green)
                            }
                        }
                    }
                    .buttonStyle(.plain)
                }
            }
        }
    }

    private func reloadFromDisk() {
        let fresh = settingsRepository.load()
        config = fresh
        convertHeicToPng = fresh.convertHeicToPng
        selectedDestinationId = fresh.defaultDestinationInstanceId ?? fresh.selectableDestinations().first?.instanceId
    }
}

private struct SettingsNavigationRow: View {
    let title: String
    let subtitle: String
    let systemImage: String

    var body: some View {
        Label {
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                Text(subtitle)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        } icon: {
            Image(systemName: systemImage)
                .foregroundStyle(.blue)
        }
    }
}
