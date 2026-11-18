// node --test scripts/test-anonymous-list.mjs: the signed-out reader's list as explorer-interop.js keeps it
// (Fhi.Metadata-emsc0.3.12). The .NET side never sees localStorage, so what the module does with it is tested here.
import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';

const KEY = 'Fhi.Munin.Explorer:anonymous-list';
const MODULE = new URL('../src/Fhi.Munin.Explorer/wwwroot/explorer-interop.js', import.meta.url);

// One store, as one browser profile has: every module instance below is a tab over it.
const store = new Map();
const failing = { get: false, set: false };
const listeners = new Set();
globalThis.localStorage = {
  getItem: key => { if (failing.get) throw new Error('SecurityError'); return store.has(key) ? store.get(key) : null; },
  setItem: (key, value) => { if (failing.set) throw new Error('QuotaExceededError'); store.set(key, String(value)); },
  removeItem: key => { if (failing.set) throw new Error('QuotaExceededError'); store.delete(key); },
};
globalThis.window = {
  addEventListener: (type, fn) => { if (type === 'storage') listeners.add(fn); },
  removeEventListener: (type, fn) => { if (type === 'storage') listeners.delete(fn); },
};
globalThis.document ??= { addEventListener() {}, removeEventListener() {} };

let tabs = 0;
const tab = () => import(`${MODULE.href}?tab=${++tabs}`);

const VARIABLE = 'b7c1f4a2-5d38-4e6b-9c02-8a1e3f7d5b90';
const DATASAMLING = '3e5a8c11-7b42-49df-a6c8-1d904f2e6b73';
const row = (variabelId, datasamlingId = null, extra = {}) => ({ variabelId, datasamlingId, ...extra });
const stored = () => (store.has(KEY) ? JSON.parse(store.get(KEY)) : null);

beforeEach(() => {
  store.clear();
  failing.get = false;
  failing.set = false;
  listeners.clear();
});

test('Status_WhenAnyCallAnswers_ThenItCarriesTheFieldsDotNetReads', async () => {
  const m = await tab();
  assert.deepEqual(Object.keys(m.anonymousListCount()).sort(), ['count', 'foreign', 'full', 'kept', 'ok', 'stillStored']);
});

test('Keys_WhenRead_ThenTheyAreSpelledAsBrowserListKeysParsesThem', async () => {
  // The same two literals BrowserListStoreTest parses on the .NET side.
  const m = await tab();
  m.anonymousListAdd(row(VARIABLE, DATASAMLING), 10);
  m.anonymousListAdd(row(VARIABLE), 10);
  assert.deepEqual(m.anonymousListKeys(0, 10), [`${VARIABLE}|${DATASAMLING}`, `${VARIABLE}|`]);
});

test('RemoveKeys_WhenGivenTheSpellingDotNetSends_ThenExactlyThoseRowsGo', async () => {
  // The same two literals BrowserListStoreTest asserts RemoveKeysAsync sends.
  const m = await tab();
  m.anonymousListAdd(row(VARIABLE, DATASAMLING), 10);
  m.anonymousListAdd(row(VARIABLE), 10);
  m.anonymousListAdd(row('other'), 10);
  assert.equal(m.anonymousListRemoveKeys([`${VARIABLE}|${DATASAMLING}`, `${VARIABLE}|`]).count, 1);
  assert.deepEqual(stored().items.map(i => i.variabelId), ['other']);
});

test('AddRemoveClear_WhenCalled_ThenStorageFollowsAndARepeatIsNotADuplicate', async () => {
  const m = await tab();
  assert.equal(m.anonymousListAdd(row('a', 'd'), 10).count, 1);
  assert.equal(m.anonymousListAdd(row('a', 'd'), 10).count, 1);
  assert.equal(m.anonymousListAdd(row('b'), 10).count, 2);
  assert.deepEqual(stored(), { v: 1, items: [row('a', 'd'), row('b')] });
  assert.equal(m.anonymousListRemove('a', 'd').count, 1);
  assert.deepEqual(m.anonymousListRemoveKeys(['b|']), { ok: true, count: 0, kept: true, full: false, foreign: false, stillStored: false });
  assert.equal(store.has(KEY), false);
  m.anonymousListAdd(row('c'), 10);
  assert.equal(m.anonymousListClear().count, 0);
  assert.equal(store.has(KEY), false);
});

test('Add_WhenTheListIsAtItsCeiling_ThenItIsRefusedAndNothingChanges', async () => {
  const m = await tab();
  m.anonymousListAdd(row('a'), 2);
  m.anonymousListAdd(row('b'), 2);
  assert.deepEqual(m.anonymousListAdd(row('c'), 2), { ok: false, count: 2, kept: true, full: true, foreign: false, stillStored: false });
  assert.equal(stored().items.length, 2);
});

