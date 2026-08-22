import { describe, expect, it } from "vitest";

import {
  markdownImage,
  normalizeMediaUrl,
  publishSchema,
  titleFromFileName,
} from "@/lib/validation";

describe("media URL validation", () => {
  it("accepts public HTTPS and strips fragments", () => {
    expect(
      normalizeMediaUrl(
        "https://cdn.example.com/path/file.png?token=value#secret",
      ),
    ).toBe("https://cdn.example.com/path/file.png?token=value");
  });

  it.each([
    "http://example.com/file.png",
    "https://user:pass@example.com/file.png",
    "https://localhost/file.png",
    "https://127.0.0.1/file.png",
    "https://10.0.0.1/file.png",
    "https://metadata.local/file.png",
    "https://[::1]/file.png",
    "https://[fc00::1]/file.png",
    "https://[ff02::1]/file.png",
    "https://[2001:db8::1]/file.png",
  ])("rejects unsafe URL %s", (url) => {
    expect(() => normalizeMediaUrl(url)).toThrow();
  });

  it("rejects unexpected payload fields", () => {
    expect(
      publishSchema.safeParse({
        url: "https://example.com/a.png",
        kind: "screenshot",
        fileName: "a.png",
        capturedAt: "2026-08-22T08:00:00Z",
        ownerId: crypto.randomUUID(),
      }).success,
    ).toBe(false);
  });
});

describe("title and markdown derivation", () => {
  it("derives the title from a leaf filename", () =>
    expect(titleFromFileName("screenshot-2026-08-22.png")).toBe(
      "screenshot-2026-08-22",
    ));
  it("preserves dotfiles as usable titles", () =>
    expect(titleFromFileName(".capture")).toBe(".capture"));
  it("uses only the final filename segment", () =>
    expect(titleFromFileName("../secret.png")).toBe("secret"));
  it("escapes markdown syntax", () =>
    expect(markdownImage("a[b]", "https://example.com/a(b).png")).toBe(
      "![a\\[b\\]](https://example.com/a%28b%29.png)",
    ));
});
