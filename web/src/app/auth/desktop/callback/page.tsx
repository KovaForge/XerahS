import { notFound } from "next/navigation";

import { DesktopOAuthRelay } from "@/components/desktop-oauth-relay";

export const dynamic = "force-dynamic";

interface PageProps {
  searchParams: Promise<{ code?: string; state?: string }>;
}

export default async function DesktopOAuthCallbackPage({
  searchParams,
}: PageProps) {
  const { code, state } = await searchParams;
  if (!code || !state || code.length > 4096 || state.length > 1024) notFound();
  return <DesktopOAuthRelay code={code} state={state} />;
}
