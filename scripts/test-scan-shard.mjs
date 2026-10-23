// node --test scripts/test-scan-shard.mjs: the CI split of the browser scans (Fhi.Metadata-ln1s6).
import test from 'node:test';
import assert from 'node:assert/strict';
import { isFirstShard, ownsItem, packByCost, parseShard } from './scan-shard.mjs';

test('unset takes every item, so a local run is unchanged', () => {
  // '' and not undefined: an explicit undefined takes the default, which is CI's own SCAN_SHARD.
  assert.deepEqual(parseShard(''), { index: 1, count: 1 });
  assert.ok([0, 1, 2, 3].every(i => ownsItem(i, parseShard(''))));
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
  assert.deepEqual(['', '1/3', '2/3', '3/3'].map(raw => isFirstShard(parseShard(raw))), [true, true, false, false]);
});

test('packing by cost gives every item one shard and spreads the heavy ones', () => {
  const costs = [8, 138, 8, 112, 8, 8, 12, 8, 8, 10];
  const shardOf = packByCost(costs, 4, 29);
  assert.ok(shardOf.every(k => k >= 1 && k <= 4));
  assert.notEqual(shardOf[1], shardOf[3]);
  const load = [29, 0, 0, 0];
  costs.forEach((cost, i) => { load[shardOf[i] - 1] += cost; });
  assert.ok(Math.max(...load) <= 138 + 8, `loads ${load}`);
  assert.deepEqual(packByCost(costs, 4, 29), shardOf);
});

test('one shard takes everything, and shard 1 is charged its extra work', () => {
  assert.deepEqual(packByCost([5, 9, 1], 1, 29), [1, 1, 1]);
  assert.deepEqual(packByCost([10, 10], 2, 29), [2, 2]);
});

test('the measured table names only states axe-states.mjs has', async () => {
  const [{ states }, { stateSeconds }] = await Promise.all([import('./axe-states.mjs'), import('./axe-state-seconds.mjs')]);
  assert.deepEqual(Object.keys(stateSeconds).filter(name => !Object.hasOwn(states, name)), []);
});

test('the heavy items are placed first, so small ones fill in around them', () => {
  const costs = [8, 8, 8, 8, 138, 112];
  const load = [0, 0];
  packByCost(costs, 2).forEach((k, i) => { load[k - 1] += costs[i]; });
  assert.ok(Math.max(...load) <= 144, `loads ${load}`);
});
