import { ApiError } from "@/lib/errors";
import { getServerEnv } from "@/lib/env";

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

  const origin = request.headers.get("origin");
  const expected = new URL(getServerEnv().APP_ORIGIN).origin;
  if (!origin || origin !== expected) {
    throw new ApiError(403, "forbidden", "The request origin is not allowed.");
  }
  if (request.headers.get("sec-fetch-site") === "cross-site") {
    throw new ApiError(
      403,
      "forbidden",
      "Cross-site requests are not allowed.",
    );
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
