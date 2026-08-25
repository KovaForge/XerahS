"use client";

import { useCallback, useEffect, useState } from "react";

import type { GalleryItem } from "@/lib/database";
import { utcRangeForZonedDay } from "@/lib/time-zone";
import { markdownImage } from "@/lib/validation";

interface GalleryProps {
  initialItems: GalleryItem[];
  initialNextCursor: string | null;
  slug: string;
  timeZone: string;
}
interface CalendarCount {
  day: string;
  count: number;
}

function GalleryPreview({ item }: { item: GalleryItem }) {
  const sources = [
    item.thumbnailUrl,
    item.kind === "screenshot" ? item.url : null,
  ].filter(
    (source, index, values): source is string =>
      Boolean(source) && values.indexOf(source) === index,
  );
  const [sourceIndex, setSourceIndex] = useState(0);
  const source = sources[sourceIndex];

  if (!source) {
    return (
      <span aria-hidden="true" className="placeholder">
        {item.kind === "screenshot" ? "▧" : "▶"}
      </span>
    );
  }

  return (
    <>
      {/* Fetch owner media in the viewer's browser. Server-side optimization would proxy untrusted URLs. */}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        alt=""
        decoding="async"
        fetchPriority="low"
        loading="lazy"
        onError={() => setSourceIndex((index) => index + 1)}
        referrerPolicy="no-referrer"
        src={source}
      />
    </>
  );
}

