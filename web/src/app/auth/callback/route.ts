import { NextResponse } from "next/server";

import { createSupabaseServerClient } from "@/lib/supabase/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  const url = new URL(request.url);
  const code = url.searchParams.get("code");
  const requestedNext = url.searchParams.get("next") ?? "/settings";
  const next =
    requestedNext.startsWith("/") && !requestedNext.startsWith("//")
      ? requestedNext
      : "/settings";
  if (!code)
    return NextResponse.redirect(
      new URL("/auth?error=invalid_callback", url.origin),
    );

  const { error } = await (
    await createSupabaseServerClient(request)
  ).auth.exchangeCodeForSession(code);
  const response = NextResponse.redirect(
    new URL(error ? "/auth?error=verification_failed" : next, url.origin),
  );
  response.headers.set("Cache-Control", "private, no-store");
  response.headers.set("Referrer-Policy", "no-referrer");
  return response;
}
