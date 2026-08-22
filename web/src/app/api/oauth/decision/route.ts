import { NextResponse } from "next/server";
import { z } from "zod";

import { requireAuthenticatedUser } from "@/lib/auth";
import { getServerEnv } from "@/lib/env";
import { ApiError } from "@/lib/errors";
import {
  assertDesktopAuthorization,
  assertDesktopOAuthRedirect,
  authorizationIdSchema,
} from "@/lib/oauth-validation";
import { enforceSameOriginMutation } from "@/lib/request";
import { handleApi } from "@/lib/route-handler";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const decisionSchema = z.object({
  authorization_id: authorizationIdSchema,
  decision: z.enum(["approve", "deny"]),
});

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const contentLength = Number(request.headers.get("content-length") ?? "0");
    if (Number.isFinite(contentLength) && contentLength > 2_048)
      throw new ApiError(413, "invalid_request", "The request is too large.");

    const input = decisionSchema.parse(
      Object.fromEntries((await request.formData()).entries()),
    );
    const user = await requireAuthenticatedUser(request, {
      strong: true,
      verifiedEmail: true,
    });
    const env = getServerEnv();
    const clientId = env.XERAHS_DESKTOP_OAUTH_CLIENT_ID;
    if (!clientId)
      throw new ApiError(
        503,
        "integration_unavailable",
        "Desktop authorization is unavailable.",
      );
    const redirectUri = new URL("/auth/desktop/callback", env.APP_ORIGIN).href;
    const supabase = await createSupabaseServerClient(request);
    const { data: details, error: detailsError } =
      await supabase.auth.oauth.getAuthorizationDetails(input.authorization_id);
    if (detailsError || !details || !("authorization_id" in details))
      throw new ApiError(
        400,
        "invalid_request",
        "The authorization request is invalid or expired.",
      );
    assertDesktopAuthorization(details, {
      clientId,
      redirectUri,
      userId: user.id,
    });

    const operation =
      input.decision === "approve"
        ? supabase.auth.oauth.approveAuthorization(input.authorization_id, {
            skipBrowserRedirect: true,
          })
        : supabase.auth.oauth.denyAuthorization(input.authorization_id, {
            skipBrowserRedirect: true,
          });
    const { data, error } = await operation;
    if (error || !data?.redirect_url)
      throw new ApiError(
        400,
        "invalid_request",
        "The authorization decision could not be completed.",
      );
    const target = assertDesktopOAuthRedirect(data.redirect_url, redirectUri);
    const response = NextResponse.redirect(target, 303);
    response.headers.set("Cache-Control", "private, no-store, max-age=0");
    response.headers.set("Referrer-Policy", "no-referrer");
    return response;
  });
}
