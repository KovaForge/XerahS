import { requireAuthenticatedUser } from "@/lib/auth";
import { getAccountSummary } from "@/lib/database";
import { enforceSameOriginMutation } from "@/lib/request";
import { handleApi } from "@/lib/route-handler";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const user = await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    const client = await createSupabaseServerClient(request);
    const [summary, profile, gallery] = await Promise.all([
      getAccountSummary(client),
      client
        .from("profiles")
        .select("slug,time_zone,created_at,updated_at")
        .single(),
      client
        .from("gallery_items")
        .select(
          "id,client_item_id,url,thumbnail_url,kind,file_name,title,captured_at,published_at,host,content_type",
        )
        .order("captured_at", { ascending: false }),
    ]);
    if (profile.error) throw profile.error;
    if (gallery.error) throw gallery.error;

    const body = JSON.stringify(
      {
        schemaVersion: 1,
        exportedAt: new Date().toISOString(),
        account: { id: user.id, email: user.email },
        profile: profile.data,
        subscription: {
          status: summary.subscriptionStatus,
          paidThrough: summary.paidThrough,
          trialStatus: summary.trialStatus,
          trialEndsAt: summary.trialEndsAt,
        },
        galleryItems: gallery.data,
        security: { assuranceLevel: user.aal },
      },
      null,
      2,
    );
    return new Response(body, {
      status: 200,
      headers: {
        "Cache-Control": "private, no-store",
        "Content-Disposition":
          'attachment; filename="xerahs-cloud-export.json"',
        "Content-Type": "application/json; charset=utf-8",
      },
    });
  });
}