export function Gallery({
  initialItems,
  initialNextCursor,
  slug,
  timeZone,
}: GalleryProps) {
  const [items, setItems] = useState(initialItems);
  const [nextCursor, setNextCursor] = useState(initialNextCursor);
  const [currentCursor, setCurrentCursor] = useState<string | null>(null);
  const [cursorHistory, setCursorHistory] = useState<Array<string | null>>([]);
  const [activeDay, setActiveDay] = useState<string | null>(null);
  const [kind, setKind] = useState("all");
  const [view, setView] = useState<"grid" | "calendar">("grid");
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));
  const [calendar, setCalendar] = useState<CalendarCount[]>([]);
  const [message, setMessage] = useState("");

  const load = useCallback(
    async (
      cursor: string | null = null,
      day: string | null = null,
      selectedKind = kind,
    ): Promise<boolean> => {
      const query = new URLSearchParams({ limit: "50" });
      if (selectedKind !== "all") query.set("kind", selectedKind);
      if (cursor) query.set("cursor", cursor);
      if (day) {
        const range = utcRangeForZonedDay(day, timeZone);
        query.set("from", range.from);
        query.set("to", range.to);
      }
      const response = await fetch(`/api/v1/items?${query}`, {
        cache: "no-store",
      });
      if (!response.ok) {
        setMessage("Could not load the gallery.");
        return false;
      }
      const page = (await response.json()) as {
        items: GalleryItem[];
        nextCursor: string | null;
      };
      setItems(page.items);
      setNextCursor(page.nextCursor);
      setMessage("");
      return true;
    },
    [kind, timeZone],
  );

  useEffect(() => {
    if (view !== "calendar") return;
    void fetch(`/api/v1/items/calendar?month=${month}`, { cache: "no-store" })
      .then(async (response) =>
        response.ok
          ? (response.json() as Promise<{ days: CalendarCount[] }>)
          : Promise.reject(new Error()),
      )
      .then((value) => setCalendar(value.days))
      .catch(() => setMessage("Could not load the calendar."));
  }, [month, view]);

  async function copy(text: string) {
    await navigator.clipboard.writeText(text);
    setMessage("Copied to clipboard.");
  }
  async function changeKind(selectedKind: string) {
    setKind(selectedKind);
    if (await load(null, null, selectedKind)) {
      setCurrentCursor(null);
      setCursorHistory([]);
      setActiveDay(null);
    }
  }
  async function unpublish(item: GalleryItem) {
    if (
      !confirm(
        `Unpublish “${item.title}”? The destination file will not be deleted.`,
      )
    )
      return;
    const response = await fetch(`/api/v1/items/${item.clientItemId}`, {
      method: "DELETE",
      headers: { Origin: location.origin },
    });
    if (response.ok) {
      setItems((current) =>
        current.filter((candidate) => candidate.id !== item.id),
      );
      setMessage("Removed from your profile.");
    } else setMessage("Could not unpublish this item.");
  }

  const counts = new Map(calendar.map((entry) => [entry.day, entry.count]));
  const daysInMonth = new Date(`${month}-01T00:00:00Z`);
  const dayCount = new Date(
    Date.UTC(daysInMonth.getUTCFullYear(), daysInMonth.getUTCMonth() + 1, 0),
  ).getUTCDate();
  const leading = daysInMonth.getUTCDay();

  return (
    <section>
      <header className="gallery-header">
        <div>
          <p className="eyebrow">Owner-only profile</p>
          <h1>{slug}</h1>
        </div>
        <div className="toolbar">
          <label>
            <span className="sr-only">Media kind</span>
            <select
              onChange={(event) => void changeKind(event.target.value)}
              value={kind}
            >
              <option value="all">All media</option>
              <option value="screenshot">Screenshots</option>
              <option value="screencast">Screencasts</option>
            </select>
          </label>
          <button
            aria-pressed={view === "grid"}
            onClick={() => setView("grid")}
          >
            Grid
          </button>
          <button
            aria-pressed={view === "calendar"}
            onClick={() => setView("calendar")}
          >
            Calendar
          </button>
        </div>
      </header>
      <p aria-live="polite" className="status">
        {message}
      </p>
      {view === "calendar" && (
        <>
          <label>
            Month
            <input
              onChange={(event) => setMonth(event.target.value)}
              type="month"
              value={month}
            />
          </label>
          <div
            className="calendar"
            role="grid"
            aria-label={`Publishes in ${month}`}
          >
            {Array.from({ length: leading }, (_, index) => (
              <span aria-hidden="true" key={`blank-${index}`} />
            ))}
            {Array.from({ length: dayCount }, (_, index) => {
              const day = `${month}-${String(index + 1).padStart(2, "0")}`;
              const count = counts.get(day) ?? 0;
              return (
                <button
                  className="calendar-day"
                  disabled={!count}
                  key={day}
                  onClick={() => {
                    void (async () => {
                      if (await load(null, day)) {
                        setCurrentCursor(null);
                        setCursorHistory([]);
                        setActiveDay(day);
                        setView("grid");
                      }
                    })();
                  }}
                >
                  {index + 1}
                  {count > 0 && (
                    <span className="calendar-count">
                      {count} item{count === 1 ? "" : "s"}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </>
      )}
      {view === "grid" &&
        (items.length === 0 ? (
          <div className="empty">No published captures match this view.</div>
        ) : (
          <div className="gallery-grid">
            {items.map((item) => (
              <article className="tile" key={item.id}>
                <a
                  className="tile-media"
                  href={item.url}
                  rel="noopener noreferrer"
                  target="_blank"
                  aria-label={`Open ${item.title}`}
                >
                  <GalleryPreview item={item} />
                </a>
                <div className="tile-body">
                  <h2 className="tile-title">{item.title}</h2>
                  <p className="tile-meta">
                    {new Date(item.capturedAt).toLocaleString()}
                  </p>
                  <div className="tile-actions">
                    <button onClick={() => void copy(item.url)}>
                      Copy URL
                    </button>
                    <button
                      onClick={() =>
                        void copy(markdownImage(item.title, item.url))
                      }
                    >
                      Markdown
                    </button>
                    <a
                      className="button"
                      href={item.url}
                      rel="noopener noreferrer"
                      target="_blank"
                    >
                      Download
                    </a>
                    <button
                      className="danger"
                      onClick={() => void unpublish(item)}
                    >
                      Unpublish
                    </button>
                  </div>
                </div>
              </article>
            ))}
          </div>
        ))}
      {view === "grid" && (
        <nav className="pager" aria-label="Gallery pages">
          <button
            disabled={cursorHistory.length === 0}
            onClick={() =>
              void (async () => {
                const history = [...cursorHistory];
                const previous = history.at(-1) ?? null;
                if (await load(previous, activeDay)) {
                  history.pop();
                  setCursorHistory(history);
                  setCurrentCursor(previous);
                }
              })()
            }
          >
            Previous
          </button>
          <button
            disabled={!nextCursor}
            onClick={() =>
              void (async () => {
                if (!nextCursor) return;
                if (await load(nextCursor, activeDay)) {
                  setCursorHistory((history) => [...history, currentCursor]);
                  setCurrentCursor(nextCursor);
                }
              })()
            }
          >
            Next
          </button>
        </nav>
      )}
    </section>
  );
}
