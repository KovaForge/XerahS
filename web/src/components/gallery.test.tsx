// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { Gallery } from "@/components/gallery";

const screenshot = {
  id: "item-1",
  clientItemId: "client-1",
  url: "https://media.example/original.png?signature=kept",
  thumbnailUrl: null,
  kind: "screenshot" as const,
  fileName: "original.png",
  title: "Original",
  capturedAt: "2026-08-25T11:00:00Z",
  publishedAt: "2026-08-25T11:00:01Z",
  host: "media.example",
  contentType: "image/png",
};

describe("gallery pagination", () => {
  afterEach(() => vi.restoreAllMocks());

  it("returns to the first page without reusing the second-page cursor", async () => {
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValueOnce(Response.json({ items: [], nextCursor: null }))
      .mockResolvedValueOnce(
        Response.json({ items: [], nextCursor: "second-page" }),
      );

    render(
      <Gallery
        initialItems={[]}
        initialNextCursor="second-page"
        slug="owner"
        timeZone="Australia/Perth"
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain(
      "cursor=second-page",
    );

    const previous = screen.getByRole("button", { name: "Previous" });
    await waitFor(() =>
      expect((previous as HTMLButtonElement).disabled).toBe(false),
    );
    fireEvent.click(previous);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
    expect(String(fetchMock.mock.calls[1]?.[0])).not.toContain("cursor=");
  });
});

describe("gallery previews", () => {
  afterEach(() => vi.restoreAllMocks());

  it("uses the original screenshot URL when no thumbnail is published", () => {
    const { container } = render(
      <Gallery
        initialItems={[screenshot]}
        initialNextCursor={null}
        slug="owner"
        timeZone="Australia/Perth"
      />,
    );

    const image = container.querySelector("img");
    expect(image?.getAttribute("src")).toBe(screenshot.url);
    expect(image?.getAttribute("loading")).toBe("lazy");
    expect(image?.getAttribute("decoding")).toBe("async");
    expect(image?.getAttribute("fetchpriority")).toBe("low");
    expect(image?.getAttribute("referrerpolicy")).toBe("no-referrer");
  });

  it("falls back from a failed thumbnail to the original screenshot", () => {
    const { container } = render(
      <Gallery
        initialItems={[
          { ...screenshot, thumbnailUrl: "https://thumbs.example/item.png" },
        ]}
        initialNextCursor={null}
        slug="owner"
        timeZone="Australia/Perth"
      />,
    );

    const image = container.querySelector("img");
    expect(image?.getAttribute("src")).toBe("https://thumbs.example/item.png");
    fireEvent.error(image!);
    expect(container.querySelector("img")?.getAttribute("src")).toBe(
      screenshot.url,
    );
    fireEvent.error(container.querySelector("img")!);
    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector(".placeholder")).not.toBeNull();
  });

  it("does not automatically load a screencast without a thumbnail", () => {
    const { container } = render(
      <Gallery
        initialItems={[{ ...screenshot, kind: "screencast" }]}
        initialNextCursor={null}
        slug="owner"
        timeZone="Australia/Perth"
      />,
    );

    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector(".placeholder")).not.toBeNull();
  });
});
