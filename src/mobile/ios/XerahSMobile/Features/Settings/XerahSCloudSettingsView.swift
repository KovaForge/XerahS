//
//  XerahSCloudSettingsView.swift
//  XerahS Mobile (Swift)
//
//  XerahS - The Avalonia UI implementation of ShareX
//  Copyright (c) 2007-2026 ShareX Team.
//

import AuthenticationServices
import SwiftUI

@MainActor
final class XerahSCloudSettingsViewModel: NSObject, ObservableObject, ASWebAuthenticationPresentationContextProviding {
    @Published var account: XerahSCloudAccount?
    @Published var isWorking = false
    @Published var errorMessage: String?

    private let client: XerahSCloudClient
    private var authenticationSession: ASWebAuthenticationSession?

    init(client: XerahSCloudClient) {
        self.client = client
    }

    var isSignedIn: Bool { account != nil || client.hasStoredCredential }

    func restore() async {
        guard account == nil, client.hasStoredCredential else { return }
        await perform { self.account = try await self.client.restoreSession() }
    }

    func signIn() {
        let attempt = client.beginOAuth()
        errorMessage = nil
        isWorking = true
        let session = ASWebAuthenticationSession(
            url: attempt.authorizationURL,
            callbackURLScheme: XerahSCloudConfiguration.callbackScheme
        ) { [weak self] callbackURL, error in
            guard let self else { return }
            if let authError = error as? ASWebAuthenticationSessionError, authError.code == .canceledLogin {
                self.isWorking = false
                return
            }
            guard let callbackURL else {
                self.errorMessage = error?.localizedDescription ?? "Sign in did not return to XerahS."
                self.isWorking = false
                return
            }
            Task { @MainActor in
                await self.perform {
                    self.account = try await self.client.completeOAuth(callbackURL: callbackURL, attempt: attempt)
                }
            }
        }
        session.presentationContextProvider = self
        session.prefersEphemeralWebBrowserSession = true
        authenticationSession = session
        if !session.start() {
            errorMessage = "Sign in could not open the system authorization session."
            isWorking = false
        }
    }

    func refresh() async {
        await perform { self.account = try await self.client.account() }
    }

    func signOut() {
        authenticationSession?.cancel()
        authenticationSession = nil
        client.signOut()
        account = nil
        errorMessage = nil
        isWorking = false
        objectWillChange.send()
    }

    func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        return scenes.flatMap(\.windows).first(where: \.isKeyWindow) ?? ASPresentationAnchor()
    }

    private func perform(_ operation: @escaping () async throws -> Void) async {
        isWorking = true
        errorMessage = nil
        do { try await operation() }
        catch { errorMessage = error.localizedDescription }
        isWorking = false
        authenticationSession = nil
        objectWillChange.send()
    }
}

struct XerahSCloudSettingsView: View {
    @StateObject private var viewModel: XerahSCloudSettingsViewModel
    @State private var autoPublish = XerahSCloudSettings.automaticallyPublishesEligibleUploads

    init(client: XerahSCloudClient) {
        _viewModel = StateObject(wrappedValue: XerahSCloudSettingsViewModel(client: client))
    }

    var body: some View {
        Group {
            if let account = viewModel.account {
                LabeledContent("Account", value: "@\(account.slug)")
                LabeledContent("Publishing", value: account.canPublish ? "Available" : "Unavailable")
                LabeledContent("Plan", value: account.subscriptionStatus ?? account.trialStatus ?? "Unknown")
                Toggle("Automatically publish images and videos", isOn: Binding(
                    get: { autoPublish },
                    set: { value in autoPublish = value; XerahSCloudSettings.automaticallyPublishesEligibleUploads = value }
                ))
                Link(destination: XerahSCloudConfiguration.settingsURL) {
                    Label("Manage Cloud Account", systemImage: "arrow.up.right.square")
                }
                Button("Refresh Account") { Task { await viewModel.refresh() } }
                Button("Sign Out", role: .destructive) { viewModel.signOut() }
            } else if viewModel.isSignedIn {
                Text("Restoring your secure Cloud session…")
                    .foregroundStyle(.secondary)
                Button("Retry") { Task { await viewModel.restore() } }
            } else {
                Text("Sign in with your XerahS Cloud account to publish eligible image and video links and view remote history.")
                    .foregroundStyle(.secondary)
                Button("Sign In to XerahS Cloud") { viewModel.signIn() }
            }

            if viewModel.isWorking { ProgressView() }
            if let error = viewModel.errorMessage {
                Text(error).font(.caption).foregroundStyle(.red).textSelection(.enabled)
            }
        }
        .task { await viewModel.restore() }
    }
}
