export type ErrorCode =
  | "authentication_required"
  | "strong_auth_required"
  | "email_verification_required"
  | "entitlement_required"
  | "forbidden"
  | "invalid_request"
  | "not_found"
  | "conflict"
  | "rate_limited"
  | "integration_unavailable"
  | "internal_error";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: ErrorCode,
    message: string,
    public readonly retryAfter?: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export function isPostgresCode(error: unknown, code: string): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    error.code === code
  );
}

export function mapDatabaseError(error: unknown): ApiError {
  if (isPostgresCode(error, "P0001"))
    return new ApiError(403, "forbidden", "The operation is not permitted.");
  if (isPostgresCode(error, "23505"))
    return new ApiError(
      409,
      "conflict",
      "The request conflicts with existing data.",
    );
  return new ApiError(
    500,
    "internal_error",
    "The operation could not be completed.",
  );
}
