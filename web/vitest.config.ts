import { defineConfig } from "vitest/config";

export default defineConfig({
  resolve: {
    alias: {
      "@": new URL("./src", import.meta.url).pathname,
    },
  },
  test: {
    environment: "node",
    coverage: {
      include: ["src/lib/**/*.ts"],
      reporter: ["text", "json", "html"],
    },
  },
});
