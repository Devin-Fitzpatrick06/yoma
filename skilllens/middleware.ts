// Password-protects the facilitator view with HTTP Basic auth (any username, password = FACILITATOR_PASSWORD).
// shortcut: one shared password; switch to P2E or Yoma sign-in with a facilitator role before a real pilot.
import { NextResponse, type NextRequest } from "next/server";

// Constant-time compare, so the password cannot be guessed one character at a time.
function same(a: string, b: string): boolean {
  let diff = a.length ^ b.length;
  for (let i = 0; i < Math.max(a.length, b.length); i++) diff |= (a.charCodeAt(i) || 0) ^ (b.charCodeAt(i) || 0);
  return diff === 0;
}

export function middleware(req: NextRequest) {
  const password = process.env.FACILITATOR_PASSWORD;
  if (!password) return new NextResponse("Set FACILITATOR_PASSWORD in .env.local to open the facilitator view.", { status: 503 });

  const [scheme, encoded] = (req.headers.get("authorization") ?? "").split(" ");
  if (scheme === "Basic" && encoded) {
    const decoded = atob(encoded);
    if (same(decoded.slice(decoded.indexOf(":") + 1), password)) return NextResponse.next();
  }
  return new NextResponse("Facilitator sign-in required", { status: 401, headers: { "WWW-Authenticate": 'Basic realm="SkillLens facilitator"' } });
}

export const config = { matcher: ["/facilitator/:path*"] };
