import { AuthForm } from "@/components/auth-form";

export const dynamic = "force-dynamic";

export default function AuthPage() {
  return (
    <section className="card auth-card">
      <p className="eyebrow">Owner access</p>
      <h2>Sign in to XerahS Cloud</h2>
      <p className="lead">
        Use your verified email and password. Your gallery requires a completed
        strong-authentication challenge.
      </p>
      <AuthForm />
    </section>
  );
}
