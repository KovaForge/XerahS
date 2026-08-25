import Link from "next/link";
import { redirect } from "next/navigation";

import { ConsentDecisionForm } from "@/components/consent-decision-form";
import { MfaControls } from "@/components/mfa-controls";
import { requireAuthenticatedUser } from "@/lib/auth";
import { getPublicEnv, getServerEnv } from "@/lib/env";
import { ApiError } from "@/lib/errors";
import {
  assertDesktopAuthorization,
  assertDesktopOAuthRedirect,
  authorizationIdSchema,
  desktopOAuthRedirectUris,
} from "@/lib/oauth-validation";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export const dynamic = "force-dynamic";

interface ConsentPageProps {
  searchParams: Promise<{ authorization_id?: string | string[] }>;
}

function ConsentError({ children }: { children: React.ReactNode }) {
  return (
    <section className="card auth-card">
      <p className="eyebrow">Desktop authorization</p>
      <h2>Authorization unavailable</h2>
      <p className="lead">{children}</p>
      <Link className="button" href="/settings">
        Return to settings
      </Link>
    </section>
  );
}

export default async function ConsentPage({ searchParams }: ConsentPageProps) {
  const rawAuthorizationId = (await searchParams).authorization_id;
  const parsed = authorizationIdSchema.safeParse(
    Array.isArray(rawAuthorizationId)
      ? rawAuthorizationId[0]
      : rawAuthorizationId,
  );
  if (!parsed.success)
    return <ConsentError>The authorization request is invalid.</ConsentError>;

  const authorizationId = parsed.data;
  const consentPath = `/oauth/consent?authorization_id=${encodeURIComponent(authorizationId)}`;
  let user: Awaited<ReturnType<typeof requireAuthenticatedUser>>;
  try {
    user = await requireAuthenticatedUser(undefined, { verifiedEmail: true });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401)
      redirect(`/auth?next=${encodeURIComponent(consentPath)}`);
    return (
      <ConsentError>
        Sign in with a verified email address before authorizing the desktop
        application.
      </ConsentError>
    );
  }

  if (user.aal !== "aal2") {
    return (
      <section className="consent-stack">
        <article className="card">
          <p className="eyebrow">Desktop authorization</p>
          <h2>Strong authentication required</h2>
          <p className="lead">
            Verify your authenticator before reviewing this authorization
            request. This page will resume automatically after verification.
          </p>
        </article>
        <MfaControls
          passkeysEnabled={getPublicEnv().NEXT_PUBLIC_PASSKEYS_ENABLED}
          strongAuth={false}
        />
      </section>
    );
  }

  const env = getServerEnv();
  const clientId = env.XERAHS_DESKTOP_OAUTH_CLIENT_ID;
  if (!clientId)
    return (
      <ConsentError>Desktop authorization is not configured.</ConsentError>
    );
  const redirectUris = desktopOAuthRedirectUris(env.APP_ORIGIN);
  const supabase = await createSupabaseServerClient();
  const { data, error } =
    await supabase.auth.oauth.getAuthorizationDetails(authorizationId);
  if (error || !data)
    return <ConsentError>The authorization request has expired.</ConsentError>;

  if (!("authorization_id" in data)) {
    redirect(assertDesktopOAuthRedirect(data.redirect_url, redirectUris).href);
  }

  try {
    assertDesktopAuthorization(data, {
      clientId,
      redirectUris,
      userId: user.id,
    });
  } catch {
    return (
      <ConsentError>The authorization request is not permitted.</ConsentError>
    );
  }

  const scopes = data.scope.trim().split(/\s+/);
  return (
    <section className="card auth-card stack">
      <p className="eyebrow">Desktop authorization</p>
      <h2>Connect {data.client.name}</h2>
      <p className="lead">
        Allow the XerahS desktop application to use your owner-only cloud
        gallery as {data.user.email}?
      </p>
      <div>
        <strong>Requested identity information</strong>
        <ul>
          {scopes.map((scope) => (
            <li key={scope}>{scope}</li>
          ))}
        </ul>
      </div>
      <p className="status">
        The desktop application can act with the same owner permissions as this
        session. Approve only if you started this request from XerahS.
      </p>
      <ConsentDecisionForm authorizationId={authorizationId} />
    </section>
  );
}
