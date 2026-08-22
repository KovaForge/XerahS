import "server-only";

import type { SupabaseClient } from "@supabase/supabase-js";

import { ApiError, mapDatabaseError } from "@/lib/errors";

export interface AccountSummary {
  slug: string;
  timeZone: string;
  strongAuth: boolean;
  trialStatus: "not_started" | "trial_pending" | "active" | "expired";
  trialEndsAt: string | null;
  subscriptionStatus: string | null;
  paidThrough: string | null;
  canPublish: boolean;
  disputeSuspended: boolean;
}

export interface GalleryItem {
  id: string;
  clientItemId: string;
  url: string;
  thumbnailUrl: string | null;
  kind: "screenshot" | "screencast";
  fileName: string;
  title: string;
  capturedAt: string;
  publishedAt: string;
  host: string | null;
  contentType: string | null;
}

function firstRow<T>(data: unknown): T {
  const value = Array.isArray(data) ? data[0] : data;
  if (!value || typeof value !== "object")
    throw new ApiError(
      500,
      "internal_error",
      "The database returned an invalid result.",
    );
  return value as T;
}

export async function rpc<T>(
  client: SupabaseClient,
  name: string,
  args: Record<string, unknown> = {},
): Promise<T> {
  const { data, error } = await client.rpc(name, args);
  if (error) throw mapDatabaseError(error);
  return data as T;
}

export async function getAccountSummary(
  client: SupabaseClient,
): Promise<AccountSummary> {
  return firstRow<AccountSummary>(await rpc(client, "get_my_account_summary"));
}
