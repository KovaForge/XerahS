export function tryOrigin(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    return new URL(value).origin;
  } catch {
    try {
      return new URL(`https://${value}`).origin;
    } catch {
      return null;
    }
  }
}

export function mutationAllowedOrigins(
  requestUrl: string,
  appOrigin: string,
  forwardedHost?: string | null,
  host?: string | null,
): Set<string> {
  const allowed = new Set<string>();
  for (const candidate of [requestUrl, appOrigin, forwardedHost, host]) {
    const origin = tryOrigin(candidate);
    if (origin) allowed.add(origin);
  }
  return allowed;
}

export function isAllowedMutationOrigin(
  origin: string | null,
  allowed: Set<string>,
  secFetchSite?: string | null,
  referer?: string | null,
): boolean {
  if (secFetchSite === "cross-site") return false;
  if (origin && origin !== "null" && allowed.has(origin)) return true;
  if (
    (!origin || origin === "null") &&
    secFetchSite === "same-origin" &&
    referer
  ) {
    const refererOrigin = tryOrigin(referer);
    return refererOrigin !== null && allowed.has(refererOrigin);
  }
  return false;
}
