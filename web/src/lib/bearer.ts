export function bearerAccessToken(
  authorizationHeader: string | null | undefined,
): string | null {
  const match = /^Bearer\s+(\S+)$/i.exec(authorizationHeader ?? "");
  return match?.[1] ?? null;
}

export function hasBearerAuthorization(
  authorizationHeader: string | null | undefined,
): boolean {
  return bearerAccessToken(authorizationHeader) !== null;
}
