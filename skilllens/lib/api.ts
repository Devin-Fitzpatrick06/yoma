// Shared route plumbing: one place that turns a failed AI, safety or Yoma call into a clean JSON error.
import type { z } from "zod";

export const badRequest = (error: string) => Response.json({ error }, { status: 400 });

export async function handle(name: string, fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn();
  } catch (e) {
    // Log the reason only. Errors never include learner text: providers report status codes, not request bodies.
    console.error(`[skilllens] ${name} failed: ${e instanceof Error ? e.message : String(e)}`);
    return Response.json({ error: "The service is busy. Please try again." }, { status: 502 });
  }
}

export async function parseJson<T extends z.ZodTypeAny>(req: Request, schema: T): Promise<z.infer<T> | null> {
  const parsed = schema.safeParse(await req.json().catch(() => null));
  return parsed.success ? parsed.data : null;
}
