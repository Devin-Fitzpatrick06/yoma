// Reads what the learner actually wrote, per rubric criterion: amounts and calculations, causes, concrete steps,
// polite wording, course ideas. The simulated provider scores from this, and real-model scores are checked against it,
// so feedback never asks for something the answer already has (for example "add numbers" to an answer with sums).
// shortcut: English and Pidgin keyword rules, so a strong answer in unusual words can read one level low; tune them on
// real educator-scored answers (or replace them with a model check) before a pilot.
import { t } from "./i18n";
import type { Criterion, Lang, PreviousAttempt, Rubric } from "./schema";

export interface Evidence {
  level: 1 | 2 | 3 | 4;
  /** The learner's own words that support the level, for "Did well" quotes. Empty when nothing fits. */
  quote: string;
}

const STOP = new Set("that this with your have they what when should would could about them their there from which then also just very much like more make some been were will into than only even well good does done".split(" "));

const AMOUNT = /₦\s?\d[\d,]*(?:\.\d+)?|\b\d[\d,]*(?:\.\d+)?\s?(?:naira|k\b|%|percent)?/gi;
const MONEY = /\b(naira|money|profit|cost|costs|price|prices|save|saving|savings|spend|spending|income|earn|earns|loss|lose|budget|interest|pay|paid|debt|loan|cash|wage|salary|expense|expenses|cheap|expensive|afford)\b|₦/i;
const CALC = /[=+×*÷]|\d\s?[x/-]\s?₦?\d|\b(minus|plus|times|divided|per|each|total|difference|left over|remain|instead of|compared|more than|less than|percent|half|double|break even|profit of|margin)\b|%/i;
const CAUSE = /\b(because|cos|since|so|that is why|that's why|which means|this means|means|due to|as a result|if|when|cause[sd]?|na why|reason)\b/i;
const EFFECT = /\b(money|profit|cost|lose|loss|sales|income|customers?|trust|reliable|expect|expects|needs?|wants?|worry|late|safe|risk|debt)\b|₦/i;
const STEP = /\b(should|can|could|will|would|need to|must|try|plan to|start|first|then|next|after that|finally|suppose|fit|make (?:she|he|e|im|dem|i))\b/i;
const VAGUE = /\b(something|anything|different|better|careful|think about it|pray|hope|work hard|do well|be good)\b/i;
const CONCRETE = /\b(buy|sell|save|ask|call|text|send|write|keep|track|record|list|check|compare|tell|put|open|join|practi[sc]e|prepare|plan|set|cut|reduce|raise|increase|choose|apply|visit|arrive|leave|bring|update|share|offer|use|split|budget|wait|repay|borrow|reply|greet|apologi[sz]e|explain|report|block|refuse|confirm|verify|test|measure|inspect|clean|tighten|replace|switch|disconnect|unplug)\w*\b/i;
const TIME = /\b(first|then|next|after|finally|before|tomorrow|today|tonight|each|every|weekly|daily|morning|evening|payday|week|month|day|hour|minutes?|now|immediately|later)\b|\d/i;
const QUOTED = /["“”]([^"“”]{8,})["“”]|\b(?:say|said|text|message|write|tell|reply|email)\w*\b[^.?!]{0,12}:\s*\S/i;
const POLITE = /\b(please|thank|thanks|sorry|apologi[sz]e|good (?:morning|afternoon|evening)|dear|sir|ma'?am|kindly|excuse me|grateful|appreciate|abeg)\b/i;
const MANNER = /\b(polite|politely|nice|respect|respectful|kind|calm|calmly|professional|friendly|honest|clear|clearly)\b/i;

const sentences = (text: string) =>
  text.split(/(?<=[.!?;\n])\s+|\s+(?=(?:then|also|and then|after that|finally|second|third)\b)/i).map((s) => s.trim()).filter(Boolean);
const words = (text: string) => text.toLowerCase().split(/[^\p{L}\p{N}]+/u).filter((w) => w.length >= 4 && !STOP.has(w));
const stem = (w: string) => w.replace(/(ing|ed|es|s)$/, "");
const clip = (s: string) => (s.length > 90 ? `${s.slice(0, 87).trimEnd()}…` : s);
const find = (text: string, re: RegExp) => sentences(text).find((s) => re.test(s)) ?? "";
const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

function numbers(answer: string, challenge: string): Evidence {
  const amounts = answer.match(AMOUNT)?.map((a) => a.trim()).filter((a) => /\d/.test(a)) ?? [];
  const given = new Set(challenge.match(AMOUNT)?.map((a) => a.replace(/\s/g, "")) ?? []);
  const fresh = amounts.filter((a) => !given.has(a.replace(/\s/g, "")));
  const quote = find(answer, /\d/);
  if (amounts.length === 0) return { level: MONEY.test(answer) ? 2 : 1, quote: find(answer, MONEY) };
  if ((amounts.length >= 2 && CALC.test(answer) && fresh.length >= 1) || fresh.length >= 3) return { level: 4, quote };
  return { level: 3, quote };
}

function understand(answer: string, challenge: string): Evidence {
  const asked = new Set(words(challenge).map(stem));
  const overlap = words(answer).some((w) => asked.has(stem(w)));
  const causal = CAUSE.test(answer);
  const quote = find(answer, CAUSE) || find(answer, EFFECT);
  if (!overlap && !causal) return { level: 1, quote: "" };
  if (!(overlap && causal)) return { level: 2, quote };
  return { level: EFFECT.test(answer) && answer.split(/\s+/).length >= 15 ? 4 : 3, quote };
}

function action(answer: string): Evidence {
  const steps = sentences(answer).filter((s) => STEP.test(s) || CONCRETE.test(s.split(/\s+/)[0] ?? ""));
  const concrete = steps.filter((s) => CONCRETE.test(s) && s.split(/\s+/).length >= 4 && !(VAGUE.test(s) && !TIME.test(s)));
  if (steps.length === 0) return { level: 1, quote: "" };
  if (concrete.length === 0) return { level: 2, quote: steps[0] };
  if (concrete.length >= 2 && concrete.some((s) => TIME.test(s))) return { level: 4, quote: concrete[0] };
  return { level: 3, quote: concrete[0] };
}

function communication(answer: string): Evidence {
  const quoted = QUOTED.test(answer);
  const polite = POLITE.test(answer);
  if (quoted && polite) return { level: 4, quote: find(answer, POLITE) };
  if (polite || (quoted && MANNER.test(answer))) return { level: 3, quote: find(answer, POLITE) || find(answer, MANNER) };
  if (MANNER.test(answer) || quoted) return { level: 2, quote: find(answer, MANNER) || find(answer, QUOTED) };
  return { level: 1, quote: "" };
}

function concepts(answer: string, keywords: string[]): Evidence {
  const text = answer.toLowerCase();
  const hits = keywords.filter((k) => new RegExp(`\\b${escape(k.toLowerCase())}(?:s|es|d|ed|ing)?\\b`).test(text));
  const level = Math.min(4, hits.length + 1) as Evidence["level"];
  return { level, quote: hits.length ? find(answer, new RegExp(hits.map(escape).join("|"), "i")) : "" };
}

/** What the answer shows for one criterion, from 1 (nothing yet) to 4 (strong). */
export function evidenceFor(criterion: Criterion, answer: string, challenge: string): Evidence {
  const e =
    criterion.kind === "numbers" ? numbers(answer, challenge)
    : criterion.kind === "understand" ? understand(answer, challenge)
    : criterion.kind === "action" ? action(answer)
    : criterion.kind === "communication" ? communication(answer)
    : concepts(answer, criterion.keywords ?? []);
  return { ...e, quote: clip(e.quote) };
}

/**
 * The one criterion to suggest working on: the lowest level, in rubric order. On Level 2, a tie never goes back to the
 * criterion the last tip was about once the learner has improved it, so the same tip is not given twice for nothing.
 */
export function pickSuggestion(rubric: Rubric, levels: Record<string, number>, previous?: PreviousAttempt): string {
  const improved = (id: string) => previous && levels[id] > (previous.levels[id] ?? 4);
  const low = Math.min(...rubric.criteria.map((c) => levels[c.id]));
  const lowest = rubric.criteria.filter((c) => levels[c.id] === low);
  return (lowest.find((c) => !(c.id === previous?.suggestionCriterionId && improved(c.id))) ?? lowest[0]).id;
}

/** The tip at this level: the rubric's starter tip; once "Good", what the top level looks like; at the top, keep practising. */
export function tipFor(criterion: Criterion, level: number, lang: Lang): string {
  if (level >= 4) return t(lang, "allStrong");
  return level === 3 ? `${t(lang, "goFurther")} ${criterion.levels[3][lang]}.` : criterion.improve[lang];
}
