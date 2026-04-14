//
//  S3ConfigScreen.swift
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

struct S3RegionOption: Identifiable {
    let id: String
    let displayName: String
}

private let s3Regions: [S3RegionOption] = [
    .init(id: "us-east-1", displayName: "US East (N. Virginia)"),
    .init(id: "us-east-2", displayName: "US East (Ohio)"),
    .init(id: "us-west-1", displayName: "US West (N. California)"),
    .init(id: "us-west-2", displayName: "US West (Oregon)"),
    .init(id: "ap-south-1", displayName: "Asia Pacific (Mumbai)"),
    .init(id: "ap-southeast-1", displayName: "Asia Pacific (Singapore)"),
    .init(id: "ap-southeast-2", displayName: "Asia Pacific (Sydney)"),
    .init(id: "ap-northeast-1", displayName: "Asia Pacific (Tokyo)"),
    .init(id: "eu-central-1", displayName: "Europe (Frankfurt)"),
    .init(id: "eu-west-1", displayName: "Europe (Ireland)"),
    .init(id: "eu-west-2", displayName: "Europe (London)"),
    .init(id: "ca-central-1", displayName: "Canada (Central)"),
]

struct S3ConfigScreen: View {
    @ObservedObject var viewModel: S3ConfigViewModel
    var onBack: () -> Void

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
                        Text("Amazon S3")
                            .font(.system(size: 33, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                        Text("Match the same bucket, endpoint, and signing behavior as desktop.")
                            .font(.subheadline)
                            .foregroundStyle(.white.opacity(0.72))
                    }

                    XerahSGlassGroup {
                        if let err = viewModel.validationError {
                            XerahSGlassCard {
                                Text(err)
                                    .font(.subheadline.weight(.medium))
                                    .foregroundStyle(Color(red: 1.0, green: 0.82, blue: 0.82))
                            }
                        }

                        XerahSSectionIntro(
                            title: "Credentials",
                            detail: "These fields are required before the mobile app can sign S3 requests."
                        )

                        XerahSGlassCard {
                            VStack(alignment: .leading, spacing: 12) {
                                S3InputField(title: "Access Key ID") {
                                    TextField("Access Key ID", text: $viewModel.accessKeyId)
                                        .textInputAutocapitalization(.never)
                                        .xerahSInputChrome()
                                        .foregroundStyle(.white)
                                        .onChange(of: viewModel.accessKeyId) { _, _ in viewModel.clearValidationError() }
                                }

                                S3InputField(title: "Secret Access Key") {
                                    SecureField("Secret Access Key", text: $viewModel.secretAccessKey)
                                        .xerahSInputChrome()
                                        .foregroundStyle(.white)
                                        .onChange(of: viewModel.secretAccessKey) { _, _ in viewModel.clearValidationError() }
                                }

                                S3InputField(title: "Bucket Name") {
                                    TextField("Bucket Name", text: $viewModel.bucketName)
                                        .textInputAutocapitalization(.never)
                                        .xerahSInputChrome()
                                        .foregroundStyle(.white)
                                        .onChange(of: viewModel.bucketName) { _, _ in viewModel.clearValidationError() }
                                }
                            }
                        }

                        XerahSSectionIntro(
                            title: "Endpoint",
                            detail: "Configure AWS region, custom S3-compatible endpoints, and URL style."
                        )

                        XerahSGlassCard {
                            VStack(alignment: .leading, spacing: 12) {
                                S3InputField(title: "Region") {
                                    Picker("Region", selection: $viewModel.regionIndex) {
                                        ForEach(Array(s3Regions.enumerated()), id: \.offset) { index, option in
                                            Text(option.displayName).tag(index)
                                        }
                                    }
                                    .pickerStyle(.menu)
                                    .tint(.white)
                                    .xerahSInputChrome()
                                }

                                S3InputField(title: "Custom Endpoint") {
                                    TextField("https://minio.example.com", text: $viewModel.customEndpoint)
                                        .textInputAutocapitalization(.never)
                                        .keyboardType(.URL)
                                        .xerahSInputChrome()
                                        .foregroundStyle(.white)
                                }

                                Toggle("Use path-style endpoint URLs", isOn: $viewModel.usePathStyle)
                                    .tint(Color(red: 0.33, green: 0.83, blue: 0.90))
                                    .foregroundStyle(.white)

                                Text("Recommended for dotted bucket names and S3-compatible endpoints where TLS fails with virtual-host style URLs.")
                                    .font(.footnote)
                                    .foregroundStyle(.white.opacity(0.68))
                            }
                        }

                        XerahSSectionIntro(
                            title: "Delivery",
                            detail: "Choose how uploaded file URLs are exposed after the request succeeds."
                        )

                        XerahSGlassCard {
                            VStack(alignment: .leading, spacing: 12) {
                                Toggle("Use Custom Domain (CDN)", isOn: $viewModel.useCustomDomain)
                                    .tint(Color(red: 0.98, green: 0.63, blue: 0.34))
                                    .foregroundStyle(.white)

                                if viewModel.useCustomDomain {
                                    TextField("https://cdn.example.com", text: $viewModel.customDomain)
                                        .textInputAutocapitalization(.never)
                                        .keyboardType(.URL)
                                        .xerahSInputChrome()
                                        .foregroundStyle(.white)
                                }

                                Toggle("Signed payload", isOn: $viewModel.signedPayload)
                                    .tint(Color(red: 0.33, green: 0.83, blue: 0.90))
                                    .foregroundStyle(.white)

                                Text("Recommended. Signing the payload avoids 403 responses on stricter bucket policies.")
                                    .font(.footnote)
                                    .foregroundStyle(.white.opacity(0.68))

                                Toggle("Make uploads public (public-read ACL)", isOn: $viewModel.setPublicAcl)
                                    .tint(Color(red: 0.98, green: 0.63, blue: 0.34))
                                    .foregroundStyle(.white)
                            }
                        }

                        Button("Save") {
                            if viewModel.save() { onBack() }
                        }
                        .xerahSGlassButton(prominent: true)
                    }
                }
                .padding(.horizontal, 20)
                .padding(.top, 20)
                .padding(.bottom, 28)
            }
            .scrollIndicators(.hidden)
        }
        .onAppear { viewModel.load() }
    }
}

