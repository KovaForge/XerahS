#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Collections.ObjectModel;
using System.Text;

namespace XerahS.CLI.Commands;

public static class OpenClawPluginExporter
{
    private static readonly IReadOnlyDictionary<string, string> Templates = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["package.json"] = """
            {
              "name": "@xerahs/openclaw-plugin",
              "version": "0.1.0",
              "private": true,
              "description": "OpenClaw native plugin for XerahS upload-to-URL workflows.",
              "type": "module",
              "devDependencies": {
                "@openclaw/plugin-sdk": "workspace:*"
              },
              "dependencies": {
                "typebox": "1.1.37"
              },
              "openclaw": {
                "extensions": [
                  "./index.ts"
                ]
              }
            }
            """,
            ["openclaw.plugin.json"] = """
            {
              "id": "xerahs",
              "activation": {
                "onStartup": true,
                "onCommands": [
                  "xerahs",
                  "xerahs.upload",
                  "xerahs.uploadText",
                  "xerahs.doctorUploaders",
                  "xerahs.bootstrapUploaders"
                ]
              },
              "enabledByDefault": true,
              "name": "XerahS",
              "description": "Upload files and generated text through XerahS and return shareable URLs.",
              "contracts": {
                "tools": [
                  "xerahs_upload_file",
                  "xerahs_upload_text",
                  "xerahs_doctor_uploaders",
                  "xerahs_bootstrap_uploaders"
                ]
              },
              "configSchema": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "command": {
                    "type": "string",
                    "description": "XerahS CLI command or absolute executable path.",
                    "default": "xerahscli"
                  },
                  "timeoutMs": {
                    "type": "integer",
                    "description": "Maximum time to wait for a XerahS CLI operation.",
                    "minimum": 1000,
                    "default": 120000
                  }
                }
              }
            }
            """,
            ["cli-metadata.ts"] = """
            import { definePluginEntry } from "openclaw/plugin-sdk/plugin-entry";

            export default definePluginEntry({
              id: "xerahs",
              name: "XerahS",
              description: "Upload files and generated text through XerahS and return shareable URLs.",
              register(api) {
                api.registerCli(() => {}, {
                  descriptors: [
                    {
                      name: "xerahscli",
                      description: "Upload files or text through XerahS",
                      hasSubcommands: true,
                    },
                  ],
                });
              },
            });
            """,
            ["index.ts"] = """
            import { definePluginEntry } from "openclaw/plugin-sdk/plugin-entry";
            import { registerXerahSCli } from "./src/cli.js";
            import { resolveXerahSPluginConfig, xerahsConfigSchema } from "./src/config.js";
            import { createXerahSTools } from "./src/tools.js";

            export default definePluginEntry({
              id: "xerahs",
              name: "XerahS",
              description: "Upload files and generated text through XerahS and return shareable URLs.",
              configSchema: xerahsConfigSchema,
              register(api) {
                const config = resolveXerahSPluginConfig(api.pluginConfig);

                for (const tool of createXerahSTools(config)) {
                  api.registerTool(tool);
                }

                api.registerCli(
                  ({ program }) => {
                    registerXerahSCli(program, config);
                  },
                  {
                    descriptors: [
                      {
                        name: "xerahscli",
                        description: "Upload files or text through XerahS",
                        hasSubcommands: true,
                      },
                    ],
                  },
                );
              },
            });
            """,
            ["src/config.ts"] = """
            import { Type } from "typebox";

            export type XerahSPluginConfig = {
              command: string;
              timeoutMs: number;
            };

            export const xerahsConfigSchema = Type.Object(
              {
                command: Type.Optional(
                  Type.String({
                    description: "XerahS CLI command or absolute executable path.",
                    default: "xerahscli",
                  }),
                ),
                timeoutMs: Type.Optional(
                  Type.Integer({
                    description: "Maximum time to wait for a XerahS CLI operation.",
                    minimum: 1000,
                    default: 120000,
                  }),
                ),
              },
              { additionalProperties: false },
            );

            export function resolveXerahSPluginConfig(
              rawConfig: Record<string, unknown> | undefined,
            ): XerahSPluginConfig {
              const command =
                typeof rawConfig?.command === "string" && rawConfig.command.trim()
                  ? rawConfig.command.trim()
                  : "xerahscli";
              const timeoutMs =
                typeof rawConfig?.timeoutMs === "number" &&
                Number.isFinite(rawConfig.timeoutMs) &&
                rawConfig.timeoutMs >= 1000
                  ? Math.trunc(rawConfig.timeoutMs)
                  : 120_000;

              return {
                command,
                timeoutMs,
              };
            }
            """,
            ["src/runner.ts"] = """
            import { spawn } from "node:child_process";
            import type { XerahSPluginConfig } from "./config.js";

            export type XerahSRunOptions = {
              input?: string;
              expectJson?: boolean;
              signal?: AbortSignal;
            };

            export type XerahSRunResult = {
              exitCode: number | null;
              signalCode: NodeJS.Signals | null;
              stdout: string;
              stderr: string;
              json?: unknown;
            };

            export async function runXerahS(
              config: XerahSPluginConfig,
              args: string[],
              options: XerahSRunOptions = {},
            ): Promise<XerahSRunResult> {
              return await new Promise<XerahSRunResult>((resolve, reject) => {
                const abortSignal = options.signal;
                if (abortSignal?.aborted) {
                  reject(createAbortError());
                  return;
                }

                let settled = false;
                let timedOut = false;
                const child = spawn(config.command, args, {
                  stdio: ["pipe", "pipe", "pipe"],
                  windowsHide: true,
                });
                const stdout: Buffer[] = [];
                const stderr: Buffer[] = [];
                const forceKillDelayMs = 5_000;
                let forceKillTimer: NodeJS.Timeout | undefined;
                let abortListener: (() => void) | undefined;
                const forceKill = () => {
                  if (!forceKillTimer) {
                    forceKillTimer = setTimeout(() => child.kill("SIGKILL"), forceKillDelayMs);
                  }
                };
                const terminateChild = () => {
                  if (child.exitCode === null && child.signalCode === null) {
                    child.kill();
                    forceKill();
                  }
                };
                const timer = setTimeout(() => {
                  timedOut = true;
                  terminateChild();
                }, config.timeoutMs);
                const cleanup = () => {
                  clearTimeout(timer);
                  if (forceKillTimer) {
                    clearTimeout(forceKillTimer);
                  }
                  if (abortListener) {
                    abortSignal?.removeEventListener("abort", abortListener);
                  }
                };
                const rejectOnce = (error: Error) => {
                  if (settled) {
                    return;
                  }

                  settled = true;
                  cleanup();
                  terminateChild();
                  reject(error);
                };
                abortListener = () => rejectOnce(createAbortError());
                abortSignal?.addEventListener("abort", abortListener, { once: true });

                child.stdout.on("data", (chunk: Buffer) => stdout.push(chunk));
                child.stdout.on("error", rejectOnce);
                child.stderr.on("data", (chunk: Buffer) => stderr.push(chunk));
                child.stderr.on("error", rejectOnce);
                child.stdin.on("error", (error: NodeJS.ErrnoException) => {
                  if (error.code !== "EPIPE") {
                    rejectOnce(error);
                  }
                });
                child.on("error", rejectOnce);
                child.on("close", (exitCode, signalCode) => {
                  cleanup();
                  if (settled) {
                    return;
                  }
                  settled = true;
                  const rawStdout = Buffer.concat(stdout).toString("utf8").trim();
                  const rawStderr = Buffer.concat(stderr).toString("utf8").trim();
                  const result: XerahSRunResult = {
                    exitCode,
                    signalCode,
                    stdout: redactDiagnostics(rawStdout),
                    stderr: redactDiagnostics(rawStderr),
                  };

                  if (timedOut) {
                    reject(new Error(`XerahS command timed out after ${config.timeoutMs} ms.`));
                    return;
                  }

                  if (exitCode !== 0) {
                    reject(new Error(formatFailure(args, result)));
                    return;
                  }

                  if (options.expectJson) {
                    try {
                      result.json = JSON.parse(rawStdout);
                    } catch (error) {
                      reject(new Error(formatInvalidJsonFailure(error as Error, result)));
                      return;
                    }
                  }

                  resolve(result);
                });

                if (options.input !== undefined) {
                  child.stdin.end(options.input, "utf8");
                } else {
                  child.stdin.end();
                }
              });
            }

            function createAbortError(): Error {
              return new Error("XerahS command was cancelled.");
            }

            function formatFailure(args: string[], result: XerahSRunResult): string {
              const details = [result.stderr, result.stdout].filter(Boolean).join("\n");
              const status =
                result.exitCode === null && result.signalCode
                  ? `signal ${result.signalCode}`
                  : `exit code ${result.exitCode}`;
              return `XerahS ${args.join(" ")} failed with ${status}.${details ? `\n${details}` : ""}`;
            }

            function formatInvalidJsonFailure(error: Error, result: XerahSRunResult): string {
              const details = [result.stderr, result.stdout].filter(Boolean).join("\n");
              return `XerahS did not return valid JSON: ${error.message}${details ? `\n${details}` : ""}`;
            }

            function redactDiagnostics(text: string): string {
              return text.replace(
                /\b(api[_-]?key|authorization|bearer|password|secret|token)\b\s*[:=]\s*[^\s]+/giu,
                "$1=[redacted]",
              );
            }
            """,
            ["src/tools.ts"] = """
            import { jsonResult } from "openclaw/plugin-sdk/core";
            import { Type } from "typebox";
            import type { XerahSPluginConfig } from "./config.js";
            import { runXerahS } from "./runner.js";

            const uploadFileParams = Type.Object(
              {
                path: Type.String({
                  description: "Absolute or workspace-relative path to the file to upload.",
                }),
                name: Type.Optional(
                  Type.String({
                    description: "Optional upload filename override.",
                  }),
                ),
                asFile: Type.Optional(
                  Type.Boolean({
                    description: "Force text-like artifacts such as HTML through the file uploader category.",
                  }),
                ),
              },
              { additionalProperties: false },
            );

            const uploadTextParams = Type.Object(
              {
                text: Type.String({
                  description: "Text content to upload through the configured XerahS text uploader.",
                }),
                name: Type.Optional(
                  Type.String({
                    description: "Filename to associate with the uploaded text content.",
                    default: "upload.txt",
                  }),
                ),
              },
              { additionalProperties: false },
            );

            export function createXerahSTools(config: XerahSPluginConfig) {
              return [
                {
                  name: "xerahs_upload_file",
                  label: "Upload File with XerahS",
                  description: "Upload a local file through XerahS and return the resulting URL JSON.",
                  parameters: uploadFileParams,
                  execute: async (_toolCallId: string, rawParams: Record<string, unknown>, signal?: AbortSignal) => {
                    const filePath = readRequiredPath(rawParams, "path");
                    const args = ["upload", filePath, "--json"];
                    const name = readOptionalString(rawParams, "name");

                    if (name) {
                      args.push("--name", name);
                    }

                    if (rawParams.asFile === true) {
                      args.push("--as-file");
                    }

                    const result = await runXerahS(config, args, { expectJson: true, signal });
                    return jsonResult(requireUploadUrl(result.json));
                  },
                },
                {
                  name: "xerahs_upload_text",
                  label: "Upload Text with XerahS",
                  description: "Upload generated text through XerahS stdin and return the resulting URL JSON.",
                  parameters: uploadTextParams,
                  execute: async (_toolCallId: string, rawParams: Record<string, unknown>, signal?: AbortSignal) => {
                    const text = readRequiredString(rawParams, "text");
                    const name = readOptionalString(rawParams, "name") ?? "upload.txt";
                    const result = await runXerahS(config, ["upload", "--pipe", "--name", name, "--json"], {
                      input: text,
                      expectJson: true,
                      signal,
                    });

                    return jsonResult(requireUploadUrl(result.json));
                  },
                },
                {
                  name: "xerahs_doctor_uploaders",
                  label: "Check XerahS Uploaders",
                  description: "Inspect whether XerahS uploaders are configured and ready.",
                  parameters: Type.Object({}, { additionalProperties: false }),
                  execute: async (_toolCallId: string, _rawParams: Record<string, unknown>, signal?: AbortSignal) => {
                    const result = await runXerahS(config, ["doctor", "uploaders", "--json"], {
                      expectJson: true,
                      signal,
                    });

                    return jsonResult(requireUploaderReport(result.json));
                  },
                },
                {
                  name: "xerahs_bootstrap_uploaders",
                  label: "Bootstrap XerahS Uploaders",
                  description: "Initialize safe first-use XerahS uploader defaults.",
                  parameters: Type.Object({}, { additionalProperties: false }),
                  execute: async (_toolCallId: string, _rawParams: Record<string, unknown>, signal?: AbortSignal) => {
                    const result = await runXerahS(config, ["bootstrap", "uploaders", "--json"], {
                      expectJson: true,
                      signal,
                    });

                    return jsonResult(requireUploaderReport(result.json));
                  },
                },
              ];
            }

            function readRequiredString(params: Record<string, unknown>, name: string): string {
              const value = params[name];
              if (typeof value !== "string" || !value.trim()) {
                throw new Error(`${name} is required.`);
              }

              return value;
            }

            function readRequiredPath(params: Record<string, unknown>, name: string): string {
              return readRequiredString(params, name).trim();
            }

            function readOptionalString(params: Record<string, unknown>, name: string): string | undefined {
              const value = params[name];
              if (typeof value !== "string") {
                return undefined;
              }

              const trimmedValue = value.trim();
              return trimmedValue ? trimmedValue : undefined;
            }

            function requireUploadUrl(value: unknown): unknown {
              if (!value || typeof value !== "object" || Array.isArray(value)) {
                throw new Error("XerahS upload did not return an object.");
              }

              const url = (value as { url?: unknown }).url;
              if (typeof url !== "string" || !url.trim()) {
                throw new Error("XerahS upload did not return a URL.");
              }

              let parsedUrl: URL;
              try {
                parsedUrl = new URL(url);
              } catch {
                throw new Error("XerahS upload did not return a valid URL.");
              }

              if (parsedUrl.protocol !== "http:" && parsedUrl.protocol !== "https:") {
                throw new Error("XerahS upload did not return an HTTP URL.");
              }

              return value;
            }

            function requireUploaderReport(value: unknown): unknown {
              if (!value || typeof value !== "object" || Array.isArray(value)) {
                throw new Error("XerahS uploader command did not return a report object.");
              }

              const report = value as Record<string, unknown>;
              for (const property of ["Created", "Repaired", "Skipped", "Diagnostics"]) {
                if (!Array.isArray(report[property])) {
                  throw new Error(`XerahS uploader report did not return a ${property} array.`);
                }
              }

              if (typeof report.HasBlockingIssues !== "boolean") {
                throw new Error("XerahS uploader report did not return a HasBlockingIssues boolean.");
              }

              return value;
            }
            """,
            ["src/cli.ts"] = """
            import type { XerahSPluginConfig } from "./config.js";
            import { runXerahS } from "./runner.js";

            type ProgramLike = {
              command(name: string): CommandLike;
            };

            type CommandLike = {
              description(text: string): CommandLike;
              option(flags: string, description?: string): CommandLike;
              argument?(name: string, description?: string): CommandLike;
              command(name: string): CommandLike;
              action(handler: (...args: unknown[]) => Promise<void> | void): CommandLike;
            };

            export function registerXerahSCli(program: ProgramLike, config: XerahSPluginConfig): void {
              const root = program.command("xerahs").description("Upload files or text through XerahS.");

              root
                .command("doctor-uploaders")
                .description("Inspect XerahS uploader readiness.")
                .action(async () => {
                  await printRun(config, ["doctor", "uploaders", "--json"], requireUploaderReport);
                });

              root
                .command("bootstrap-uploaders")
                .description("Initialize safe first-use XerahS uploader defaults.")
                .action(async () => {
                  await printRun(config, ["bootstrap", "uploaders", "--json"], requireUploaderReport);
                });

              root
                .command("upload <file>")
                .description("Upload a local file through XerahS and print URL JSON.")
                .option("--name <name>", "Optional upload filename override.")
                .option("--as-file", "Force the file uploader category.")
                .action(async (file, options) => {
                  const filePath = String(file).trim();
                  if (!filePath) {
                    throw new Error("file is required.");
                  }

                  const args = ["upload", filePath, "--json"];
                  const opts = typeof options === "object" && options ? (options as Record<string, unknown>) : {};

                  if (typeof opts.name === "string" && opts.name.trim()) {
                    args.push("--name", opts.name.trim());
                  }

                  if (opts.asFile === true) {
                    args.push("--as-file");
                  }

                  await printRun(config, args, requireUploadUrl);
                });

              root
                .command("upload-text <text>")
                .description("Upload text through XerahS and print URL JSON.")
                .option("--name <name>", "Filename to associate with the uploaded text content.")
                .action(async (text, options) => {
                  const opts = typeof options === "object" && options ? (options as Record<string, unknown>) : {};
                  const name = typeof opts.name === "string" && opts.name.trim() ? opts.name.trim() : "upload.txt";
                  await printRun(config, ["upload", "--pipe", "--name", name, "--json"], requireUploadUrl, String(text));
                });
            }

            type JsonValidator = (value: unknown) => unknown;

            async function printRun(
              config: XerahSPluginConfig,
              args: string[],
              jsonValidator?: JsonValidator,
              input?: string,
            ): Promise<void> {
              const abortController = new AbortController();
              let cancellationExitCode: number | undefined;
              const abortSigint = () => {
                cancellationExitCode = 130;
                abortController.abort();
              };
              const abortSigterm = () => {
                cancellationExitCode = 143;
                abortController.abort();
              };
              process.once("SIGINT", abortSigint);
              process.once("SIGTERM", abortSigterm);

              try {
                const result = await runXerahS(config, args, {
                  input,
                  expectJson: jsonValidator !== undefined,
                  signal: abortController.signal,
                });
                if (jsonValidator) {
                  try {
                    process.stdout.write(`${JSON.stringify(jsonValidator(result.json))}\n`);
                  } catch (error) {
                    throw new Error(formatJsonValidationError(error, result.json));
                  }
                } else {
                  process.stdout.write(result.stdout ? `${result.stdout}\n` : "");
                }

                process.stderr.write(result.stderr ? `${result.stderr}\n` : "");
              } catch (error) {
                if (cancellationExitCode !== undefined) {
                  process.exitCode = cancellationExitCode;
                  process.stderr.write(`${(error as Error).message}\n`);
                  return;
                }

                process.exitCode = 1;
                process.stderr.write(`${formatCliError(error)}\n`);
              } finally {
                process.off("SIGINT", abortSigint);
                process.off("SIGTERM", abortSigterm);
              }
            }

            function formatCliError(error: unknown): string {
              return error instanceof Error ? error.message : String(error);
            }

            function formatJsonValidationError(error: unknown, value: unknown): string {
              return `${formatCliError(error)}\nReceived JSON shape: ${describeJsonShape(value)}`;
            }

            function describeJsonShape(value: unknown): string {
              return describeJsonShapeValue(value, 0);
            }

            function describeJsonShapeValue(value: unknown, depth: number): string {
              if (Array.isArray(value)) {
                if (value.length === 0 || depth >= 2) {
                  return `array(${value.length})`;
                }

                return `array(${value.length})<${describeJsonShapeValue(value[0], depth + 1)}>`;
              }

              if (!value || typeof value !== "object") {
                return typeof value;
              }

              const maxObjectEntries = 12;
              const allEntries = Object.entries(value as Record<string, unknown>);
              const entries = allEntries
                .slice(0, maxObjectEntries)
                .map(([key, entry]) => `${formatJsonShapeKey(key)}:${depth >= 2 ? (Array.isArray(entry) ? "array" : typeof entry) : describeJsonShapeValue(entry, depth + 1)}`)
                .sort();
              if (allEntries.length > maxObjectEntries) {
                entries.push(`...+${allEntries.length - maxObjectEntries} keys`);
              }

              return `object{${entries.join(",")}}`;
            }

            function formatJsonShapeKey(key: string): string {
              const sanitizedKey = key.replace(/[\u0000-\u001F\u007F]/gu, "?");
              const maxKeyLength = 48;
              const boundedKey = sanitizedKey.length > maxKeyLength
                ? `${sanitizedKey.slice(0, maxKeyLength)}...`
                : sanitizedKey;
              return JSON.stringify(boundedKey);
            }

            function requireUploadUrl(value: unknown): unknown {
              if (!value || typeof value !== "object" || Array.isArray(value)) {
                throw new Error("XerahS upload did not return an object.");
              }

              const url = (value as { url?: unknown }).url;
              if (typeof url !== "string" || !url.trim()) {
                throw new Error("XerahS upload did not return a URL.");
              }

              let parsedUrl: URL;
              try {
                parsedUrl = new URL(url);
              } catch {
                throw new Error("XerahS upload did not return a valid URL.");
              }

              if (parsedUrl.protocol !== "http:" && parsedUrl.protocol !== "https:") {
                throw new Error("XerahS upload did not return an HTTP URL.");
              }

              return value;
            }

            function requireUploaderReport(value: unknown): unknown {
              if (!value || typeof value !== "object" || Array.isArray(value)) {
                throw new Error("XerahS uploader command did not return a report object.");
              }

              const report = value as Record<string, unknown>;
              for (const property of ["Created", "Repaired", "Skipped", "Diagnostics"]) {
                if (!Array.isArray(report[property])) {
                  throw new Error(`XerahS uploader report did not return a ${property} array.`);
                }
              }

              if (typeof report.HasBlockingIssues !== "boolean") {
                throw new Error("XerahS uploader report did not return a HasBlockingIssues boolean.");
              }

              return value;
            }
            """
        });

    public static OpenClawPluginExportResult Export(string outputDirectory, bool force)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
        }

        string fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var writtenFiles = new List<string>(Templates.Count);

        foreach ((string relativePath, string content) in Templates)
        {
            string destinationPath = Path.Combine(fullOutputDirectory, relativePath);
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            if (!force && File.Exists(destinationPath))
            {
                throw new IOException($"Refusing to overwrite existing file without --force: {destinationPath}");
            }

            File.WriteAllText(destinationPath, content + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writtenFiles.Add(destinationPath);
        }

        return new OpenClawPluginExportResult(fullOutputDirectory, writtenFiles);
    }
}

public sealed record OpenClawPluginExportResult(string OutputDirectory, IReadOnlyList<string> Files);
