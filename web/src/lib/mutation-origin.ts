export function tryOrigin(value: string | null | undefined): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (!trimmed || trimmed === "null") return null;
  try {
    return new URL(trimmed).origin;
  } catch {
    try {
      return new URL(`https://${trimmed}`).origin;
    } catch {
      return null;
    }
  }
}

function addCandidateOrigins(
  allowed: Set<string>,
  candidate: string | null | undefined,
): void {
  if (!candidate) return;
  for (const part of candidate.split(",")) {
    const origin = tryOrigin(part);
    if (origin) allowed.add(origin);
  }
}

export function mutationAllowedOrigins(
  requestUrl: string,
  appOrigin: string,
  forwardedHost?: string | null,
  host?: string | null,
): Set<string> {
  const allowed = new Set<string>();
  addCandidateOrigins(allowed, requestUrl);
  addCandidateOrigins(allowed, appOrigin);
  addCandidateOrigins(allowed, forwardedHost);
  addCandidateOrigins(allowed, host);
  return allowed;
}

export function isAllowedMutationOrigin(
  origin: string | null,
  allowed: Set<string>,
  secFetchSite?: string | null,
  referer?: string | null,
): boolean {
  const normalized = tryOrigin(origin);
  if (normalized && allowed.has(normalized)) return true;
  if (
    !normalized &&
    secFetchSite === "same-origin" &&
    referer
  ) {
    const refererOrigin = tryOrigin(referer);
    return refererOrigin !== null && allowed.has(refererOrigin);
  }
  return false;
}
