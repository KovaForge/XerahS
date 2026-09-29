import { NextResponse } from "next/server";

import { ApiError } from "@/lib/errors";

const noStoreHeaders = {
  "Cache-Control": "private, no-store, max-age=0",
  Pragma: "no-cache",
  Vary: "Cookie, Authorization",
} as const;

export function json<T>(body: T, init: ResponseInit = {}): NextResponse<T> {
  const response = NextResponse.json(body, init);
  for (const [name, value] of Object.entries(noStoreHeaders))
    response.headers.set(name, value);
  return response;
}

export function empty(status = 204, headers?: HeadersInit): Response {
  return new Response(null, {
    status,
    headers: { ...noStoreHeaders, ...headers },
  });
}

export function pending(operationId: string, retryAfter = 5): NextResponse {
  return json(
    { status: "pending", operationId },
    { status: 202, headers: { "Retry-After": String(retryAfter) } },
  );
}

export function problem(error: unknown, correlationId?: string): NextResponse {
  const apiError =
    error instanceof ApiError
      ? error
      : new ApiError(500, "internal_error", "The request failed.");
  return json(
    {
      error: {
        code: apiError.code,
        message: apiError.message,
        correlationId,
      },
    },
    {
      status: apiError.status,
      headers: apiError.retryAfter
        ? { "Retry-After": String(apiError.retryAfter) }
        : undefined,
    },
  );
}
