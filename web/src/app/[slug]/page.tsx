import Link from "next/link";

import { Gallery } from "@/components/gallery";
import { requireAuthenticatedUser } from "@/lib/auth";
import { getAccountSummary, rpc, type GalleryItem } from "@/lib/database";
import { createSupabaseServerClient } from "@/lib/supabase/server";

export const dynamic = "force-dynamic";

interface PageProps {
  params: Promise<{ slug: string }>;
}

interface ProfileData {
  summary: Awaited<ReturnType<typeof getAccountSummary>>;
  page: { items: GalleryItem[]; nextCursor: string | null };
}

function SignInWall() {
  return (
    <section className="card sign-wall">
      <p className="eyebrow">Private gallery</p>
      <h2>Owner sign-in required</h2>
      <p className="lead">
        Sign in with the account that owns this profile and complete strong
        authentication.
      </p>
      <Link className="button primary" href="/auth">
        Sign in
      </Link>
    </section>
  );
}

async function loadProfileData(): Promise<ProfileData | null> {
  try {
    await requireAuthenticatedUser(undefined, { strong: true });
    const client = await createSupabaseServerClient();
    const [summary, page] = await Promise.all([
      getAccountSummary(client),
      rpc<{ items: GalleryItem[]; nextCursor: string | null }>(
        client,
        "list_my_gallery_items",
        { p_limit: 50 },
      ),
    ]);
    return { summary, page };
  } catch {
    return null;
  }
}

export default async function ProfilePage({ params }: PageProps) {
  const [data, { slug }] = await Promise.all([loadProfileData(), params]);
  if (!data || data.summary.slug.toLowerCase() !== slug.toLowerCase())
    return <SignInWall />;

  return (
    <Gallery
      initialItems={data.page.items}
      initialNextCursor={data.page.nextCursor}
      slug={data.summary.slug}
    />
  );
}
