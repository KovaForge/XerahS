//
//  HistoryScreen.swift
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

struct HistoryScreen: View {
    @ObservedObject var viewModel: HistoryViewModel
    var onBack: () -> Void
    var onCopyToClipboard: (String) -> Void

    var body: some View {
        ZStack {
            XerahSPageBackground()

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    HStack {
                        Spacer()
                        Button("Refresh") { viewModel.refresh() }
                            .xerahSGlassButton()
                        Button("Clear") { _ = viewModel.clearAll() }
                            .xerahSGlassButton()
                    }

                    VStack(alignment: .leading, spacing: 8) {
                        Text("History")
                            .font(.system(size: 33, weight: .bold, design: .rounded))
                            .foregroundStyle(.white)
                        Text("Recent uploads stay searchable and copy-ready here.")
                            .font(.subheadline)
                            .foregroundStyle(.white.opacity(0.72))
                    }

                    XerahSGlassGroup {
                        XerahSGlassCard {
                            VStack(alignment: .leading, spacing: 12) {
                                XerahSSectionIntro(
                                    title: "Search",
                                    detail: "Filter by filename, URL, or host."
                                )
                                TextField("Search history", text: $viewModel.searchQuery)
                                    .textInputAutocapitalization(.never)
                                    .xerahSInputChrome()
                                    .foregroundStyle(.white)
                            }
                        }

                        if viewModel.filteredEntries.isEmpty {
                            XerahSGlassCard {
                                Text("No history entries matched your search.")
                                    .font(.subheadline)
                                    .foregroundStyle(.white.opacity(0.76))
                            }
                        } else {
                            ForEach(viewModel.filteredEntries) { entry in
                                HistoryEntryRow(
                                    entry: entry,
                                    onCopyUrl: { onCopyToClipboard(entry.url) },
                                    onDelete: { viewModel.deleteEntry(entry.id) }
                                )
                            }
                        }
                    }
                }
                .padding(.horizontal, 20)
                .padding(.top, 20)
                .padding(.bottom, 28)
            }
            .scrollIndicators(.hidden)
        }
        .onAppear { viewModel.refresh() }
    }
}

private struct HistoryEntryRow: View {
    let entry: HistoryEntry
    var onCopyUrl: () -> Void
    var onDelete: () -> Void

    var body: some View {
        XerahSGlassCard {
            VStack(alignment: .leading, spacing: 10) {
                Text(entry.fileName)
                    .font(.headline)
                    .foregroundStyle(.white)

                if !entry.url.isEmpty {
                    Text(entry.url)
                        .font(.caption)
                        .foregroundStyle(.white.opacity(0.78))
                        .lineLimit(3)
                }

                Text(entry.host)
                    .font(.caption2.weight(.semibold))
                    .foregroundStyle(.white.opacity(0.6))

                HStack {
                    if !entry.url.isEmpty {
                        Button("Copy URL", action: onCopyUrl)
                            .xerahSGlassButton(prominent: true)
                    }
                    Button("Delete", role: .destructive, action: onDelete)
                        .xerahSGlassButton()
                }
            }
        }
    }
}

final class HistoryViewModel: ObservableObject {
    @Published var filteredEntries: [HistoryEntry] = []
    @Published var searchQuery: String = "" {
        didSet { applyFilter() }
    }

    private let historyRepository: HistoryRepository
    private var allEntries: [HistoryEntry] = []
    private let maxItems = 100

    init(historyRepository: HistoryRepository) {
        self.historyRepository = historyRepository
    }

    func refresh() {
        allEntries = historyRepository.getRecentEntries(limit: maxItems)
        applyFilter()
    }

    private func applyFilter() {
        let q = searchQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        if q.isEmpty {
            filteredEntries = allEntries
        } else {
            let lower = q.lowercased()
            filteredEntries = allEntries.filter {
                $0.fileName.lowercased().contains(lower) ||
                $0.url.lowercased().contains(lower) ||
                $0.host.lowercased().contains(lower)
            }
        }
    }

    func clearAll() -> Int {
        let count = historyRepository.clearEntries()
        refresh()
        return count
    }

    @discardableResult
    func deleteEntry(_ id: Int64) -> Bool {
        let ok = historyRepository.deleteEntry(id: id)
        if ok { refresh() }
        return ok
    }
}
