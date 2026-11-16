import { readFileSync } from "node:fs";
import { join } from "node:path";

// Prompts live in /prompts/<version>/ so educators can review them; bump the version for any wording change.
export const PROMPT_VERSION = "v2";

export function loadPrompt(name: "generate" | "feedback" | "vision" | "safety", vars: Record<string, string> = {}): string {
  const text = readFileSync(join(process.cwd(), "prompts", PROMPT_VERSION, `${name}.md`), "utf8");
  return text.replace(/\{\{(\w+)\}\}/g, (_, key: string) => {
    if (!(key in vars)) throw new Error(`Prompt ${name} needs {{${key}}}`);
    return vars[key];
  });
}
