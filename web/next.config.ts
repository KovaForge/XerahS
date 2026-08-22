import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  poweredByHeader: false,
  reactStrictMode: true,
  // TypeScript 7.0 has no programmatic compiler API. Next.js must invoke its
  // native CLI; ESLint is isolated with the official TypeScript 6 API shim.
  experimental: {
    typedEnv: true,
    useTypeScriptCli: true,
  },
  redirects: async () => [
    {
      source: "/:path*",
      has: [{ type: "host", value: "www.xerahs.com" }],
      destination: "https://xerahs.com/:path*",
      permanent: true,
    },
  ],
};

export default nextConfig;
