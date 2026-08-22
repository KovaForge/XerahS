const routesBySchedule = {
  "* * * * *": "/api/internal/ledger/dispatch",
  "*/5 * * * *": "/api/internal/account/deletions",
  "17 3 * * *": "/api/internal/stripe/reconcile",
} as const;

async function invokeJob(
  path: (typeof routesBySchedule)[keyof typeof routesBySchedule],
  cron: string,
  env: Env,
): Promise<void> {
  const response = await fetch(new URL(path, env.APP_ORIGIN), {
    method: "POST",
    headers: {
      Authorization: `Bearer ${env.CRON_SECRET}`,
      "User-Agent": "xerahs-cloud-scheduler/1.0",
    },
    redirect: "error",
  });
  await response.body?.cancel();
  if (!response.ok) {
    throw new Error(`Scheduled job returned HTTP ${response.status}.`);
  }
  console.log(
    JSON.stringify({
      message: "scheduled_job_completed",
      cron,
      path,
      status: response.status,
    }),
  );
}

export default {
  async fetch(request): Promise<Response> {
    const url = new URL(request.url);
    if (request.method === "GET" && url.pathname === "/healthz") {
      return Response.json(
        { status: "ready" },
        { headers: { "Cache-Control": "no-store" } },
      );
    }
    return new Response(null, { status: 404 });
  },

  async scheduled(controller, env): Promise<void> {
    const cron = controller.cron;
    if (!(cron in routesBySchedule)) {
      console.error(JSON.stringify({ message: "unknown_cron_schedule", cron }));
      throw new Error("Unknown cron schedule.");
    }
    const path = routesBySchedule[cron as keyof typeof routesBySchedule];
    try {
      await invokeJob(path, cron, env);
    } catch (error) {
      console.error(
        JSON.stringify({
          message: "scheduled_job_failed",
          cron,
          path,
          error: error instanceof Error ? error.message : "Unknown error",
        }),
      );
      throw error;
    }
  },
} satisfies ExportedHandler<Env>;