private struct S3InputField<Content: View>: View {
    let title: String
    @ViewBuilder var content: () -> Content

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title)
                .font(.caption.weight(.semibold))
                .foregroundStyle(.white.opacity(0.72))
            content()
        }
    }
}

final class S3ConfigViewModel: ObservableObject {
    @Published var accessKeyId: String = ""
    @Published var secretAccessKey: String = ""
    @Published var bucketName: String = ""
    @Published var regionIndex: Int = 0
    @Published var customEndpoint: String = ""
    @Published var usePathStyle: Bool = false
    @Published var useCustomDomain: Bool = false
    @Published var customDomain: String = ""
    @Published var signedPayload: Bool = true
    @Published var setPublicAcl: Bool = false
    @Published var validationError: String?

    private let settingsRepository: SettingsRepository

    init(settingsRepository: SettingsRepository) {
        self.settingsRepository = settingsRepository
    }

    func load() {
        let config = settingsRepository.loadS3Config()
        accessKeyId = config.accessKeyId
        secretAccessKey = config.secretAccessKey
        bucketName = config.bucketName
        customEndpoint = config.customEndpoint
        usePathStyle = config.usePathStyle
        useCustomDomain = config.useCustomDomain
        customDomain = config.customDomain
        signedPayload = config.signedPayload
        setPublicAcl = config.setPublicAcl
        regionIndex = s3Regions.firstIndex(where: { $0.id == config.region }) ?? 0
    }

    func save() -> Bool {
        let accessKey = accessKeyId.trimmingCharacters(in: .whitespacesAndNewlines)
        let secret = secretAccessKey.trimmingCharacters(in: .whitespacesAndNewlines)
        let bucket = bucketName.trimmingCharacters(in: .whitespacesAndNewlines)
        let region = s3Regions[safe: regionIndex]?.id ?? ""
        if accessKey.isEmpty { validationError = "Access Key is required"; return false }
        if secret.isEmpty { validationError = "Secret Key is required"; return false }
        if bucket.isEmpty { validationError = "Bucket name is required"; return false }
        if region.isEmpty { validationError = "Region is required"; return false }
        validationError = nil
        var config = S3Config()
        config.accessKeyId = accessKey
        config.secretAccessKey = secret
        config.bucketName = bucket
        config.region = region
        config.customEndpoint = customEndpoint.trimmingCharacters(in: .whitespacesAndNewlines)
        config.usePathStyle = usePathStyle
        config.useCustomDomain = useCustomDomain
        config.customDomain = customDomain.trimmingCharacters(in: .whitespacesAndNewlines)
        config.signedPayload = signedPayload
        config.setPublicAcl = setPublicAcl
        settingsRepository.saveS3Config(config)
        if settingsRepository.getDefaultDestinationInstanceId() == nil {
            settingsRepository.setDefaultDestinationInstanceId(kAmazonS3DestinationId)
        }
        return true
    }

    func clearValidationError() { validationError = nil }
}

private extension Array {
    subscript(safe index: Int) -> Element? {
        indices.contains(index) ? self[index] : nil
    }
}
