import "server-only";

import type Stripe from "stripe";

import { getServerEnv } from "@/lib/env";
import { getStripeClient } from "@/lib/stripe";

export type BillingStatus =
  | "incomplete"
  | "active"
  | "past_due"
  | "unpaid"
  | "paused"
  | "canceled";

export interface CanonicalStripeEntitlement {
  customerId: string;
  metadataUserId: string | null;
  subscriptionId: string;
  priceId: string | null;
  paidThrough: string | null;
  status: BillingStatus;
  catalogAllowed: boolean;
}

function stripeId(value: unknown): string | null {
  if (typeof value === "string") return value;
  if (typeof value !== "object" || value === null || !("id" in value))
    return null;
  return typeof value.id === "string" ? value.id : null;
}

function validUserId(value: unknown): string | null {
  return typeof value === "string" &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      value,
    )
    ? value
    : null;
}

export function entitledStripePriceIds(): ReadonlySet<string> {
  const env = getServerEnv();
  const legacy = env.STRIPE_ENTITLED_LEGACY_PRICES.split(",")
    .map((value) => value.trim())
    .filter(Boolean);
  if (legacy.some((value) => !/^price_[A-Za-z0-9_]{8,255}$/.test(value)))
    throw new Error("STRIPE_ENTITLED_LEGACY_PRICES is invalid.");
  return new Set(
    [env.STRIPE_PRICE_MONTHLY, env.STRIPE_PRICE_ANNUAL, ...legacy].filter(
      (value): value is string => Boolean(value),
    ),
  );
}

function mapSubscriptionStatus(
  status: Stripe.Subscription.Status,
): BillingStatus {
  switch (status) {
    case "active":
    case "trialing":
      return "active";
    case "past_due":
      return "past_due";
    case "unpaid":
      return "unpaid";
    case "paused":
      return "paused";
    case "canceled":
    case "incomplete_expired":
      return "canceled";
    default:
      return "incomplete";
  }
}

export async function retrieveCanonicalStripeEntitlement(
  subscriptionId: string,
): Promise<CanonicalStripeEntitlement> {
  if (!/^sub_[A-Za-z0-9_]{8,255}$/.test(subscriptionId))
    throw new Error("Stripe subscription identifier is invalid.");
  const subscription =
    await getStripeClient().subscriptions.retrieve(subscriptionId);
  const customerId = stripeId(subscription.customer);
  if (!customerId) throw new Error("Stripe subscription customer is missing.");
  const [item, extraItem] = subscription.items.data;
  const priceId = item ? stripeId(item.price) : null;
  const quantity = item?.quantity ?? 1;
  const catalogAllowed =
    !extraItem &&
    quantity === 1 &&
    priceId !== null &&
    entitledStripePriceIds().has(priceId);
  return {
    customerId,
    metadataUserId: validUserId(subscription.metadata.xerahs_user_id),
    subscriptionId: subscription.id,
    priceId,
    paidThrough: item
      ? new Date(item.current_period_end * 1_000).toISOString()
      : null,
    status: catalogAllowed
      ? mapSubscriptionStatus(subscription.status)
      : "canceled",
    catalogAllowed,
  };
}

function objectValue(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null
    ? (value as Record<string, unknown>)
    : {};
}

export function subscriptionIdFromEvent(event: Stripe.Event): string | null {
  const value = objectValue(event.data.object);
  if (event.type.startsWith("customer.subscription.")) return stripeId(value);
  const parent = objectValue(value.parent);
  const details = objectValue(parent.subscription_details);
  return stripeId(details.subscription) ?? stripeId(value.subscription);
}

export function checkoutMetadata(event: Stripe.Event): {
  sessionId: string;
  userId: string;
  attemptId: string;
  plan: "monthly" | "annual";
} {
  const value = objectValue(event.data.object);
  const metadata = objectValue(value.metadata);
  const sessionId = stripeId(value);
  const userId = validUserId(metadata.xerahs_user_id);
  const attemptId = validUserId(metadata.xerahs_checkout_attempt_id);
  const plan = metadata.xerahs_plan;
  if (
    !sessionId ||
    !userId ||
    !attemptId ||
    (plan !== "monthly" && plan !== "annual")
  )
    throw new Error("Stripe Checkout metadata is invalid.");
  return { sessionId, userId, attemptId, plan };
}

export function stripeCustomerId(value: unknown): string | null {
  return stripeId(value);
}
