import { requireAuthenticatedUser } from "@/lib/auth";
import { getAccountSummary } from "@/lib/database";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  return handleApi(request, async () => {
    const user = await requireAuthenticatedUser(request);
    const summary = await getAccountSummary(
      await createSupabaseServerClient(request),
    );
    return json({ ...summary, strongAuth: user.aal === "aal2" });
  });
}
