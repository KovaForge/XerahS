const dayPattern = /^(\d{4})-(\d{2})-(\d{2})$/;
const boundaryWindow = 36 * 60 * 60 * 1_000;

function parseDay(day: string): { year: number; month: number; date: number } {
  const match = dayPattern.exec(day);
  if (!match) throw new RangeError("Invalid calendar day.");
  const year = Number(match[1]);
  const month = Number(match[2]);
  const date = Number(match[3]);
  const normalized = new Date(Date.UTC(year, month - 1, date));
  if (
    normalized.getUTCFullYear() !== year ||
    normalized.getUTCMonth() !== month - 1 ||
    normalized.getUTCDate() !== date
  ) {
    throw new RangeError("Invalid calendar day.");
  }
  return { year, month, date };
}

function nextDay(day: string): string {
  const { year, month, date } = parseDay(day);
  return new Date(Date.UTC(year, month - 1, date + 1))
    .toISOString()
    .slice(0, 10);
}

function formatterFor(timeZone: string): Intl.DateTimeFormat {
  return new Intl.DateTimeFormat("en-US-u-ca-iso8601", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  });
}

function localDayAt(instant: number, formatter: Intl.DateTimeFormat): string {
  const parts = Object.fromEntries(
    formatter
      .formatToParts(new Date(instant))
      .filter(
        (part) =>
          part.type === "year" || part.type === "month" || part.type === "day",
      )
      .map((part) => [part.type, part.value]),
  );
  return `${parts.year}-${parts.month}-${parts.day}`;
}

function firstInstantOfDay(
  day: string,
  formatter: Intl.DateTimeFormat,
): number {
  const { year, month, date } = parseDay(day);
  const nominal = Date.UTC(year, month - 1, date);
  let low = nominal - boundaryWindow;
  let high = nominal + boundaryWindow;
  if (localDayAt(low, formatter) >= day || localDayAt(high, formatter) < day)
    throw new RangeError(
      "The calendar day is outside the supported time-zone range.",
    );
  while (high - low > 1) {
    const middle = low + Math.floor((high - low) / 2);
    if (localDayAt(middle, formatter) < day) low = middle;
    else high = middle;
  }
  return high;
}

export function utcRangeForZonedDay(
  day: string,
  timeZone: string,
): { from: string; to: string } {
  const formatter = formatterFor(timeZone);
  const start = firstInstantOfDay(day, formatter);
  const end = firstInstantOfDay(nextDay(day), formatter);
  return {
    from: new Date(start).toISOString(),
    // The current RPC has an inclusive upper bound.
    to: new Date(end - 1).toISOString(),
  };
}
