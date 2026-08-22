import { getPublicEnv, getServerEnv } from "@/lib/env";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET() {
  const server = getServerEnv();
  const checks = {
    supabase: Boolean(getPublicEnv().NEXT_PUBLIC_SUPABASE_URL),
    stripe: Boolean(server.STRIPE_SECRET_KEY && server.STRIPE_WEBHOOK_SECRET),
    ledger:
      server.LEDGER_USE_LOCAL_FAKE ||
      Boolean(server.R2_LEDGER_ACCOUNT_ID && server.R2_LEDGER_BUCKET),
  };
  const ready = checks.supabase && checks.stripe && checks.ledger;
  return Response.json(
    {
      status: ready ? "ready" : "degraded",
      environment: server.APP_ENV,
      checks,
    },
    { status: ready ? 200 : 503, headers: { "Cache-Control": "no-store" } },
  );
}
