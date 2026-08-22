import "server-only";

import { createHash } from "node:crypto";
import {
  GetObjectCommand,
  PutObjectCommand,
  S3Client,
} from "@aws-sdk/client-s3";

import { sha256 } from "@/lib/ledger/canonical";
import type { LedgerObject, LedgerStore } from "@/lib/ledger/types";

function getHttpStatusCode(error: unknown): number | undefined {
  if (typeof error !== "object" || error === null || !("$metadata" in error))
    return undefined;
  return (error as { $metadata?: { httpStatusCode?: number } }).$metadata
    ?.httpStatusCode;
}

export class R2LedgerStore implements LedgerStore {
  private readonly client: S3Client;

  constructor(
    private readonly bucket: string,
    accountId: string,
    accessKeyId: string,
    secretAccessKey: string,
  ) {
    this.client = new S3Client({
      region: "auto",
      endpoint: `https://${accountId}.r2.cloudflarestorage.com`,
      credentials: { accessKeyId, secretAccessKey },
    });
  }

  async putIfAbsent(key: string, body: string): Promise<LedgerObject> {
    const digest = sha256(body);
    try {
      const result = await this.client.send(
        new PutObjectCommand({
          Bucket: this.bucket,
          Key: key,
          Body: body,
          ContentType: "application/json",
          ContentMD5: createHash("md5").update(body, "utf8").digest("base64"),
          IfNoneMatch: "*",
          Metadata: { sha256: digest },
        }),
      );
      return { key, body, sha256: digest, etag: result.ETag ?? "" };
    } catch (error) {
      if (getHttpStatusCode(error) !== 412) throw error;
      const existing = await this.get(key);
      if (!existing || existing.sha256 !== digest || existing.body !== body)
        throw new Error("Ledger object collision detected.");
      return existing;
    }
  }

  async get(key: string): Promise<LedgerObject | null> {
    try {
      const result = await this.client.send(
        new GetObjectCommand({ Bucket: this.bucket, Key: key }),
      );
      const body = await result.Body?.transformToString("utf8");
      if (body === undefined)
        throw new Error("R2 returned an empty ledger object.");
      return { key, body, sha256: sha256(body), etag: result.ETag ?? "" };
    } catch (error) {
      if (getHttpStatusCode(error) === 404) return null;
      throw error;
    }
  }
}
