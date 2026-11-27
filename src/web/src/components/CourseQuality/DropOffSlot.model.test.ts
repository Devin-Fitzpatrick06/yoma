import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  collectingDropOff,
  DROP_OFF_COLLECTING_NOTE,
  formatDropOffRate,
  toDropOffView,
} from "./DropOffSlot.model";
import {
  formatCount,
  pathwayHealthExtraNote,
} from "./PathwayHealthPanel.model";

describe("toDropOffView", () => {
  test("Collecting never shows a number, even when a rate came along", () => {
    const view = toDropOffView({
      status: "Collecting",
      rate: 0.4,
      note: "Collecting from the pilot",
    });
    assert.deepEqual(view, {
      kind: "collecting",
      note: "Collecting from the pilot",
    });
  });

  test("Available with a rate shows a whole percent", () => {
    const view = toDropOffView({
      status: "Available",
      rate: 0.184,
      note: "Across steps with at least 20 youth",
    });
    assert.equal(view.kind, "available");
    if (view.kind !== "available") return;
    assert.equal(view.percent, 18);
    assert.equal(view.text, "18%");
    assert.equal(view.note, "Across steps with at least 20 youth");
  });

  test("Available without a usable rate falls back to Collecting", () => {
    assert.equal(
      toDropOffView({ status: "Available", rate: null, note: "x" }).kind,
      "collecting",
    );
    assert.equal(
      toDropOffView({ status: "Available", rate: Number.NaN, note: "x" }).kind,
      "collecting",
    );
  });

  test("missing drop-off or a blank note uses the standard note", () => {
    assert.deepEqual(toDropOffView(null), {
      kind: "collecting",
      note: DROP_OFF_COLLECTING_NOTE,
    });
    assert.deepEqual(
      toDropOffView({ status: "Collecting", rate: null, note: "  " }),
      { kind: "collecting", note: DROP_OFF_COLLECTING_NOTE },
    );
  });

  test("the client-side Collecting state has no rate", () => {
    assert.deepEqual(collectingDropOff(), {
      status: "Collecting",
      rate: null,
      note: DROP_OFF_COLLECTING_NOTE,
    });
  });
});

describe("formatDropOffRate", () => {
  test("clamps to 0–100%", () => {
    assert.equal(formatDropOffRate(-0.2), "0%");
    assert.equal(formatDropOffRate(1.7), "100%");
    assert.equal(formatDropOffRate(0.005), "1%");
  });
});

describe("pathway health helpers", () => {
  test("formatCount groups thousands and never goes negative", () => {
    assert.equal(formatCount(20417), "20,417");
    assert.equal(formatCount(-3), "0");
    assert.equal(formatCount(null), "0");
    assert.equal(formatCount(Number.POSITIVE_INFINITY), "0");
  });

  test("the API note shows only when it adds to the drop-off note", () => {
    const dropOff = collectingDropOff();
    assert.equal(
      pathwayHealthExtraNote({
        stepsTracked: 1,
        events: 2,
        dropOff,
        note: "collecting from the pilot",
      }),
      null,
    );
    assert.equal(
      pathwayHealthExtraNote({ stepsTracked: 1, events: 2, dropOff, note: "" }),
      null,
    );
    assert.equal(
      pathwayHealthExtraNote({
        stepsTracked: 1,
        events: 2,
        dropOff,
        note: "Events since 1 Dec",
      }),
      "Events since 1 Dec",
    );
  });
});
