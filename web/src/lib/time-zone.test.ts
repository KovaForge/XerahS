import { describe, expect, it } from "vitest";

import { utcRangeForZonedDay } from "@/lib/time-zone";

describe("profile time-zone calendar ranges", () => {
  it("uses the profile zone rather than UTC", () => {
    expect(utcRangeForZonedDay("2026-08-22", "Australia/Perth")).toEqual({
      from: "2026-08-21T16:00:00.000Z",
      to: "2026-08-22T15:59:59.999Z",
    });
  });

  it("honors daylight-saving day lengths", () => {
    const spring = utcRangeForZonedDay("2026-03-08", "America/New_York");
    const autumn = utcRangeForZonedDay("2026-11-01", "America/New_York");
    expect(Date.parse(spring.to) + 1 - Date.parse(spring.from)).toBe(
      23 * 60 * 60 * 1_000,
    );
    expect(Date.parse(autumn.to) + 1 - Date.parse(autumn.from)).toBe(
      25 * 60 * 60 * 1_000,
    );
  });

  it("rejects invalid days and time zones", () => {
    expect(() => utcRangeForZonedDay("2026-02-30", "UTC")).toThrow();
    expect(() => utcRangeForZonedDay("2026-02-28", "Mars/Olympus")).toThrow();
  });
});
