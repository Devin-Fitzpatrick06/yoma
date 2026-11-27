import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type {
  DraftCheckField,
  DraftCheckFix,
} from "~/api/models/courseQuality";
import {
  DRAFT_FIELD_TARGETS,
  applicableFormField,
  clampScore,
  createDebouncedCheck,
  draftFieldTarget,
  draftKey,
  goToFieldLabel,
  notCheckableReason,
  otherStepHint,
  isDraftEmpty,
  isSwitchedOff,
  pickDraftFields,
  scoreRingDash,
  sortFixes,
  suggestionValueFor,
  verdictLabel,
  type DebouncedCheckTimers,
} from "./DraftCheckPanel.helpers";

describe("verdict labels", () => {
  test("partner wording for each predicted verdict", () => {
    assert.equal(verdictLabel("Verified"), "Verified");
    assert.equal(verdictLabel("NeedsReview"), "Needs a look");
    assert.equal(verdictLabel("NotVerified"), "Not verified yet");
  });
  test("an unknown verdict reads as not verified, never as verified", () => {
    assert.equal(verdictLabel("Something"), "Not verified yet");
    assert.equal(verdictLabel(null), "Not verified yet");
  });
  test("the score is clamped for the ring", () => {
    assert.equal(clampScore(-5), 0);
    assert.equal(clampScore(140), 100);
    assert.equal(clampScore(72.6), 73);
    assert.equal(clampScore(Number.NaN), 0);
    const { dashArray, circumference } = scoreRingDash(50, 10);
    assert.equal(dashArray, `${circumference / 2} ${circumference}`);
  });
});

describe("field mapping", () => {
  test("every contract field has an entry", () => {
    const fields: DraftCheckField[] = [
      "title",
      "summary",
      "description",
      "url",
      "dateStart",
      "dateEnd",
      "commitment",
      "skills",
      "languages",
      "countries",
      "difficulty",
      "other",
    ];
    for (const field of fields) assert.ok(field in DRAFT_FIELD_TARGETS);
  });
  test("fields map to the wizard's steps", () => {
    assert.equal(draftFieldTarget("url")?.step, 1);
    assert.deepEqual(draftFieldTarget("url")?.selectors, ['[name="uRL"]']);
    assert.equal(draftFieldTarget("dateStart")?.step, 2);
    assert.equal(draftFieldTarget("skills")?.step, 4);
    assert.equal(draftFieldTarget("difficulty"), null);
    assert.equal(draftFieldTarget("other"), null);
    assert.equal(draftFieldTarget("unknownField"), null);
  });
  test("go-to only for fields on the step on screen; other steps get a hint, never a jump", () => {
    assert.equal(goToFieldLabel("url", 1), "Go to Link");
    // jumping steps would open the unsaved-changes dialog and lose the partner's typing
    assert.equal(goToFieldLabel("skills", 1), null);
    assert.equal(otherStepHint("skills", 1), "Skills is on the Skills step");
    assert.equal(otherStepHint("url", 1), null);
    assert.equal(goToFieldLabel("dateEnd"), "Go to End date");
    assert.equal(goToFieldLabel("other", 1), null);
  });
  test("a draft Ziqi cannot check shows the API's reason; other errors do not", () => {
    const badRequest = (data: unknown) => ({ response: { status: 400, data } });
    assert.equal(
      notCheckableReason(
        badRequest([
          {
            type: "ValidationException",
            message: "Choose the opportunity type to check this listing",
          },
        ]),
      ),
      "Choose the opportunity type to check this listing",
    );
    assert.equal(
      notCheckableReason(badRequest(undefined)),
      "Ziqi does not check this kind of listing yet.",
    );
    assert.equal(notCheckableReason({ response: { status: 500 } }), null);
  });
  test("only plain text suggestions can be applied", () => {
    assert.equal(applicableFormField("url"), "uRL");
    assert.equal(applicableFormField("summary"), "summary");
    assert.equal(applicableFormField("dateStart"), null);
    assert.equal(applicableFormField("skills"), null);
    assert.equal(
      suggestionValueFor("title", `  ${"x".repeat(200)} `).length,
      150,
    );
  });
});

