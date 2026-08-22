import { ESLint } from "eslint";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const toolingDirectory = dirname(fileURLToPath(import.meta.url));
const projectDirectory = resolve(toolingDirectory, "../..");
const eslint = new ESLint({
  cwd: projectDirectory,
  overrideConfigFile: resolve(toolingDirectory, "eslint.config.mjs"),
});
const results = await eslint.lintFiles(["."]);
const formatter = await eslint.loadFormatter("stylish");
const output = formatter.format(results);

if (output) process.stdout.write(output);
if (
  results.some((result) => result.errorCount > 0 || result.warningCount > 0)
) {
  process.exitCode = 1;
}
