import { ApiError } from "@/lib/errors";
import { getServerEnv } from "@/lib/env";
import {
  isAllowedMutationOrigin,
  mutationAllowedOrigins,
} from "@/lib/mutation-origin";

export function correlationId(request: Request): string {
  return (
    request.headers.get("x-vercel-id")?.slice(0, 128) ?? crypto.randomUUID()
  );
}

export function hasBearerAuthorization(request: Request): boolean {
  return /^Bearer\s+\S+$/i.test(request.headers.get("authorization") ?? "");
}

export function enforceSameOriginMutation(request: Request): void {
  if (hasBearerAuthorization(request)) return;

  const allowed = mutationAllowedOrigins(
    request.url,
    getServerEnv().APP_ORIGIN,
    request.headers.get("x-forwarded-host"),
    request.headers.get("host"),
  );
  if (
    !isAllowedMutationOrigin(
      request.headers.get("origin"),
      allowed,
      request.headers.get("sec-fetch-site"),
      request.headers.get("referer"),
    )
  ) {
    throw new ApiError(403, "forbidden", "The request origin is not allowed.");
  }
}

export async function readJson(
  request: Request,
  maxBytes = 16_384,
): Promise<unknown> {
  const contentLength = Number(request.headers.get("content-length") ?? "0");
  if (Number.isFinite(contentLength) && contentLength > maxBytes) {
    throw new ApiError(
      413,
      "invalid_request",
      "The request body is too large.",
    );
  }
  try {
    return await request.json();
  } catch {
    throw new ApiError(
      400,
      "invalid_request",
      "The request body must be valid JSON.",
    );
  }
}
