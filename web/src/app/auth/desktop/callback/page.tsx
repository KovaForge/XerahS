import { notFound } from "next/navigation";

import { DesktopOAuthRelay } from "@/components/desktop-oauth-relay";

export const dynamic = "force-dynamic";

interface PageProps {
  searchParams: Promise<{
    code?: string | string[];
    error?: string | string[];
    state?: string | string[];
  }>;
}

function one(value: string | string[] | undefined): string | null {
  return typeof value === "string" && value.length > 0 ? value : null;
}

export default async function DesktopOAuthCallbackPage({
  searchParams,
}: PageProps) {
  const input = await searchParams;
  const code = one(input.code);
  const error = one(input.error);
  const state = one(input.state);
  if (
    !state ||
    state.length > 1024 ||
    (code === null) === (error === null) ||
    (code?.length ?? 0) > 4096 ||
    (error !== null && !/^[A-Za-z0-9_]{1,128}$/.test(error))
  )
    notFound();
  return (
    <DesktopOAuthRelay
      code={code ?? undefined}
      error={error ?? undefined}
      state={state}
    />
  );
}
