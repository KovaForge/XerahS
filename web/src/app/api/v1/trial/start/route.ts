import { requireAuthenticatedUser } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { enforceSameOriginMutation } from "@/lib/request";
import { json, pending } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface TrialResult {
  operationId: string;
  replicated: boolean;
  status: "trial_pending" | "active";
}

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    const result = await rpc<TrialResult>(
      await createSupabaseServerClient(request),
      "start_my_trial",
    );
    return result.replicated
      ? json({ status: result.status }, { status: 201 })
      : pending(result.operationId);
  });
}
