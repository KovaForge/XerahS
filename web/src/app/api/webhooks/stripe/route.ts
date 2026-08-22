import { ApiError } from "@/lib/errors";
import { rpc } from "@/lib/database";
import { getServerEnv } from "@/lib/env";
import { empty } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { getStripeClient, handledStripeEvents } from "@/lib/stripe";
import { createServiceRoleClient } from "@/lib/supabase/server";
import type Stripe from "stripe";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type JsonObject = Record<string, unknown>;

interface EntitlementUpdate {
  customerId: string | null;
  metadataUserId: string | null;
  subscriptionId: string | null;
  priceId: string | null;
  paidThrough: string | null;
  status:
    | "incomplete"
    | "active"
    | "past_due"
    | "unpaid"
    | "paused"
    | "canceled";
  graceStartedAt: string | null;
}

function objectValue(value: unknown): JsonObject {
  return typeof value === "object" && value !== null
    ? (value as JsonObject)
    : {};
}

function stripeId(value: unknown): string | null {
  if (typeof value === "string") return value;
  const id = objectValue(value).id;
  return typeof id === "string" ? id : null;
}

function metadataUserId(value: JsonObject): string | null {
  const metadata = objectValue(value.metadata);
  const candidate = metadata.xerahs_user_id ?? value.client_reference_id;
  return typeof candidate === "string" &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      candidate,
    )
    ? candidate
    : null;
}

function unixTime(value: unknown): string | null {
  return typeof value === "number" && Number.isSafeInteger(value) && value > 0
    ? new Date(value * 1_000).toISOString()
    : null;
}

function firstLine(value: JsonObject): JsonObject {
  const lines = objectValue(value.lines);
  const data = Array.isArray(lines.data) ? lines.data : [];
  return objectValue(data[0]);
}

function firstArrayItem(value: unknown): JsonObject {
  return objectValue(Array.isArray(value) ? value[0] : undefined);
}

function entitlementUpdate(event: Stripe.Event): EntitlementUpdate | null {
  const value = objectValue(event.data.object);
  const customerId = stripeId(value.customer);
  const subscriptionId =
    stripeId(value.subscription) ??
    stripeId(value.id?.toString().startsWith("sub_") ? value.id : null);
  const line = firstLine(value);
  const priceId =
    stripeId(objectValue(line.pricing).price_details) ?? stripeId(line.price);
  const paidThrough =
    unixTime(objectValue(line.period).end) ??
    unixTime(value.current_period_end);
  const common = {
    customerId,
    metadataUserId: metadataUserId(value),
    subscriptionId,
    priceId,
    paidThrough,
  };

  if (event.type.startsWith("customer.subscription.")) {
    const subscriptionItem = firstArrayItem(objectValue(value.items).data);
    const status =
      typeof value.status === "string" ? value.status : "incomplete";
    const mapped: EntitlementUpdate["status"] =
      status === "active" || status === "trialing"
        ? "active"
        : status === "past_due"
          ? "past_due"
          : status === "unpaid"
            ? "unpaid"
            : status === "paused"
              ? "paused"
              : status === "canceled" || status === "incomplete_expired"
                ? "canceled"
                : "incomplete";
    return {
      ...common,
      subscriptionId: stripeId(value.id),
      priceId: stripeId(subscriptionItem.price) ?? priceId,
      paidThrough: unixTime(subscriptionItem.current_period_end) ?? paidThrough,
      status: mapped,
      graceStartedAt:
        mapped === "past_due"
          ? new Date(event.created * 1_000).toISOString()
          : null,
    };
  }

  if (event.type === "invoice.paid")
    return { ...common, status: "active", graceStartedAt: null };
  if (
    event.type === "invoice.payment_failed" ||
    event.type === "invoice.payment_action_required" ||
    event.type === "invoice.finalization_failed"
  ) {
    return {
      ...common,
      status: "past_due",
      graceStartedAt: new Date(event.created * 1_000).toISOString(),
    };
  }
  return null;
}

export async function POST(request: Request) {
  return handleApi(request, async () => {
    const env = getServerEnv();
    const signature = request.headers.get("stripe-signature");
    if (!signature || !env.STRIPE_WEBHOOK_SECRET)
      throw new ApiError(
        400,
        "invalid_request",
        "The webhook signature is missing.",
      );
    const rawBody = await request.text();
    let event;
    try {
      event = getStripeClient().webhooks.constructEvent(
        rawBody,
        signature,
        env.STRIPE_WEBHOOK_SECRET,
      );
    } catch {
      throw new ApiError(
        400,
        "invalid_request",
        "The webhook signature is invalid.",
      );
    }
    if (event.livemode !== env.STRIPE_EXPECT_LIVEMODE)
      throw new ApiError(
        400,
        "invalid_request",
        "Webhook mode does not match this environment.",
      );
    if (!handledStripeEvents.has(event.type)) return empty(200);

    const service = createServiceRoleClient();
    const createdAt = new Date(event.created * 1_000).toISOString();
    if (
      event.type === "charge.dispute.created" ||
      event.type === "charge.dispute.closed"
    ) {
      const dispute = objectValue(event.data.object);
      let customerId = stripeId(dispute.customer);
      if (!customerId) {
        const chargeId = stripeId(dispute.charge);
        if (chargeId)
          customerId = stripeId(
            (await getStripeClient().charges.retrieve(chargeId)).customer,
          );
      }
      if (!customerId)
        throw new ApiError(
          422,
          "invalid_request",
          "The dispute has no mapped customer.",
        );
      await rpc(service, "apply_stripe_dispute", {
        p_event_id: event.id,
        p_event_type: event.type,
        p_created_at: createdAt,
        p_livemode: event.livemode,
        p_customer_id: customerId,
        p_suspended: event.type === "charge.dispute.created",
      });
      return empty(200);
    }

    const update = entitlementUpdate(event);
    if (update) {
      const userId = await rpc<string>(
        service,
        "resolve_stripe_webhook_owner",
        {
          p_customer_id: update.customerId,
          p_metadata_user_id: update.metadataUserId,
        },
      );
      await rpc(service, "apply_stripe_entitlement", {
        p_event_id: event.id,
        p_event_type: event.type,
        p_stripe_created_at: createdAt,
        p_livemode: event.livemode,
        p_user_id: userId,
        p_result_status: update.status,
        p_reason: event.type,
        p_customer_id: update.customerId,
        p_subscription_id: update.subscriptionId,
        p_price_id: update.priceId,
        p_paid_through: update.paidThrough,
        p_grace_started_at: update.graceStartedAt,
        p_dispute_suspended: false,
      });
    } else {
      await rpc(service, "record_stripe_webhook_event", {
        p_event_id: event.id,
        p_event_type: event.type,
        p_created_at: createdAt,
        p_livemode: event.livemode,
      });
    }
    return empty(200);
  });
}