for (const [name, raw] of [
  ['AnotherVersion', '{"v":2,"items":[]}'],
  ['Garbage', 'not json at all'],
  ['ItemsThatAreNotAnArray', '{"v":1,"items":"x"}'],
  ['ANullItem', '{"v":1,"items":[null]}'],
  ['AnItemWithNoVariable', '{"v":1,"items":[{"datasamlingId":"d"}]}'],
  ['AnItemThatIsANumber', '{"v":1,"items":[7]}'],
]) {
  test(`Load_WhenTheKeyHolds${name}_ThenItIsForeignAndNeverOverwritten`, async () => {
    const m = await tab();
    store.set(KEY, raw);
    assert.deepEqual(m.anonymousListCount(), { ok: true, count: 0, kept: false, full: false, foreign: true, stillStored: false });
    const added = m.anonymousListAdd(row('a'), 10);
    assert.equal(added.foreign, true);
    assert.equal(added.kept, false);
    assert.equal(added.count, 1);
    m.anonymousListClear();
    assert.equal(store.get(KEY), raw);
  });
}

test('Load_WhenGetItemThrows_ThenTheListLivesForThePageAndSaysItIsNotKept', async () => {
  const m = await tab();
  failing.get = true;
  const added = m.anonymousListAdd(row('a'), 10);
  assert.equal(added.count, 1);
  assert.equal(added.kept, false);
  assert.equal(m.anonymousListCount().count, 1);
});

test('Save_WhenSetItemThrowsAndThenWorks_ThenKeptRecoversWithEveryRow', async () => {
  const m = await tab();
  failing.set = true;
  assert.equal(m.anonymousListAdd(row('a'), 10).kept, false);
  failing.set = false;
  const status = m.anonymousListAdd(row('b'), 10);
  assert.equal(status.kept, true);
  assert.deepEqual(stored().items.map(i => i.variabelId), ['a', 'b']);
});

test('Load_WhenStorageRefusedOnceAndAnotherTabWroteSince_ThenTheOtherTabsRowsSurvive', async () => {
  // A copy held for the page lays its own changes over the stored list; it never stands in for it.
  const first = await tab();
  const second = await tab();
  failing.set = true;
  first.anonymousListAdd(row('mine'), 10);
  failing.set = false;
  second.anonymousListAdd(row('theirs'), 10);
  first.anonymousListAdd(row('later'), 10);
  assert.deepEqual(stored().items.map(i => i.variabelId).sort(), ['later', 'mine', 'theirs']);
});

test('Remove_WhenStorageRefusedIt_ThenTheRowStaysGoneOnThisPage', async () => {
  const m = await tab();
  m.anonymousListAdd(row('a'), 10);
  failing.set = true;
  assert.equal(m.anonymousListRemove('a', null).count, 0);
  assert.equal(m.anonymousListCount().count, 0);
});

test('TwoTabs_WhenEachAddsInTurn_ThenNoRowIsLost', async () => {
  const first = await tab();
  const second = await tab();
  first.anonymousListAdd(row('a'), 10);
  second.anonymousListAdd(row('b'), 10);
  first.anonymousListAdd(row('c'), 10);
  second.anonymousListRemove('a', null);
  assert.deepEqual(stored().items.map(i => i.variabelId), ['b', 'c']);
  assert.equal(first.anonymousListCount().count, 2);
});

const utf8 = value => new TextEncoder().encode(JSON.stringify(value)).length;

test('Page_WhenRowsAreLong_ThenOneAnswerStaysUnderSixteenKilobytesOfUtf8', async () => {
  // Norwegian letters are two bytes each: a bound counted in characters would let these past it.
  const m = await tab();
  for (let n = 0; n < 20; n++) m.anonymousListAdd(row(`v${n}`, null, { variabelName: 'ø'.repeat(1500) }), 100);
  const page = m.anonymousListPage(0, 20);
  assert.ok(page.items.length > 0 && page.items.length < 20, `${page.items.length} rows`);
  assert.ok(utf8(page) <= 16_100, `${utf8(page)} bytes`);
  assert.equal(m.anonymousListPage(19, 20).items.length, 1);
});

