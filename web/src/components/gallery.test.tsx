// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { Gallery } from "@/components/gallery";

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
