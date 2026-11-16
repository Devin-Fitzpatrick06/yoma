import { badRequest, handle } from "../../../lib/api";
import { transcribeSafely } from "../../../lib/pipeline";

const MAX_BYTES = 2 * 1024 * 1024; // photos are compressed on the device first, so this is generous

export async function POST(req: Request) {
  const form = await req.formData().catch(() => null);
  const photo = form?.get("photo");
  if (!(photo instanceof File) || !photo.type.startsWith("image/")) return badRequest("Send an image in the 'photo' field");
  if (photo.size > MAX_BYTES) return Response.json({ error: "Photo is too large" }, { status: 413 });

  // The photo is screened, read once and not stored.
  return handle("transcribe", async () => {
    const outcome = await transcribeSafely(new Uint8Array(await photo.arrayBuffer()), photo.type);
    if (outcome.status === "flagged") return Response.json({ status: "flagged" });
    return Response.json({ status: "ok", text: outcome.text, redacted: outcome.redacted });
  });
}
