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
    var onBack: () -> Void
    var onNavigateToS3: () -> Void
    var onNavigateToCustomUploader: () -> Void

    @State private var config: ApplicationConfig = ApplicationConfig()
    @State private var convertHeicToPng: Bool = true
    @State private var selectedDestinationId: String? = nil

    var body: some View {
        ZStack {
            XerahSPageBackground()

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    HStack {
                        Button("Back", action: onBack)
                            .xerahSGlassButton()
                        Spacer()
                    }

                    VStack(alignment: .leading, spacing: 8) {
                        Text("Settings")
                            .font(.system(size: 33, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                        Text("Adjust the mobile uploader without guessing which labels are interactive.")
                            .font(.subheadline)
                            .foregroundStyle(.white.opacity(0.72))
                    }

                    XerahSGlassGroup {
                        XerahSSectionIntro(
                            title: "Active Upload Destination",
                            detail: "This heading is informational only. Tap a glass row below to choose the destination used by share uploads."
                        )

                        activeDestinationSection

                        XerahSSectionIntro(
                            title: "Upload Options",
                            detail: "Image conversion applies before the upload request is built."
                        )

                        XerahSGlassCard {
                            Toggle(isOn: Binding(
                                get: { convertHeicToPng },
                                set: { newValue in
                                    convertHeicToPng = newValue
                                    settingsRepository.setConvertHeicToPng(newValue)
                                }
                            )) {
                                VStack(alignment: .leading, spacing: 6) {
                                    Text("Convert HEIC/HEIF to PNG before upload")
                                        .font(.headline)
                                        .foregroundStyle(.white)
                                    Text("Use PNG so images open directly in browsers instead of downloading as HEIC.")
                                        .font(.footnote)
                                        .foregroundStyle(.white.opacity(0.68))
                                }
                            }
                            .tint(Color(red: 0.33, green: 0.83, blue: 0.90))
                        }

                        XerahSSectionIntro(
                            title: "Destinations",
                            detail: "These rows are interactive. Tap one to configure or import destination settings."
                        )

                        Button(action: onNavigateToS3) {
                            SettingsNavigationCard(
                                title: "Amazon S3",
                                subtitle: config.s3Config.isConfigured
                                    ? "Bucket: \(config.s3Config.bucketName)"
                                    : "Not configured. Tap to set up."
                            )
                        }
                        .buttonStyle(.plain)

                        Button(action: onNavigateToCustomUploader) {
                            SettingsNavigationCard(
                                title: "Custom Uploader",
                                subtitle: config.customUploaders.isEmpty
                                    ? "Not configured. Tap to add or import."
                                    : "\(config.customUploaders.count) uploader(s) available."
                            )
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(.horizontal, 20)
                .padding(.top, 20)
                .padding(.bottom, 28)
            }
            .scrollIndicators(.hidden)
        }
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
                XerahSGlassCard {
                    Text("No destination configured yet. Set up Amazon S3 or import a custom uploader below.")
                        .font(.subheadline)
                        .foregroundStyle(.white.opacity(0.76))
                }
            } else {
                ForEach(Array(options.enumerated()), id: \.offset) { _, pair in
                    let isSelected = effectiveId == pair.instanceId
                    Button {
                        selectedDestinationId = pair.instanceId
                        settingsRepository.setDefaultDestinationInstanceId(pair.instanceId)
                    } label: {
                        XerahSGlassCard {
                            HStack(spacing: 14) {
                                VStack(alignment: .leading, spacing: 6) {
                                    Text(pair.displayName)
                                        .font(.headline)
                                        .foregroundStyle(.white)
                                    Text(isSelected ? "Active destination" : "Tap to make this the active destination")
                                        .font(.footnote)
                                        .foregroundStyle(.white.opacity(0.66))
                                }

                                Spacer(minLength: 12)

                                Image(systemName: isSelected ? "checkmark.circle.fill" : "circle")
                                    .font(.title3)
                                    .foregroundStyle(isSelected ? Color(red: 0.52, green: 0.95, blue: 0.72) : .white.opacity(0.62))
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

private struct SettingsNavigationCard: View {
    let title: String
    let subtitle: String

    var body: some View {
        XerahSGlassCard {
            HStack(spacing: 14) {
                VStack(alignment: .leading, spacing: 6) {
                    Text(title)
                        .font(.headline)
                        .foregroundStyle(.white)
                    Text(subtitle)
                        .font(.footnote)
                        .foregroundStyle(.white.opacity(0.68))
                }

                Spacer(minLength: 12)

                Image(systemName: "chevron.right")
                    .font(.footnote.weight(.bold))
                    .foregroundStyle(.white.opacity(0.65))
            }
        }
    }
}
