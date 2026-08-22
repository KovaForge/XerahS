import { describe, expect, it } from "vitest";

import {
  assertDesktopAuthorization,
  assertDesktopOAuthRedirect,
  type OAuthAuthorizationDetails,
} from "@/lib/oauth-validation";

const expected = {
  clientId: "ad6062b4-3ab9-4b97-9cde-05038e4cc885",
  redirectUri: "https://xerahs.com/auth/desktop/callback",
  userId: "3443a3bf-b72e-4ae2-a1c6-d5c01ab32579",
};

function details(
  changes: Partial<OAuthAuthorizationDetails> = {},
): OAuthAuthorizationDetails {
  return {
    authorization_id: "valid-authorization-id",
    redirect_uri: expected.redirectUri,
    client: { id: expected.clientId, name: "XerahS Desktop" },
    user: { id: expected.userId, email: "owner@example.com" },
    scope: "openid email profile",
    ...changes,
  };
}

describe("desktop OAuth consent validation", () => {
  it("accepts only the configured client, user, relay, and exact scopes", () => {
    expect(() => assertDesktopAuthorization(details(), expected)).not.toThrow();
    expect(() =>
      assertDesktopAuthorization(
        details({ scope: "profile openid email" }),
        expected,
      ),
    ).not.toThrow();
  });

  it.each([
    details({ client: { id: crypto.randomUUID(), name: "Other" } }),
    details({ user: { id: crypto.randomUUID(), email: "owner@example.com" } }),
    details({ redirect_uri: "https://evil.example/callback" }),
    details({ redirect_uri: `${expected.redirectUri}?next=evil` }),
    details({ scope: "openid email profile phone" }),
    details({ scope: "openid email email" }),
  ])("rejects mismatched authorization details", (authorization) => {
    expect(() =>
      assertDesktopAuthorization(authorization, expected),
    ).toThrowError("not permitted");
  });

  it("restricts OAuth redirects to the exact desktop relay", () => {
    expect(
      assertDesktopOAuthRedirect(
        `${expected.redirectUri}?code=one&state=two`,
        expected.redirectUri,
      ).pathname,
    ).toBe("/auth/desktop/callback");
    expect(
      assertDesktopOAuthRedirect(
        `${expected.redirectUri}?error=access_denied&error_description=Denied&state=two`,
        expected.redirectUri,
      ).searchParams.get("error"),
    ).toBe("access_denied");
    expect(() =>
      assertDesktopOAuthRedirect(
        "https://evil.example/callback?code=one",
        expected.redirectUri,
      ),
    ).toThrowError("not permitted");
    expect(() =>
      assertDesktopOAuthRedirect(
        `${expected.redirectUri}?code=one`,
        expected.redirectUri,
      ),
    ).toThrowError("not permitted");
    expect(() =>
      assertDesktopOAuthRedirect(
        `${expected.redirectUri}?code=one&state=two&next=https://evil.example`,
        expected.redirectUri,
      ),
    ).toThrowError("not permitted");
  });
});
