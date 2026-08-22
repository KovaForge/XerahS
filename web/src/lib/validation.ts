import { z } from "zod";

const blockedHostnames = new Set([
  "localhost",
  "localhost.localdomain",
  "0.0.0.0",
  "::",
  "::1",
]);
const blockedIpv4Ranges: Array<[number, number]> = [
  [0x00000000, 0x00ffffff],
  [0x0a000000, 0x0affffff],
  [0x64400000, 0x647fffff],
  [0x7f000000, 0x7fffffff],
  [0xa9fe0000, 0xa9feffff],
  [0xac100000, 0xac1fffff],
  [0xc0000000, 0xc00000ff],
  [0xc0000200, 0xc00002ff],
  [0xc0a80000, 0xc0a8ffff],
  [0xc6120000, 0xc613ffff],
  [0xc6336400, 0xc63364ff],
  [0xcb007100, 0xcb0071ff],
  [0xe0000000, 0xffffffff],
];

function ipv4Number(hostname: string): number | undefined {
  const parts = hostname.split(".");
  if (parts.length !== 4 || parts.some((part) => !/^\d{1,3}$/.test(part)))
    return undefined;
  const octets = parts.map(Number);
  if (octets.some((part) => part > 255)) return undefined;
  return octets.reduce((value, octet) => value * 256 + octet, 0) >>> 0;
}

function isBlockedHostname(hostname: string): boolean {
  const normalized = hostname
    .toLowerCase()
    .replace(/\.$/, "")
    .replace(/^\[|\]$/g, "");
  if (
    blockedHostnames.has(normalized) ||
    normalized.endsWith(".local") ||
    normalized.endsWith(".internal")
  )
    return true;
  if (normalized.includes(":")) {
    // Accept only IPv6 global-unicast space (2000::/3), excluding the
    // documentation prefix. This conservatively rejects mapped, multicast,
    // link-local, unique-local, loopback, unspecified, and reserved literals.
    return (
      !/^[23][0-9a-f]{0,3}:/.test(normalized) ||
      normalized.startsWith("2001:db8:")
    );
  }
  const address = ipv4Number(normalized);
  return (
    address !== undefined &&
    blockedIpv4Ranges.some(([start, end]) => address >= start && address <= end)
  );
}

export function normalizeMediaUrl(raw: string): string {
  if (/\p{Cc}/u.test(raw))
    throw new Error("Control characters are not allowed.");
  const url = new URL(raw);
  if (
    url.protocol !== "https:" ||
    url.username ||
    url.password ||
    !url.hostname ||
    isBlockedHostname(url.hostname)
  ) {
    throw new Error("Only public HTTPS URLs without credentials are allowed.");
  }
  url.hash = "";
  return url.toString();
}

export function titleFromFileName(fileName: string): string {
  const leaf = fileName.replaceAll("\\", "/").split("/").at(-1)?.trim() ?? "";
  if (
    !leaf ||
    leaf === "." ||
    leaf === ".." ||
    /\p{Cc}/u.test(leaf) ||
    leaf.length > 255
  ) {
    throw new Error("A valid leaf filename is required.");
  }
  const dot = leaf.lastIndexOf(".");
  const title = dot > 0 ? leaf.slice(0, dot) : leaf;
  if (!title.trim()) throw new Error("The filename must contain a title.");
  return title;
}

const mediaUrl = z
  .string()
  .max(8_192)
  .transform((value, context) => {
    try {
      return normalizeMediaUrl(value);
    } catch (error) {
      context.addIssue({
        code: "custom",
        message: error instanceof Error ? error.message : "Invalid URL.",
      });
      return z.NEVER;
    }
  });

export const publishSchema = z
  .object({
    url: mediaUrl,
    thumbnailUrl: mediaUrl.nullish().transform((value) => value ?? null),
    kind: z.enum(["screenshot", "screencast"]),
    fileName: z
      .string()
      .min(1)
      .max(255)
      .refine(
        (value) => !value.includes("/") && !value.includes("\\"),
        "A leaf filename is required.",
      ),
    capturedAt: z.iso.datetime({ offset: true }),
    host: z
      .string()
      .trim()
      .min(1)
      .max(255)
      .nullable()
      .optional()
      .transform((value) => value ?? null),
    contentType: z
      .string()
      .regex(/^(image|video)\/[a-z0-9.+-]+$/i)
      .max(127)
      .nullable()
      .optional()
      .transform((value) => value ?? null),
  })
  .strict();

export const clientItemIdSchema = z.uuid();
export const planSchema = z
  .object({ plan: z.enum(["monthly", "annual"]) })
  .strict();
export const monthSchema = z.string().regex(/^\d{4}-(0[1-9]|1[0-2])$/);

export function markdownImage(title: string, url: string): string {
  const safeTitle = title
    .replaceAll("\\", "\\\\")
    .replaceAll("[", "\\[")
    .replaceAll("]", "\\]");
  const safeUrl = url.replaceAll("(", "%28").replaceAll(")", "%29");
  return `![${safeTitle}](${safeUrl})`;
}