test('Page_WhenOneRowIsOverTheBoundAlone_ThenItComesBackAsItsIdsAndCodeAndTheNextRowFollows', async () => {
  const m = await tab();
  m.anonymousListAdd(row('huge', 'd', { variabelCode: 'V_HUGE', variabelName: 'å'.repeat(20_000) }), 10);
  m.anonymousListAdd(row('next', null, { variabelName: 'Neste' }), 10);
  const page = m.anonymousListPage(0, 10);
  assert.deepEqual(page.items[0], { variabelId: 'huge', datasamlingId: 'd', variabelCode: 'V_HUGE', truncated: true });
  assert.equal(page.items[1].variabelName, 'Neste');
  assert.ok(utf8(page) <= 16_000);
});

test('TwoTabs_WhenOneIsRefusedAndTheOtherClears_ThenTheClearHoldsAndOnlyTheUnsavedRowIsAdded', async () => {
  // (a) The refused tab holds only its own unsaved change, so its next good write cannot bring the cleared rows back.
  const a = await tab();
  const b = await tab();
  a.anonymousListAdd(row('x'), 10);
  a.anonymousListAdd(row('y'), 10);
  failing.set = true;
  a.anonymousListAdd(row('unsaved'), 10);
  failing.set = false;
  b.anonymousListClear();
  assert.equal(store.has(KEY), false);
  a.anonymousListAdd(row('later'), 10);
  assert.deepEqual(stored().items.map(i => i.variabelId), ['unsaved', 'later']);
  assert.equal(b.anonymousListCount().count, 2);
});

test('TwoTabs_WhenARefusedRemoveMeetsAnotherTabsReAdd_ThenThisPagesUnsavedChangeWinsForThatKey', async () => {
  // (b) Which came last cannot be known from here, so the page's own unsaved change wins for the key it touched.
  const c = await tab();
  const d = await tab();
  c.anonymousListAdd(row('x'), 10);
  c.anonymousListAdd(row('kept'), 10);
  failing.set = true;
  c.anonymousListRemove('x', null);
  failing.set = false;
  d.anonymousListRemove('x', null);
  d.anonymousListAdd(row('x'), 10);
  d.anonymousListAdd(row('theirs'), 10);
  c.anonymousListAdd(row('mine'), 10);
  assert.deepEqual(stored().items.map(i => i.variabelId).sort(), ['kept', 'mine', 'theirs']);
});

test('TwoTabs_WhenOneCarriesOverWhileTheOtherIsRefused_ThenTheCarriedRowsStayGone', async () => {
  // (c) A carry-over takes its rows out with removeKeys; a refused tab's later good write must not put them back.
  const a = await tab();
  const b = await tab();
  a.anonymousListAdd(row('x'), 10);
  a.anonymousListAdd(row('y'), 10);
  failing.set = true;
  a.anonymousListRemove('nothing', null);
  failing.set = false;
  b.anonymousListRemoveKeys(['x|', 'y|']);
  a.anonymousListAdd(row('z'), 10);
  assert.deepEqual(stored().items.map(i => i.variabelId), ['z']);
});

test('TwoTabs_WhenARefusedTabsRemovalIsSavedLater_ThenAnotherTabsNewRowsStay', async () => {
  const a = await tab();
  const b = await tab();
  a.anonymousListAdd(row('x'), 10);
  failing.set = true;
  a.anonymousListClear();
  failing.set = false;
  b.anonymousListAdd(row('new'), 10);
  a.anonymousListAdd(row('mine'), 10);
  assert.deepEqual(stored().items.map(i => i.variabelId), ['new', 'mine']);
});

test('Watch_WhenASecondCircuitListens_ThenItReplacesTheFirstAndOnlyItIsTold', async () => {
  const m = await tab();
  const told = [];
  m.watchAnonymousList({ invokeMethodAsync: name => { told.push(`first ${name}`); return Promise.resolve(); } });
  m.watchAnonymousList({ invokeMethodAsync: name => { told.push(`second ${name}`); return Promise.resolve(); } });
  assert.equal(listeners.size, 1);
  for (const fn of listeners) fn({ key: KEY });
  for (const fn of listeners) fn({ key: 'something else' });
  assert.deepEqual(told, ['second ChangedElsewhere']);
});

test('RemoveKeys_WhenTheRowsWereOnlyEverInThisPagesMemory_ThenNoneIsStillStored', async () => {
  // A carry-over of rows storage never held: nothing is left behind for another page to save twice.
  const m = await tab();
  failing.set = true;
  m.anonymousListAdd(row('a'), 10);
  const status = m.anonymousListRemoveKeys(['a|']);
  assert.equal(status.kept, false);
  assert.equal(status.stillStored, false);
  assert.equal(status.count, 0);
});

test('RemoveKeys_WhenTheRemovalIsRefusedForStoredRows_ThenTheyAreStillStored', async () => {
  const m = await tab();
  m.anonymousListAdd(row('a'), 10);
  failing.set = true;
  assert.equal(m.anonymousListRemoveKeys(['a|']).stillStored, true);
});

