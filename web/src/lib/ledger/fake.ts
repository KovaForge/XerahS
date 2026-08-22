import "server-only";

import { sha256 } from "@/lib/ledger/canonical";
import type { LedgerObject, LedgerStore } from "@/lib/ledger/types";

export class FakeLedgerStore implements LedgerStore {
  private readonly objects = new Map<string, LedgerObject>();

  async putIfAbsent(key: string, body: string): Promise<LedgerObject> {
    const existing = this.objects.get(key);
    if (existing) return existing;
    const digest = sha256(body);
    const object = {
      key,
      body,
      sha256: digest,
      etag: `fake-${digest.slice(0, 32)}`,
    };
    this.objects.set(key, object);
    return object;
  }

  async get(key: string): Promise<LedgerObject | null> {
    return this.objects.get(key) ?? null;
  }
}
