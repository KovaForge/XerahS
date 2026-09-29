export interface LedgerOutboxEvent {
  eventId: string;
  eventType:
    | "trial_grant_created"
    | "gallery_item_unpublished"
    | "account_deleted";
  occurredAt: string;
  payload: Record<string, unknown>;
  hmacKeyVersion: string;
  payloadSha256: string;
}

export interface LedgerObject {
  key: string;
  body: string;
  sha256: string;
  etag: string;
}

export interface LedgerStore {
  putIfAbsent(key: string, body: string): Promise<LedgerObject>;
  get(key: string): Promise<LedgerObject | null>;
}
