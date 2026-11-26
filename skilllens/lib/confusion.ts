// Spots an answer that says "I don't understand the question" instead of trying it. Scoring that against a rubric
// would praise or criticise an answer that does not exist, so the learner gets the question explained instead.
// shortcut: English and Pidgin phrases only; add phrases from real learner answers before a pilot.

const CONFUSED =
  /\b(confus\w*|(?:do not|don'?t|dont|cannot|can'?t) (?:understand|get it|get the question|follow)|not sure what (?:to|i|you)|what (?:does|do) (?:this|it|that|you) mean|what am i (?:supposed|meant) to|(?:i )?no understand|i no sabi|i no know wetin|wetin (?:dem|una|e|this one) mean|(?:question|this) (?:is|dey|na) (?:bad|unclear|confusing|hard|not clear)|makes? no sense|no make sense|help me understand|explain (?:the|this) question)\b/i;

/** True when the answer is mainly "I don't get it": a confusion phrase, no amounts, and no more than 30 words. */
export const isConfused = (answer: string) => CONFUSED.test(answer) && !/\d/.test(answer) && answer.trim().split(/\s+/).length <= 30;
