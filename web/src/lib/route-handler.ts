import { ZodError } from "zod";

import { ApiError } from "@/lib/errors";
import { correlationId } from "@/lib/request";
import { problem } from "@/lib/responses";

export async function handleApi(
  request: Request,
  operation: () => Promise<Response>,
): Promise<Response> {
  const id = correlationId(request);
  try {
    const response = await operation();
    response.headers.set("X-Correlation-ID", id);
    return response;
  } catch (error) {
    if (error instanceof ZodError) {
      return problem(
        new ApiError(
          400,
          "invalid_request",
          error.issues[0]?.message ?? "The request is invalid.",
        ),
        id,
      );
    }
    return problem(error, id);
  }
}
