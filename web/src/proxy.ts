import { createServerClient } from "@supabase/ssr";
import { NextResponse, type NextRequest } from "next/server";

import {
  applyNoStore,
  applySecurityHeaders,
  isPersonalizedPath,
} from "@/lib/security-headers";

function nonce(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(18));
  return btoa(String.fromCharCode(...bytes));
}

export async function proxy(request: NextRequest) {
  const cspNonce = nonce();
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", cspNonce);
  const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const publishableKey = process.env.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY;

  let response = NextResponse.next({ request: { headers: requestHeaders } });
  if (supabaseUrl && publishableKey) {
    const supabase = createServerClient(supabaseUrl, publishableKey, {
      cookies: {
        getAll: () => request.cookies.getAll(),
        setAll: (values, authHeaders) => {
          for (const { name, value } of values)
            request.cookies.set(name, value);
          response = NextResponse.next({
            request: { headers: requestHeaders },
          });
          for (const { name, value, options } of values)
            response.cookies.set(name, value, options);
          for (const [name, value] of Object.entries(authHeaders))
            response.headers.set(name, value);
        },
      },
    });
    await supabase.auth.getClaims();
  }

  if (supabaseUrl)
    applySecurityHeaders(
      response.headers,
      cspNonce,
      supabaseUrl,
      process.env.APP_ENV === "production",
    );
  if (isPersonalizedPath(request.nextUrl.pathname)) {
    applyNoStore(response.headers);
  }
  return response;
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|avif)$).*)",
  ],
};
