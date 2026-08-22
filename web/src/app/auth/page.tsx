import { AuthForm } from "@/components/auth-form";

export const dynamic = "force-dynamic";

function safeNext(value: string | string[] | undefined): string {
  const candidate = Array.isArray(value) ? value[0] : value;
  return candidate?.startsWith("/") && !candidate.startsWith("//")
    ? candidate
    : "/settings";
}

export default async function AuthPage({
  searchParams,
}: {
  searchParams: Promise<{ next?: string | string[] }>;
}) {
  const next = safeNext((await searchParams).next);
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
