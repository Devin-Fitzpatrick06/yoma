import type { NextConfig } from "next";

// Only P2E (and this app itself, for the demo host page) may frame SkillLens.
const frameAncestors = ["'self'", process.env.NEXT_PUBLIC_P2E_ORIGIN].filter(Boolean).join(" ");

const nextConfig: NextConfig = {
  poweredByHeader: false,
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "Content-Security-Policy", value: `frame-ancestors ${frameAncestors}` },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          // Voice answers need the microphone; the photo input uses the camera through the file picker.
          { key: "Permissions-Policy", value: "microphone=(self), camera=(self), geolocation=()" },
        ],
      },
    ];
  },
};

export default nextConfig;
