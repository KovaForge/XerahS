import { describe, expect, it } from "vitest";

import { bearerAccessToken, hasBearerAuthorization } from "@/lib/bearer";
import {
  isAllowedMutationOrigin,
  mutationAllowedOrigins,
} from "@/lib/mutation-origin";

describe("mutation origin checks", () => {
  it("accepts the browser origin when it matches the request host", () => {
    const allowed = mutationAllowedOrigins(
      "https://xerahs-cloud-staging.vercel.app/api/oauth/decision",
      "https://staging.xerahs.com",
      "staging.xerahs.com",
      "xerahs-cloud-staging.vercel.app",
    );
    expect(isAllowedMutationOrigin("https://staging.xerahs.com", allowed)).toBe(
      true,
    );
    expect(
      isAllowedMutationOrigin(
        "https://xerahs-cloud-staging.vercel.app",
        allowed,
      ),
    ).toBe(true);
  });

  it("rejects a foreign origin even if the request host is trusted", () => {
    const allowed = mutationAllowedOrigins(
      "https://staging.xerahs.com/api/oauth/decision",
      "https://staging.xerahs.com",
    );
    expect(isAllowedMutationOrigin("https://evil.example", allowed)).toBe(
      false,
    );
    expect(
      isAllowedMutationOrigin("https://evil.example", allowed, "cross-site"),
    ).toBe(false);
  });

  it("accepts a matching origin even when Sec-Fetch-Site is cross-site", () => {
    const allowed = mutationAllowedOrigins(
      "https://xerahs-cloud-staging.vercel.app/api/oauth/decision",
      "https://staging.xerahs.com",
      "staging.xerahs.com, xerahs-cloud-staging.vercel.app",
      "xerahs-cloud-staging.vercel.app",
    );
    expect(
      isAllowedMutationOrigin(
        "https://staging.xerahs.com",
        allowed,
        "cross-site",
      ),
    ).toBe(true);
    expect(
      isAllowedMutationOrigin(
        "https://staging.xerahs.com/",
        allowed,
        "cross-site",
      ),
    ).toBe(true);
  });

  it("allows a same-origin form POST that omitted Origin", () => {
    const allowed = mutationAllowedOrigins(
      "https://staging.xerahs.com/api/oauth/decision",
      "https://staging.xerahs.com",
    );
    expect(
      isAllowedMutationOrigin(
        null,
        allowed,
        "same-origin",
        "https://staging.xerahs.com/oauth/consent?authorization_id=abc",
      ),
    ).toBe(true);
  });

  it("extracts a bearer access token", () => {
    expect(hasBearerAuthorization("Bearer abc.def.ghi")).toBe(true);
    expect(bearerAccessToken("Bearer abc.def.ghi")).toBe("abc.def.ghi");
    expect(hasBearerAuthorization("Basic abc")).toBe(false);
    expect(bearerAccessToken(null)).toBeNull();
  });

  it("rejects a missing origin when Sec-Fetch-Site is cross-site", () => {
    const allowed = mutationAllowedOrigins(
      "https://staging.xerahs.com/api/oauth/decision",
      "https://staging.xerahs.com",
    );
    expect(
      isAllowedMutationOrigin(
        null,
        allowed,
        "cross-site",
        "https://staging.xerahs.com/oauth/consent",
      ),
    ).toBe(false);
  });
});
