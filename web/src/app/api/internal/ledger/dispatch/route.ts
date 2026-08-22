import { timingSafeEqual } from "node:crypto";

import { ApiError } from "@/lib/errors";
import { getServerEnv } from "@/lib/env";
import { dispatchLedgerBatch } from "@/lib/ledger/dispatcher";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function authorized(request: Request): boolean {
  const expected = getServerEnv().CRON_SECRET;
  const actual = request.headers
    .get("authorization")
    ?.replace(/^Bearer\s+/i, "");
  if (!expected || !actual || expected.length !== actual.length) return false;
  return timingSafeEqual(Buffer.from(expected), Buffer.from(actual));
}

async function dispatch(request: Request) {
  return handleApi(request, async () => {
    if (!authorized(request))
      throw new ApiError(
        401,
        "authentication_required",
        "Authorization is required.",
      );
    return json(await dispatchLedgerBatch());
  });
}

export const GET = dispatch;
export const POST = dispatch;
