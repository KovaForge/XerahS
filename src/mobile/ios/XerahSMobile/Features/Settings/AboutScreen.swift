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
    AboutLink(title: "Privacy Policy", url: URL(string: "https://getsharex.com/privacy-policy")!),
    AboutLink(title: "Donate", url: URL(string: "https://getsharex.com/donate")!)
]

private let socialLinks: [AboutLink] = [
    AboutLink(title: "X", url: URL(string: "https://x.com/ShareX")!),
    AboutLink(title: "Discord", url: URL(string: "https://discord.gg/ShareX")!),
    AboutLink(title: "Reddit", url: URL(string: "https://www.reddit.com/r/sharex")!)
]

struct AboutScreen: View {
    @Environment(\.openURL) private var openURL

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
        ZStack {
            XerahSPageBackground()

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    XerahSGlassCard(alignment: .center) {
                        VStack(spacing: 14) {
                            Image("Logo")
                                .resizable()
                                .scaledToFit()
                                .frame(width: 112, height: 112)
                                .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))

                            VStack(spacing: 6) {
                                Text("\(appName) \(appVersion)")
                                    .font(.system(size: 28, weight: .bold, design: .rounded))
                                    .foregroundStyle(.white)
                                    .multilineTextAlignment(.center)

                                Text("Copyright (c) 2007-2026 ShareX Team")
                                    .font(.footnote)
                                    .foregroundStyle(.white.opacity(0.68))
                                    .multilineTextAlignment(.center)
                            }
                        }
                    }

                    XerahSGlassGroup {
                        XerahSSectionIntro(
                            title: "Version",
                            detail: "Build information from the installed iOS bundle."
                        )

                        XerahSGlassCard {
                            VStack(spacing: 12) {
                                AboutInfoRow(title: "Version", value: appVersion)
                                AboutInfoRow(title: "Build", value: buildNumber)
                                AboutInfoRow(title: "Bundle ID", value: bundleIdentifier)
                                AboutInfoRow(title: "iOS", value: UIDevice.current.systemVersion)
                            }
                        }

                        XerahSSectionIntro(title: "Links")
                        ForEach(aboutLinks) { link in
                            Button {
                                openURL(link.url)
                            } label: {
                                AboutLinkRow(title: link.title, url: link.url)
                            }
                            .buttonStyle(.plain)
                        }

                        XerahSSectionIntro(title: "Social")
                        ForEach(socialLinks) { link in
                            Button {
                                openURL(link.url)
                            } label: {
                                AboutLinkRow(title: link.title, url: link.url)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
                .padding(.horizontal, 20)
                .padding(.top, 20)
                .padding(.bottom, 28)
            }
            .scrollIndicators(.hidden)
        }
    }
}

private struct AboutInfoRow: View {
    let title: String
    let value: String

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 16) {
            Text(title)
                .font(.subheadline.weight(.medium))
                .foregroundStyle(.white.opacity(0.72))

            Spacer(minLength: 12)

            Text(value)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(.white)
                .multilineTextAlignment(.trailing)
                .textSelection(.enabled)
        }
    }
}

private struct AboutLinkRow: View {
    let title: String
    let url: URL

    var body: some View {
        XerahSGlassCard {
            HStack(spacing: 14) {
                VStack(alignment: .leading, spacing: 6) {
                    Text(title)
                        .font(.headline)
                        .foregroundStyle(.white)

                    Text(url.absoluteString)
                        .font(.footnote)
                        .foregroundStyle(.white.opacity(0.68))
                        .lineLimit(2)
                        .multilineTextAlignment(.leading)
                }

                Spacer(minLength: 12)

                Image(systemName: "arrow.up.forward.square")
                    .font(.footnote.weight(.bold))
                    .foregroundStyle(.white.opacity(0.65))
            }
        }
    }
}
