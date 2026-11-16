// The next batch for the reviewer mode, without the courses the reviewer skipped this session.
// Decided courses leave Pending on the server, but skipped ones stay, and they sort first, so
// page 1 alone would hand the same skipped courses back forever. Pure, so it is unit tested.

export interface ReviewPage<T> {
  totalCount: number;
  items: T[];
}

export interface ReviewBatch<T> {
  items: T[];
  /** Pending in all, skipped ones included. */
  totalCount: number;
  /** Pending courses not in this batch and not skipped: what "the next batch" could still bring. */
  newAfterBatch: number;
}

export const fetchReviewBatch = async <T extends { id: string }>(
  fetchPage: (pageNumber: number, pageSize: number) => Promise<ReviewPage<T>>,
  skipped: ReadonlySet<string>,
  batchSize: number,
  maxPages = 40,
): Promise<ReviewBatch<T>> => {
  const items: T[] = [];
  const seen = new Set<string>();
  let totalCount = 0;
  let skippedSeen = 0;
  let sawAll = false;

  for (let page = 1; page <= maxPages; page++) {
    const result = await fetchPage(page, batchSize);
    totalCount = result.totalCount;
    for (const item of result.items) {
      if (seen.has(item.id)) continue;
      seen.add(item.id);
      if (skipped.has(item.id)) skippedSeen++;
      else if (items.length < batchSize) items.push(item);
    }
    if (
      result.items.length < batchSize ||
      page * batchSize >= result.totalCount
    ) {
      sawAll = true;
      break;
    }
    if (items.length >= batchSize) break;
  }

  // a skipped course decided elsewhere meanwhile no longer counts against what is left
  const skippedPending = sawAll ? skippedSeen : skipped.size;
  return {
    items,
    totalCount,
    newAfterBatch: Math.max(0, totalCount - items.length - skippedPending),
  };
};
