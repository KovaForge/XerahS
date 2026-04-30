//
//  AboutScreen.swift
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

private struct AboutLink: Identifiable {
    let id = UUID()
    let title: String
    let url: URL
}

private let aboutLinks: [AboutLink] = [
    AboutLink(title: "Website", url: URL(string: "https://xerahs.com")!),
    AboutLink(title: "GitHub Project", url: URL(string: "https://github.com/ShareX/XerahS")!),
    AboutLink(title: "Issues", url: URL(string: "https://github.com/ShareX/XerahS/issues/")!),
    AboutLink(title: "Contributors", url: URL(string: "https://github.com/ShareX/XerahS/graphs/contributors")!),
    AboutLink(title: "Changelog", url: URL(string: "https://xerahs.com/changelog.html")!),
    AboutLink(title: "Privacy Policy", url: URL(string: "https://getsharex.com/privacy-policy")!)
]

private let socialLinks: [AboutLink] = [
    AboutLink(title: "X", url: URL(string: "https://x.com/ShareX")!),
    AboutLink(title: "Discord", url: URL(string: "https://discord.gg/ShareX")!),
    AboutLink(title: "Reddit", url: URL(string: "https://www.reddit.com/r/sharex")!)
]

struct AboutScreen: View {
    private var appName: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String
            ?? Bundle.main.object(forInfoDictionaryKey: "CFBundleName") as? String
            ?? "XerahS"
    }

    private var appVersion: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "Unknown"
    }

    private var buildNumber: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "Unknown"
    }

    private var bundleIdentifier: String {
        Bundle.main.bundleIdentifier ?? "Unknown"
    }

    var body: some View {
        Form {
            Section {
                VStack(spacing: 12) {
                    Image("Logo")
                        .resizable()
                        .scaledToFit()
                        .frame(width: 96, height: 96)
                        .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))

                    Text(appName)
                        .font(.title2.weight(.semibold))

                    Text("Version \(appVersion)")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)

                    Text("Copyright (c) 2007-2026 ShareX Team")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                        .multilineTextAlignment(.center)
                }
                .frame(maxWidth: .infinity)
                .listRowBackground(Color.clear)
            }

            Section("Version") {
                AboutInfoRow(title: "Version", value: appVersion)
                AboutInfoRow(title: "Build", value: buildNumber)
                AboutInfoRow(title: "Bundle ID", value: bundleIdentifier)
                AboutInfoRow(title: "iOS", value: UIDevice.current.systemVersion)
            }

            Section("Links") {
                ForEach(aboutLinks) { link in
                    AboutLinkRow(title: link.title, url: link.url)
                }
            }

            Section("Social") {
                ForEach(socialLinks) { link in
                    AboutLinkRow(title: link.title, url: link.url)
                }
            }
        }
        .navigationTitle("About")
        .navigationBarTitleDisplayMode(.inline)
    }
}

private struct AboutInfoRow: View {
    let title: String
    let value: String

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 16) {
            Text(title)
                .font(.subheadline.weight(.medium))
                .foregroundStyle(.secondary)

            Spacer(minLength: 12)

            Text(value)
                .font(.subheadline.weight(.semibold))
                .multilineTextAlignment(.trailing)
                .textSelection(.enabled)
        }
    }
}

private struct AboutLinkRow: View {
    let title: String
    let url: URL

    var body: some View {
        Link(destination: url) {
            HStack(spacing: 12) {
                Image(systemName: "arrow.up.forward.square")
                    .foregroundStyle(.blue)

                VStack(alignment: .leading, spacing: 4) {
                    Text(title)
                    Text(url.absoluteString)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
            }
        }
    }
}
