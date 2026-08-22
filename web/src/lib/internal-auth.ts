import "server-only";

import { timingSafeEqual } from "node:crypto";

import { ApiError } from "@/lib/errors";
import { getServerEnv } from "@/lib/env";

export function requireCronAuthorization(request: Request): void {
  const expected = getServerEnv().CRON_SECRET;
  const actual = request.headers
    .get("authorization")
    ?.replace(/^Bearer\s+/i, "");
  if (
    !expected ||
    !actual ||
    expected.length !== actual.length ||
    !timingSafeEqual(Buffer.from(expected), Buffer.from(actual))
  ) {
    throw new ApiError(
      401,
      "authentication_required",
      "Authorization is required.",
    );
  }
}