test('Write_WhenThisPagesUnsavedAddsMeetAnotherTabsFullList_ThenTheStoredRowsStayAndTheExcessAddsGo', async () => {
  // The ceiling holds in storage: another tab's rows are kept, and this page's newest unsaved adds are dropped.
  const a = await tab();
  const b = await tab();
  failing.set = true;
  a.anonymousListAdd(row('mine'), 2);
  failing.set = false;
  b.anonymousListAdd(row('x'), 2);
  b.anonymousListAdd(row('y'), 2);
  const status = a.anonymousListRemove('nothing', null);
  assert.equal(status.full, true);
  assert.equal(status.count, 2);
  assert.deepEqual(stored().items.map(i => i.variabelId), ['x', 'y']);
  assert.equal(status.kept, true);
});

// Many random runs of two tabs, each sometimes refused, against a small model of the rule the module states: the stored
// list stands for every key a tab has no unsaved change to, and a tab's unsaved change stands for the keys it touched.
test('TwoTabs_WhenRandomRefusalsInterleave_ThenStorageAlwaysMatchesTheModel', async () => {
  // mulberry32: an LCG's low bits alternate, which once sent nearly every step to one tab.
  let seed = 0x9e3779b9;
  const random = n => {
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return Math.floor((((t ^ (t >>> 14)) >>> 0) / 4294967296) * n);
  };
  const keysOf = () => (stored()?.items ?? []).map(i => i.variabelId);
  const acted = [0, 0];
  let steps = 0;

  for (let run = 0; run < 200; run++) {
    store.clear();
    const tabs = [await tab(), await tab()];
    const model = { stored: [], deltas: [{ added: [], removed: new Set() }, { added: [], removed: new Set() }] };
    const view = d => [...model.stored.filter(k => !d.removed.has(k)), ...d.added.filter(k => !model.stored.includes(k) || d.removed.has(k))]
      .filter((k, i, all) => all.indexOf(k) === i);

    for (let step = 0; step < 30; step++) {
      const t = random(2);
      acted[t]++;
      steps++;
      const d = model.deltas[t];
      const touched = new Set([...d.added, ...d.removed]);
      const key = `k${random(5)}`;
      const op = random(5);
      failing.set = random(3) === 0;
      const before = view(d);
      const storedBefore = [...model.stored];
      let next = null;
      let status;

      if (op <= 1) {
        status = tabs[t].anonymousListAdd(row(key), 100);
        if (!before.includes(key)) {
          d.removed.delete(key);
          d.added = [...d.added.filter(k => k !== key), key];
          next = [...before, key];
        }
      } else if (op <= 3) {
        status = tabs[t].anonymousListRemove(key, null);
        d.added = d.added.filter(k => k !== key);
        d.removed.add(key);
        next = before.filter(k => k !== key);
      } else {
        status = tabs[t].anonymousListClear();
        d.added = [];
        before.forEach(k => d.removed.add(k));
        next = [];
      }
      touched.add(key);
      if (op > 3) before.forEach(k => touched.add(k));

      const wrote = next !== null && !failing.set;
      if (wrote) {
        model.stored = next;
        model.deltas[t] = { added: [], removed: new Set() };
      }
      failing.set = false;

      const where = `run ${run} step ${step}`;
      assert.deepEqual(keysOf(), model.stored, where);
      assert.equal(new Set(keysOf()).size, keysOf().length, `duplicate in ${where}`);
      assert.equal(status.count, (next ?? before).length, `count in ${where}`);
      const unsaved = model.deltas[t].added.length > 0 || model.deltas[t].removed.size > 0;
      assert.equal(status.kept, !unsaved, `kept in ${where}`);
      const other = 1 - t;
      assert.deepEqual(tabs[other].anonymousListKeys(0, 100), view(model.deltas[other]).map(k => `${k}|`), `other tab in ${where}`);

      // The rule itself, beside the formula: a good write moves only the keys the writer touched.
      if (wrote) {
        for (const k of ['k0', 'k1', 'k2', 'k3', 'k4'].filter(k => !touched.has(k))) {
          assert.equal(keysOf().includes(k), storedBefore.includes(k), `untouched ${k} moved in ${where}`);
        }
      }
    }
  }

  // Each tab acts often enough for the interleavings to mean something; the counts are printed for the report.
  console.log(`steps per tab: ${acted[0]} and ${acted[1]} of ${steps}`);
  assert.ok(acted.every(n => n >= steps * 0.3), `steps per tab ${acted}`);
});
