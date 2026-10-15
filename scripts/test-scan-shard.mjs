// node --test scripts/test-scan-shard.mjs: the CI split of the browser scans (Fhi.Metadata-ln1s6).
import test from 'node:test';
import assert from 'node:assert/strict';
import { isFirstShard, ownsItem, parseShard } from './scan-shard.mjs';

test('unset takes every item, so a local run is unchanged', () => {
  assert.deepEqual(parseShard(undefined), { index: 1, count: 1 });
  assert.deepEqual(parseShard(''), { index: 1, count: 1 });
  assert.ok([0, 1, 2, 3].every(i => ownsItem(i, parseShard(undefined))));
});

test('three shards take every item exactly once between them', () => {
  const items = Array.from({ length: 40 }, (_, i) => i);
  const shards = [1, 2, 3].map(k => items.filter(i => ownsItem(i, parseShard(`${k}/3`))));
  assert.deepEqual(shards.flat().sort((a, b) => a - b), items);
  assert.deepEqual(shards.map(s => s.length), [14, 13, 13]);
});

test('a malformed value is refused rather than read as all or none', () => {
  // 01/3 too: the shell side compares the numerator as text, so a padded 1 would skip the reflow.
  for (const raw of ['0/3', '4/3', '1/0', '2', 'a/b', '1/3/2', '01/3']) {
    assert.throws(() => parseShard(raw), /SCAN_SHARD must be/, raw);
  }
});

test('only shard 1 is the first, which prints the n/a lines and measures the reflow', () => {
  assert.deepEqual([undefined, '1/3', '2/3', '3/3'].map(raw => isFirstShard(parseShard(raw))), [true, true, false, false]);
});
