import { describe, expect, it } from "vitest";

import {
  applyNoStore,
  applySecurityHeaders,
  contentSecurityPolicy,
} from "@/lib/security-headers";

describe("security headers", () => {
  it("builds a nonce CSP with an explicit Supabase origin", () => {
    const policy = contentSecurityPolicy(
      "nonce-value",
      "https://project.supabase.co",
    );
    expect(policy).toContain(
      "script-src 'self' 'nonce-nonce-value' 'strict-dynamic'",
    );
    expect(policy).toContain(
      "https://project.supabase.co wss://project.supabase.co",
    );
    expect(policy).not.toContain("unsafe-eval");
  });

  it("applies production and no-store headers", () => {
    const headers = new Headers();
    applySecurityHeaders(headers, "abc", "https://project.supabase.co", true);
    applyNoStore(headers);
    expect(headers.get("x-frame-options")).toBe("DENY");
    expect(headers.get("strict-transport-security")).toContain(
      "includeSubDomains",
    );
    expect(headers.get("content-security-policy")).toContain("nonce-abc");
    expect(headers.get("cache-control")).toContain("no-store");
  });

  it("uses report-only CSP outside production", () => {
    const headers = new Headers();
    applySecurityHeaders(headers, "abc", "https://project.supabase.co", false);
    expect(headers.get("content-security-policy")).toBeNull();
    expect(headers.get("content-security-policy-report-only")).toContain(
      "nonce-abc",
    );
  });
});
