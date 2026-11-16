import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { fetchReviewBatch } from "./courseQualityReviewBatch";

const queue = (count: number) =>
  Array.from({ length: count }, (_, i) => ({ id: `c${i}` }));

/** A Pending search over `all`, paged like the API. */
const pager = (all: { id: string }[]) => {
  const calls: number[] = [];
  const fetchPage = async (pageNumber: number, pageSize: number) => {
    calls.push(pageNumber);
    return {
      totalCount: all.length,
      items: all.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
    };
  };
  return { fetchPage, calls };
};

describe("review batch", () => {
  test("first batch: page 1 as is", async () => {
    const { fetchPage, calls } = pager(queue(120));
    const batch = await fetchReviewBatch(fetchPage, new Set(), 50);
    assert.equal(batch.items.length, 50);
    assert.equal(batch.items[0]!.id, "c0");
    assert.equal(batch.newAfterBatch, 70);
    assert.deepEqual(calls, [1]);
  });

  test("skipped courses never come back, even when they fill page 1", async () => {
    const all = queue(120);
    const skipped = new Set(all.slice(0, 50).map((c) => c.id));
    const { fetchPage, calls } = pager(all);
    const batch = await fetchReviewBatch(fetchPage, skipped, 50);
    assert.equal(batch.items[0]!.id, "c50");
    assert.equal(batch.items.length, 50);
    assert.ok(batch.items.every((c) => !skipped.has(c.id)));
    assert.equal(batch.newAfterBatch, 20);
    assert.deepEqual(calls, [1, 2]);
  });

  test("only skipped courses left: an empty batch, nothing new", async () => {
    const all = queue(30);
    const { fetchPage } = pager(all);
    const batch = await fetchReviewBatch(
      fetchPage,
      new Set(all.map((c) => c.id)),
      50,
    );
    assert.equal(batch.items.length, 0);
    assert.equal(batch.totalCount, 30);
    assert.equal(batch.newAfterBatch, 0);
  });

  test("a skipped course decided elsewhere is not counted", async () => {
    const all = queue(10);
    const { fetchPage } = pager(all);
    const batch = await fetchReviewBatch(fetchPage, new Set(["c0", "gone"]), 5);
    assert.deepEqual(
      batch.items.map((c) => c.id),
      ["c1", "c2", "c3", "c4", "c5"],
    );
    assert.equal(batch.newAfterBatch, 4);
  });
});
