import "server-only";

import { createServerClient } from "@supabase/ssr";
import { createClient } from "@supabase/supabase-js";
import { cookies } from "next/headers";

import { getPublicEnv, getServerEnv } from "@/lib/env";

export async function createSupabaseServerClient(request?: Request) {
  const cookieStore = await cookies();
  const publicEnv = getPublicEnv();
  const authorization = request?.headers.get("authorization");

  return createServerClient(
    publicEnv.NEXT_PUBLIC_SUPABASE_URL,
    publicEnv.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY,
    {
      global: authorization
        ? { headers: { Authorization: authorization } }
        : undefined,
      cookies: {
        getAll: () => cookieStore.getAll(),
        setAll: (values) => {
          try {
            for (const { name, value, options } of values)
              cookieStore.set(name, value, options);
          } catch {
            // Server Components cannot write cookies. proxy.ts performs refreshes.
          }
        },
      },
    },
  );
}

export function createServiceRoleClient() {
  const publicEnv = getPublicEnv();
  const serviceRoleKey = getServerEnv().SUPABASE_SERVICE_ROLE_KEY;
  if (!serviceRoleKey)
    throw new Error("SUPABASE_SERVICE_ROLE_KEY is not configured.");

  return createClient(publicEnv.NEXT_PUBLIC_SUPABASE_URL, serviceRoleKey, {
    auth: { autoRefreshToken: false, persistSession: false },
  });
}
