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
  assert.deepEqual(Object.keys(m.anonymousListCount()).sort(), ['count', 'foreign', 'full', 'kept', 'ok']);
});

test('Keys_WhenRead_ThenTheyAreSpelledAsBrowserListKeysParsesThem', async () => {
  // The same two literals BrowserListStoreTest parses on the .NET side.
  const m = await tab();
  m.anonymousListAdd(row(VARIABLE, DATASAMLING), 10);
  m.anonymousListAdd(row(VARIABLE), 10);
  assert.deepEqual(m.anonymousListKeys(0, 10), [`${VARIABLE}|${DATASAMLING}`, `${VARIABLE}|`]);
});

test('AddRemoveClear_WhenCalled_ThenStorageFollowsAndARepeatIsNotADuplicate', async () => {
  const m = await tab();
  assert.equal(m.anonymousListAdd(row('a', 'd'), 10).count, 1);
  assert.equal(m.anonymousListAdd(row('a', 'd'), 10).count, 1);
  assert.equal(m.anonymousListAdd(row('b'), 10).count, 2);
  assert.deepEqual(stored(), { v: 1, items: [row('a', 'd'), row('b')] });
  assert.equal(m.anonymousListRemove('a', 'd').count, 1);
  assert.deepEqual(m.anonymousListRemoveKeys(['b|']), { ok: true, count: 0, kept: true, full: false, foreign: false });
  assert.equal(store.has(KEY), false);
  m.anonymousListAdd(row('c'), 10);
  assert.equal(m.anonymousListClear().count, 0);
  assert.equal(store.has(KEY), false);
});

test('Add_WhenTheListIsAtItsCeiling_ThenItIsRefusedAndNothingChanges', async () => {
  const m = await tab();
  m.anonymousListAdd(row('a'), 2);
  m.anonymousListAdd(row('b'), 2);
  assert.deepEqual(m.anonymousListAdd(row('c'), 2), { ok: false, count: 2, kept: true, full: true, foreign: false });
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
    assert.deepEqual(m.anonymousListCount(), { ok: true, count: 0, kept: false, full: false, foreign: true });
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

test('Page_WhenRowsAreLong_ThenOneAnswerStaysNearSixteenKilobytes', async () => {
  const m = await tab();
  for (let n = 0; n < 20; n++) m.anonymousListAdd(row(`v${n}`, null, { variabelName: 'x'.repeat(2000) }), 100);
  const page = m.anonymousListPage(0, 20);
  assert.ok(page.items.length > 0 && page.items.length < 20, `${page.items.length} rows`);
  assert.ok(JSON.stringify(page).length < 18_000);
  assert.equal(m.anonymousListPage(19, 20).items.length, 1);
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
