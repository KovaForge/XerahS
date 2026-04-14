//
//  GlassUI.swift
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

private let xerahSPanelShape = RoundedRectangle(cornerRadius: 28, style: .continuous)

struct XerahSPageBackground: View {
    var body: some View {
        ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.06, green: 0.13, blue: 0.23),
                    Color(red: 0.10, green: 0.19, blue: 0.31),
                    Color(red: 0.18, green: 0.12, blue: 0.16)
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            Circle()
                .fill(Color(red: 0.33, green: 0.83, blue: 0.90).opacity(0.28))
                .frame(width: 360, height: 360)
                .blur(radius: 24)
                .offset(x: 140, y: -220)

            Circle()
                .fill(Color(red: 0.98, green: 0.63, blue: 0.34).opacity(0.18))
                .frame(width: 300, height: 300)
                .blur(radius: 40)
                .offset(x: -140, y: 260)

            Rectangle()
                .fill(.white.opacity(0.03))
                .overlay {
                    LinearGradient(
                        colors: [.clear, .white.opacity(0.10), .clear],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    )
                }
                .blendMode(.plusLighter)
        }
        .ignoresSafeArea()
    }
}

struct XerahSSectionIntro: View {
    let title: String
    var detail: String? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title.uppercased())
                .font(.caption.weight(.semibold))
                .tracking(1.2)
                .foregroundStyle(.white.opacity(0.78))

            if let detail, !detail.isEmpty {
                Text(detail)
                    .font(.footnote)
                    .foregroundStyle(.white.opacity(0.72))
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct XerahSGlassGroup<Content: View>: View {
    @ViewBuilder var content: () -> Content

    var body: some View {
        if #available(iOS 26.0, *) {
            GlassEffectContainer(spacing: 16) {
                content()
            }
        } else {
            content()
        }
    }
}

struct XerahSGlassCard<Content: View>: View {
    var padding: CGFloat = 18
    var alignment: Alignment = .leading
    @ViewBuilder var content: () -> Content

    var body: some View {
        let shape = xerahSPanelShape

        Group {
            if #available(iOS 26.0, *) {
                content()
                    .frame(maxWidth: .infinity, alignment: alignment)
                    .padding(padding)
                    .background(Color.white.opacity(0.001), in: shape)
                    .glassEffect(.regular, in: shape)
            } else {
                content()
                    .frame(maxWidth: .infinity, alignment: alignment)
                    .padding(padding)
                    .background(.ultraThinMaterial, in: shape)
                    .overlay(shape.stroke(.white.opacity(0.16), lineWidth: 1))
            }
        }
        .shadow(color: .black.opacity(0.15), radius: 24, y: 14)
    }
}

struct XerahSStatusPill: View {
    let text: String
    var accent: Color = Color(red: 0.33, green: 0.83, blue: 0.90)

    var body: some View {
        Text(text)
            .font(.caption.weight(.semibold))
            .foregroundStyle(.white.opacity(0.88))
            .padding(.horizontal, 12)
            .padding(.vertical, 8)
            .background(accent.opacity(0.18), in: Capsule())
            .overlay(Capsule().stroke(accent.opacity(0.42), lineWidth: 1))
    }
}

extension View {
    @ViewBuilder
    func xerahSGlassButton(prominent: Bool = false) -> some View {
        if #available(iOS 26.0, *) {
            if prominent {
                buttonStyle(.glassProminent)
            } else {
                buttonStyle(.glass)
            }
        } else {
            if prominent {
                buttonStyle(.borderedProminent)
            } else {
                buttonStyle(.bordered)
            }
        }
    }

    func xerahSInputChrome() -> some View {
        self
            .padding(.horizontal, 14)
            .padding(.vertical, 12)
            .background(.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .stroke(.white.opacity(0.12), lineWidth: 1)
            )
    }
}
