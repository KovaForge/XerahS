import { assertProductionConfiguration } from "@/lib/env";

export function register(): void {
  if (process.env.NEXT_RUNTIME === "nodejs") assertProductionConfiguration();
}
