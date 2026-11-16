import type { Locale } from "./schema";

/** Picks one value per slot. Locale lists are index-aligned, so one index keeps item/unit/price consistent. */
export function slotValues(locale: Locale, seed: number): Record<string, string> {
  const values: Record<string, string> = { city: locale.city };
  for (const [slot, list] of Object.entries(locale.slots)) values[slot] = list[seed % list.length];
  return values;
}

/** The distinct {slot} names a template uses, in order of first appearance. */
export const templateSlots = (text: string) => [...new Set([...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]))];

/** Fills {slot} placeholders. Throws on a missing slot so a broken template never reaches a learner. */
export function fillTemplate(text: string, values: Record<string, string>): string {
  return text.replace(/\{(\w+)\}/g, (_, slot: string) => {
    if (!(slot in values)) throw new Error(`Template slot "{${slot}}" has no value`);
    return values[slot];
  });
}
