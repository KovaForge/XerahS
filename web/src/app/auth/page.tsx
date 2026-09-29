import { AuthForm } from "@/components/auth-form";
import { getOptionalAuthenticatedUser } from "@/lib/auth";
import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

function safeNext(value: string | string[] | undefined): string {
  const candidate = Array.isArray(value) ? value[0] : value;
  return candidate?.startsWith("/") &&
    !candidate.startsWith("//") &&
    !/^\/auth(?:[/?]|$)/.test(candidate)
    ? candidate
    : "/settings";
}

export default async function AuthPage({
  searchParams,
}: {
  searchParams: Promise<{ next?: string | string[] }>;
}) {
  const next = safeNext((await searchParams).next);
  if (await getOptionalAuthenticatedUser()) redirect(next);
  return (
    <section className="card auth-card">
      <p className="eyebrow">Owner access</p>
      <h2>Sign in to XerahS Cloud</h2>
      <p className="lead">
        Use your verified email and password. Your gallery requires a completed
        strong-authentication challenge.
      </p>
      <AuthForm next={next} />
    </section>
  );
}
