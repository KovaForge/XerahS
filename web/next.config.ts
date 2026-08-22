import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  poweredByHeader: false,
  reactStrictMode: true,
  experimental: {
    typedEnv: true,
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
