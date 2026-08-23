import { describe, expect, it } from "vitest";

import { mapDatabaseError } from "@/lib/errors";

describe("mapDatabaseError", () => {
  it("maps a disabled trial kill switch to a service error", () => {
    const error = mapDatabaseError({
      code: "55000",
      message: "trial_grants_disabled",
    });
    expect(error.status).toBe(503);
    expect(error.code).toBe("integration_unavailable");
    expect(error.message).toBe("Trial grants are currently disabled.");
  });

  it("maps an already-granted trial to a conflict", () => {
    const error = mapDatabaseError({
      code: "23505",
      message: "trial_already_granted_for_identity",
    });
    expect(error.status).toBe(409);
    expect(error.code).toBe("conflict");
  });
});