describe("the draft", () => {
  test("a step's form gives only its own fields", () => {
    const stale = {
      title: "stale title",
      dateStart: "2026-12-01",
      skills: ["a"],
    };
    assert.deepEqual(pickDraftFields(2, stale), { dateStart: "2026-12-01" });
    assert.deepEqual(pickDraftFields(1, stale), { title: "stale title" });
    assert.deepEqual(pickDraftFields(4, stale), { skills: ["a"] });
    assert.deepEqual(pickDraftFields(3, stale), {});
  });
  test("an empty form is not checked", () => {
    assert.equal(isDraftEmpty({ title: " ", uRL: "", summary: null }), true);
    assert.equal(isDraftEmpty({ title: "Budgeting" }), false);
  });
  test("the key ignores key order", () => {
    assert.equal(
      draftKey({ title: "a", uRL: "b" }),
      draftKey({ uRL: "b", title: "a" }),
    );
    assert.notEqual(draftKey({ title: "a" }), draftKey({ title: "b" }));
  });
  test("hard gates come first, otherwise the API's order", () => {
    const fix = (criterion: string, hardGate: boolean): DraftCheckFix => ({
      criterion,
      field: "other",
      hardGate,
      reason: criterion,
      recommendation: null,
    });
    assert.deepEqual(
      sortFixes([fix("a", false), fix("b", true), fix("c", false)]).map(
        (f) => f.criterion,
      ),
      ["b", "a", "c"],
    );
    assert.deepEqual(sortFixes(null), []);
  });
  test("a 404 means the API switch is off", () => {
    assert.equal(isSwitchedOff({ response: { status: 404 } }), true);
    assert.equal(isSwitchedOff({ response: { status: 500 } }), false);
    assert.equal(isSwitchedOff(new Error("x")), false);
  });
});

// Manual timers: `tick()` fires every pending timer.
const manualTimers = () => {
  let next = 1;
  const pending = new Map<number, () => void>();
  const timers: DebouncedCheckTimers = {
    set: (callback) => {
      const id = next++;
      pending.set(id, callback);
      return id;
    },
    clear: (handle) => pending.delete(handle as number),
  };
  const tick = () => {
    const due = [...pending.values()];
    pending.clear();
    due.forEach((callback) => callback());
  };
  return { timers, tick, count: () => pending.size };
};

const flush = () => new Promise((resolve) => setImmediate(resolve));

describe("debounced, cancellable check", () => {
  test("only the last value within the delay is sent", async () => {
    const { timers, tick, count } = manualTimers();
    const sent: string[] = [];
    const results: string[] = [];
    const check = createDebouncedCheck<string, string>({
      run: async (input) => {
        sent.push(input);
        return `ok:${input}`;
      },
      onResult: (result) => results.push(result),
      onError: () => assert.fail("no error expected"),
      timers,
    });
    check.schedule("a");
    check.schedule("ab");
    check.schedule("abc");
    assert.equal(count(), 1);
    tick();
    await flush();
    assert.deepEqual(sent, ["abc"]);
    assert.deepEqual(results, ["ok:abc"]);
    assert.equal(check.pending, false);
  });

  test("a change aborts the request in flight and drops its answer", async () => {
    const { timers, tick } = manualTimers();
    const signals: AbortSignal[] = [];
    const resolvers: ((value: string) => void)[] = [];
    const results: string[] = [];
    const check = createDebouncedCheck<string, string>({
      run: (_input, signal) => {
        signals.push(signal);
        return new Promise<string>((resolve) => resolvers.push(resolve));
      },
      onResult: (result) => results.push(result),
      onError: () => assert.fail("no error expected"),
      timers,
    });
    check.schedule("first");
    tick();
    check.schedule("second");
    assert.equal(signals[0]?.aborted, true);
    tick();
    resolvers[0]?.("late first");
    resolvers[1]?.("second");
    await flush();
    assert.deepEqual(results, ["second"]);
  });

  test("errors reach onError; cancel stops everything", async () => {
    const { timers, tick, count } = manualTimers();
    const errors: unknown[] = [];
    const check = createDebouncedCheck<string, string>({
      run: async () => {
        throw new Error("boom");
      },
      onResult: () => assert.fail("no result expected"),
      onError: (error) => errors.push(error),
      timers,
    });
    check.schedule("x");
    tick();
    await flush();
    assert.equal(errors.length, 1);

    check.schedule("y");
    check.cancel();
    assert.equal(count(), 0);
    assert.equal(check.pending, false);
  });
});
