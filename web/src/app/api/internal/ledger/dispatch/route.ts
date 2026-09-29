import { requireCronAuthorization } from "@/lib/internal-auth";
import { dispatchLedgerBatch } from "@/lib/ledger/dispatcher";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

async function dispatch(request: Request) {
  return handleApi(request, async () => {
    requireCronAuthorization(request);
    return json(await dispatchLedgerBatch());
  });
}

export const GET = dispatch;
export const POST = dispatch;
