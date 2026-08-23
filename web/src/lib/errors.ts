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

function postgresMessage(error: unknown): string {
  if (
    typeof error === "object" &&
    error !== null &&
    "message" in error &&
    typeof error.message === "string"
  )
    return error.message;
  return "";
}

export function mapDatabaseError(error: unknown): ApiError {
  const message = postgresMessage(error);
  if (message === "trial_grants_disabled")
    return new ApiError(
      503,
      "integration_unavailable",
      "Trial grants are currently disabled.",
    );
  if (
    message === "aal2_required" ||
    message === "recent_strong_auth_required" ||
    message === "session_required"
  )
    return new ApiError(
      403,
      "strong_auth_required",
      "Complete a strong-authentication challenge to continue.",
    );
  if (
    message === "verified_identity_not_registered" ||
    message === "verified_email_required"
  )
    return new ApiError(
      403,
      "email_verification_required",
      "Verify your email address to continue.",
    );
  if (message === "trial_already_granted_for_identity")
    return new ApiError(
      409,
      "conflict",
      "A trial has already been granted for this account.",
    );
  if (isPostgresCode(error, "P0001") || isPostgresCode(error, "42501"))
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
