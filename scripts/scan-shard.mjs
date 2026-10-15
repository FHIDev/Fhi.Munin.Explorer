// SCAN_SHARD="k/n" splits a scan's work items across n CI jobs, item i going to job (i mod n) + 1. Unset, a scan
// takes every item, so a developer's local run is unchanged. A malformed value is a TOOLING failure, never a
// silent "all" or "none": a shard that scanned nothing would pass. (Fhi.Metadata-ln1s6)

export function parseShard(raw = process.env.SCAN_SHARD) {
  if (raw === undefined || raw === '') return { index: 1, count: 1 };
  const match = /^([1-9]\d*)\/([1-9]\d*)$/.exec(raw);
  const index = match ? Number(match[1]) : NaN;
  const count = match ? Number(match[2]) : NaN;
  if (!(count >= 1 && index >= 1 && index <= count)) {
    throw new Error(`SCAN_SHARD must be "k/n" with 1 <= k <= n, got "${raw}"`);
  }
  return { index, count };
}

export const ownsItem = (itemIndex, { index, count } = parseShard()) => itemIndex % count === index - 1;

export const isFirstShard = ({ index } = parseShard()) => index === 1;

export function describeShard(owned, total, { index, count } = parseShard()) {
  return count === 1 ? '' : `shard ${index}/${count}: ${owned} of ${total}`;
}
