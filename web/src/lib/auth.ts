import "server-only";

import { ApiError } from "@/lib/errors";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export interface AuthenticatedUser {
  id: string;
  email: string;
  aal: "aal1" | "aal2";
  sessionId: string | null;
  authenticatedAt: Date | null;
}

interface AuthRequirements {
  strong?: boolean;
  recent?: boolean;
  verifiedEmail?: boolean;
}

function claimString(
  claims: Record<string, unknown>,
  name: string,
): string | undefined {
  const value = claims[name];
  return typeof value === "string" ? value : undefined;
}

function latestStrongAuthentication(
  claims: Record<string, unknown>,
): Date | null {
  if (!Array.isArray(claims.amr)) return null;
  const timestamps = claims.amr
    .map((entry) =>
      typeof entry === "object" && entry !== null
        ? (entry as Record<string, unknown>)
        : {},
    )
    .filter((entry) => entry.method === "totp" || entry.method === "mfa")
    .map((entry) => entry.timestamp)
    .filter(
      (value): value is number =>
        typeof value === "number" && Number.isFinite(value),
    );
  if (timestamps.length === 0) return null;
  return new Date(Math.max(...timestamps) * 1_000);
}

export async function requireAuthenticatedUser(
  request?: Request,
  requirements: AuthRequirements = {},
): Promise<AuthenticatedUser> {
  const supabase = await createSupabaseServerClient(request);
  const { data, error } = await supabase.auth.getClaims();
  if (error || !data?.claims)
    throw new ApiError(401, "authentication_required", "Sign in is required.");

  const claims = data.claims as Record<string, unknown>;
  const id = claimString(claims, "sub");
  const email = claimString(claims, "email");
  const aal = claimString(claims, "aal") === "aal2" ? "aal2" : "aal1";
  const authenticatedAt = latestStrongAuthentication(claims);
  if (!id || !email)
    throw new ApiError(401, "authentication_required", "Sign in is required.");
  if (requirements.strong && aal !== "aal2") {
    throw new ApiError(
      403,
      "strong_auth_required",
      "Complete a strong-authentication challenge to continue.",
    );
  }
  if (
    requirements.recent &&
    (!authenticatedAt ||
      Date.now() - authenticatedAt.getTime() > 10 * 60 * 1_000)
  ) {
    throw new ApiError(
      403,
      "strong_auth_required",
      "Recent strong authentication is required.",
    );
  }
  if (requirements.verifiedEmail) {
    const { data: userData, error: userError } = await supabase.auth.getUser();
    if (userError || !userData.user?.email_confirmed_at) {
      throw new ApiError(
        403,
        "email_verification_required",
        "Verify your email address to continue.",
      );
    }
  }

  return {
    id,
    email,
    aal,
    sessionId: claimString(claims, "session_id") ?? null,
    authenticatedAt,
  };
}
