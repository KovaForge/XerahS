import { z } from "zod";

import { ApiError } from "@/lib/errors";

export const authorizationIdSchema = z
  .string()
  .min(16)
  .max(512)
  .regex(/^[A-Za-z0-9._~-]+$/);

export const desktopOAuthScopes = ["openid", "email", "profile"] as const;

export interface OAuthAuthorizationDetails {
  authorization_id: string;
  redirect_uri: string;
  client: { id: string; name: string };
  user: { id: string; email: string };
  scope: string;
}

export interface DesktopOAuthExpectation {
  clientId: string;
  redirectUri: string;
  userId: string;
}

function invalidAuthorization(): never {
  throw new ApiError(
    403,
    "forbidden",
    "The OAuth authorization request is not permitted.",
  );
}

function exactScopes(scope: string): boolean {
  const values = scope.trim().split(/\s+/).filter(Boolean);
  const requested = new Set(values);
  return (
    values.length === desktopOAuthScopes.length &&
    requested.size === desktopOAuthScopes.length &&
    desktopOAuthScopes.every((value) => requested.has(value))
  );
}

function exactRedirectUri(actual: string, expected: string): boolean {
  try {
    const actualUrl = new URL(actual);
    const expectedUrl = new URL(expected);
    return (
      actualUrl.protocol === "https:" &&
      actualUrl.href === expectedUrl.href &&
      !actualUrl.username &&
      !actualUrl.password &&
      !actualUrl.search &&
      !actualUrl.hash
    );
  } catch {
    return false;
  }
}

export function assertDesktopAuthorization(
  details: OAuthAuthorizationDetails,
  expected: DesktopOAuthExpectation,
): void {
  if (
    details.client.id !== expected.clientId ||
    details.user.id !== expected.userId ||
    details.authorization_id.length === 0 ||
    !exactRedirectUri(details.redirect_uri, expected.redirectUri) ||
    !exactScopes(details.scope)
  ) {
    invalidAuthorization();
  }
}

export function assertDesktopOAuthRedirect(
  redirectUrl: string,
  expectedRedirectUri: string,
): URL {
  let actual: URL;
  let expected: URL;
  try {
    actual = new URL(redirectUrl);
    expected = new URL(expectedRedirectUri);
  } catch {
    return invalidAuthorization();
  }
  if (
    actual.protocol !== "https:" ||
    actual.origin !== expected.origin ||
    actual.pathname !== expected.pathname ||
    actual.username ||
    actual.password ||
    actual.hash
  ) {
    return invalidAuthorization();
  }
  const state = actual.searchParams.getAll("state");
  const code = actual.searchParams.getAll("code");
  const error = actual.searchParams.getAll("error");
  const errorDescription = actual.searchParams.getAll("error_description");
  const allowedKeys = new Set(["state", "code", "error", "error_description"]);
  if (
    [...actual.searchParams.keys()].some((key) => !allowedKeys.has(key)) ||
    state.length !== 1 ||
    state[0]?.length === 0 ||
    (state[0]?.length ?? 0) > 1024 ||
    (code.length === 1) === (error.length === 1) ||
    code.length > 1 ||
    error.length > 1 ||
    (code[0]?.length ?? error[0]?.length ?? 0) === 0 ||
    (code[0]?.length ?? 0) > 4096 ||
    (error[0] !== undefined && !/^[A-Za-z0-9_]{1,128}$/.test(error[0])) ||
    (code.length === 1 && errorDescription.length !== 0) ||
    errorDescription.length > 1 ||
    (errorDescription[0]?.length ?? 0) > 512
  ) {
    return invalidAuthorization();
  }
  return actual;
}
