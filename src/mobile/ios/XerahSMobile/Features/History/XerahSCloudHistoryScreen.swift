//
//  XerahSCloudHistoryScreen.swift
//  XerahS Mobile (Swift)
//
//  XerahS - The Avalonia UI implementation of ShareX
//  Copyright (c) 2007-2026 ShareX Team.
//

import SwiftUI
import UIKit

@MainActor
final class XerahSCloudHistoryViewModel: ObservableObject {
    @Published var items: [XerahSCloudGalleryItem] = []
    @Published var isLoading = false
    @Published var errorMessage: String?
    @Published var nextCursor: String?

    private let client: XerahSCloudClient

    init(client: XerahSCloudClient) { self.client = client }

    var isSignedIn: Bool { client.hasStoredCredential }

    func refresh() async {
        guard isSignedIn else { items = []; nextCursor = nil; return }
        await load(cursor: nil, replacing: true)
    }

    func loadMore() async {
        guard let nextCursor, !isLoading else { return }
        await load(cursor: nextCursor, replacing: false)
    }

    func unpublish(_ item: XerahSCloudGalleryItem) async {
        isLoading = true
        errorMessage = nil
        do {
            try await client.unpublish(clientItemID: item.clientItemId)
            items.removeAll { $0.id == item.id }
        } catch { errorMessage = error.localizedDescription }
        isLoading = false
    }

    private func load(cursor: String?, replacing: Bool) async {
        isLoading = true
        errorMessage = nil
        do {
            let page = try await client.history(cursor: cursor)
            items = replacing ? page.items : items + page.items
            nextCursor = page.nextCursor
        } catch { errorMessage = error.localizedDescription }
        isLoading = false
    }
}

struct XerahSCloudHistoryScreen: View {
    @StateObject private var viewModel: XerahSCloudHistoryViewModel
    @State private var pendingUnpublish: XerahSCloudGalleryItem?

    init(client: XerahSCloudClient) {
        _viewModel = StateObject(wrappedValue: XerahSCloudHistoryViewModel(client: client))
    }

    var body: some View {
        List {
            if !viewModel.isSignedIn {
                ContentUnavailableView("Cloud Sign-In Required", systemImage: "cloud", description: Text("Sign in from Settings to view remote Cloud history."))
            } else if viewModel.items.isEmpty && !viewModel.isLoading {
                ContentUnavailableView("No Cloud History", systemImage: "cloud", description: Text("Published image and video uploads will appear here."))
            } else {
                ForEach(viewModel.items) { item in
                    VStack(alignment: .leading, spacing: 8) {
                        Text(item.fileName).font(.headline)
                        Text(item.url).font(.caption).foregroundStyle(.secondary).lineLimit(3)
                        Text(item.kind == "screencast" ? "Video" : "Image").font(.caption2.weight(.semibold)).foregroundStyle(.secondary)
                        HStack {
                            if let url = URL(string: item.url) { Link("Open", destination: url) }
                            Button("Copy") { UIPasteboard.general.string = item.url }
                            Button("Unpublish", role: .destructive) { pendingUnpublish = item }
                        }.buttonStyle(.borderless)
                    }
                }
                if viewModel.nextCursor != nil {
                    Button("Load More") { Task { await viewModel.loadMore() } }
                        .disabled(viewModel.isLoading)
                }
            }
            if let error = viewModel.errorMessage {
                Text(error).font(.caption).foregroundStyle(.red).textSelection(.enabled)
            }
        }
        .navigationTitle("Cloud History")
        .refreshable { await viewModel.refresh() }
        .overlay { if viewModel.isLoading && viewModel.items.isEmpty { ProgressView() } }
        .task { await viewModel.refresh() }
        .confirmationDialog("Unpublish this item?", isPresented: Binding(
            get: { pendingUnpublish != nil }, set: { if !$0 { pendingUnpublish = nil } }
        ), titleVisibility: .visible) {
            Button("Unpublish", role: .destructive) {
                guard let item = pendingUnpublish else { return }
                pendingUnpublish = nil
                Task { await viewModel.unpublish(item) }
            }
            Button("Cancel", role: .cancel) { pendingUnpublish = nil }
        } message: {
            Text("The public Cloud gallery entry will be removed. The original upload remains at its host.")
        }
    }
}
