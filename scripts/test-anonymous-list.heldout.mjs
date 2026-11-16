// node --test scripts/test-anonymous-list.heldout.mjs: held-out checks of the signed-out reader's list
// (Fhi.Metadata-emsc0.3.12), written from the requirements alone. They pin size, two-tab and foreign-value behaviour.
import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';

const KEY = 'Fhi.Munin.Explorer:anonymous-list';
const MODULE = new URL('../src/Fhi.Munin.Explorer/wwwroot/explorer-interop.js', import.meta.url);
const LIMIT = 16000;

// One shared store is one browser profile; each imported module instance is a tab over it.
const store = new Map();
const failing = { set: false };
globalThis.localStorage = {
  getItem: key => (store.has(key) ? store.get(key) : null),
  setItem: (key, value) => { if (failing.set) throw new Error('QuotaExceededError'); store.set(key, String(value)); },
  removeItem: key => { if (failing.set) throw new Error('QuotaExceededError'); store.delete(key); },
};
globalThis.window = { addEventListener() {}, removeEventListener() {} };
globalThis.document ??= { addEventListener() {}, removeEventListener() {} };

let tabs = 0;
const tab = () => import(`${MODULE.href}?heldout=${++tabs}`);
const bytes = answer => Buffer.byteLength(JSON.stringify(answer), 'utf8');
const guid = n => `${n.toString(16).padStart(8, '0')}-5d38-4e6b-9c02-8a1e3f7d5b90`;
const row = (n, extra = {}) => ({ variabelId: guid(n), datasamlingId: n % 2 ? guid(n + 100000) : null, ...extra });
const key = r => `${r.variabelId}|${r.datasamlingId ?? ''}`;
const storedRaw = () => (store.has(KEY) ? store.get(KEY) : null);
const storedKeys = () => JSON.parse(storedRaw()).items.map(key);

const realistic = n => row(n, {
  kildeId: guid(n + 200000),
  variabelName: `Årlig gjennomsnittlig antall sykehusinnleggelser for pasienter bosatt på Østlandet etter ${n}. kontakt, justert for alder og kjønn, ` +
    'inkludert dagbehandling og øyeblikkelig hjelp ved somatiske avdelinger i spesialisthelsetjenesten, målt per hundre tusen innbyggere'.repeat(2),
  datasamlingName: 'Norsk pasientregister – somatikk, avtalespesialister og private ideelle sykehus med driftsavtale',
  variabelCode: `NPR_SOM_${String(n).padStart(6, '0')}_ÆØÅ`,
  validFrom: '2019-01-01T00:00:00+01:00',
  addedAt: '2026-11-03T10:15:42.123Z',
});

beforeEach(() => {
  store.clear();
  failing.set = false;
});

test('Size_WhenTwoThousandRealisticRowsAreStored_ThenNoKeysAnswerPassesSixteenKilobytes', async () => {
  // Blazor Server drops the circuit on a JS answer over 32 KB, so every answer keeps well under half of that.
  const m = await tab();
  for (let n = 1; n <= 2000; n++) m.anonymousListAdd(realistic(n), 2000);
  assert.equal(m.anonymousListCount().count, 2000);
  const seen = [];
  while (seen.length < 2000) {
    const keys = m.anonymousListKeys(seen.length, 100);
    assert.ok(bytes(keys) <= LIMIT, `keys answer was ${bytes(keys)} bytes`);
    assert.ok(keys.length > 0, 'a keys call must make progress');
    seen.push(...keys);
  }
  assert.deepEqual(seen, Array.from({ length: 2000 }, (_, i) => key(realistic(i + 1))));
});

test('Size_WhenTwoThousandRealisticRowsAreStored_ThenPagingReturnsEveryRowOnceUnderSixteenKilobytes', async () => {
  const m = await tab();
  for (let n = 1; n <= 2000; n++) m.anonymousListAdd(realistic(n), 2000);
  const seen = [];
  let calls = 0;
  while (seen.length < 2000 && calls++ < 5000) {
    const page = m.anonymousListPage(seen.length, 500);
    assert.ok(bytes(page) <= LIMIT, `page answer was ${bytes(page)} bytes`);
    assert.ok(page.items.length > 0, 'a page call must make progress');
    seen.push(...page.items.map(key));
  }
  assert.equal(seen.length, 2000);
  assert.equal(new Set(seen).size, 2000);
  assert.deepEqual(seen, Array.from({ length: 2000 }, (_, i) => key(realistic(i + 1))));
});

test('Size_WhenOneRowAloneIsOverTheLimit_ThenPagingStillReachesTheRowsAfterIt', async () => {
  // A caller that advances by the rows it got would otherwise stall on the first oversized row.
  const m = await tab();
  m.anonymousListAdd(row(1), 10);
  m.anonymousListAdd(row(2, { variabelName: 'æ'.repeat(40000) }), 10);
  m.anonymousListAdd(row(3), 10);
  const seen = [];
  for (let calls = 0; seen.length < 3 && calls < 10; calls++) {
    const page = m.anonymousListPage(seen.length, 10);
    assert.ok(bytes(page) <= LIMIT, `page answer was ${bytes(page)} bytes`);
    assert.ok(page.items.length > 0);
    seen.push(...page.items.map(key));
  }
  assert.deepEqual(seen, [key(row(1)), key(row(2)), key(row(3))]);
});

test('TwoTabs_WhenOneTabsWriteWasRefusedAndAnotherClearedTheList_ThenTheClearHolds', async () => {
  const a = await tab();
  const b = await tab();
  a.anonymousListAdd(row(1), 10);
  a.anonymousListAdd(row(2), 10);
  failing.set = true;
  a.anonymousListAdd(row(3), 10);
  failing.set = false;
  b.anonymousListClear();
  a.anonymousListAdd(row(4), 10);
  const keys = storedKeys();
  assert.ok(!keys.includes(key(row(1))) && !keys.includes(key(row(2))), `cleared rows came back: ${keys}`);
  assert.ok(keys.includes(key(row(4))));
});

test('TwoTabs_WhenOneTabCarriesOverWhileAnotherHasARefusedWrite_ThenTheRemovalHoldsForUntouchedKeys', async () => {
  const a = await tab();
  const b = await tab();
  for (let n = 1; n <= 4; n++) a.anonymousListAdd(row(n), 10);
  failing.set = true;
  a.anonymousListAdd(row(5), 10);
  failing.set = false;
  b.anonymousListRemoveKeys([key(row(1)), key(row(2))]);
  a.anonymousListAdd(row(6), 10);
  const keys = storedKeys();
  assert.ok(!keys.includes(key(row(1))) && !keys.includes(key(row(2))), `removed rows came back: ${keys}`);
  for (const n of [3, 4, 6]) assert.ok(keys.includes(key(row(n))), `row ${n} was lost`);
});

test('TwoTabs_WhenAnotherTabAddedARow_ThenThisTabsNextChangeKeepsIt', async () => {
  const a = await tab();
  const b = await tab();
  a.anonymousListAdd(row(1), 10);
  b.anonymousListAdd(row(2), 10);
  a.anonymousListAdd(row(3), 10);
  assert.deepEqual(storedKeys().sort(), [key(row(1)), key(row(2)), key(row(3))].sort());
});

// A fresh add shows what shape this module writes, so a foreign value can be one step away from it.
const ownVersion = async () => {
  const m = await tab();
  m.anonymousListAdd(row(1), 10);
  const v = JSON.parse(storedRaw()).v;
  store.clear();
  return v;
};

test('Foreign_WhenTheKeyHoldsSomethingElse_ThenNoCallOverwritesItAndCallsSayNotOurs', async () => {
  const v = await ownVersion();
  const foreign = [
    '{"v":2,"items":[],"extra":true}',
    '{"v":999,"rows":[{"variabelId":"x"}]}',
    'not json at all {{{',
    `{"v":${v},"items":[1,"two",null]}`,
    `{"v":${v},"items":[{"variabelId":5}]}`,
    `{"v":${v},"items":[{"variabelId":"ok","datasamlingId":null},"stray"]}`,
  ];
  for (const raw of foreign) {
    store.set(KEY, raw);
    const m = await tab();
    const answers = [
      m.anonymousListCount(),
      m.anonymousListAdd(row(7), 10),
      m.anonymousListRemove(guid(7), null),
      m.anonymousListRemoveKeys([key(row(7))]),
      m.anonymousListClear(),
    ];
    for (const answer of answers) assert.equal(answer.foreign, true, `not reported as foreign for ${raw}`);
    m.anonymousListKeys(0, 10);
    m.anonymousListPage(0, 10);
    assert.equal(storedRaw(), raw, `value was overwritten: ${raw}`);
  }
});

test('Foreign_WhenTheKeyIsAbsentOrOurOwnList_ThenCallsDoNotReportForeign', async () => {
  const m = await tab();
  assert.equal(m.anonymousListCount().foreign, false);
  assert.equal(m.anonymousListAdd(row(1), 10).foreign, false);
  assert.equal(m.anonymousListCount().foreign, false);
});

test('Ceiling_WhenAddingPastMax_ThenTheRowIsRefusedAsFullAndNothingIsStored', async () => {
  const m = await tab();
  for (let n = 1; n <= 3; n++) assert.equal(m.anonymousListAdd(row(n), 3).ok, true);
  const refused = m.anonymousListAdd(row(4), 3);
  assert.equal(refused.full, true);
  assert.equal(refused.ok, false);
  assert.equal(storedKeys().length, 3);
  assert.ok(!storedKeys().includes(key(row(4))));
});

test('Ceiling_WhenTheRowIsAlreadyStoredAtMax_ThenAddingItAgainIsNotFull', async () => {
  const m = await tab();
  for (let n = 1; n <= 3; n++) m.anonymousListAdd(row(n), 3);
  const again = m.anonymousListAdd(row(2), 3);
  assert.equal(again.full, false);
  assert.equal(storedKeys().length, 3);
});

test('Ceiling_WhenTwoTabsAddAlternately_ThenTogetherTheyNeverStoreMoreThanMax', async () => {
  const a = await tab();
  const b = await tab();
  for (let n = 1; n <= 6; n++) {
    (n % 2 ? a : b).anonymousListAdd(row(n), 4);
    assert.ok(storedKeys().length <= 4);
  }
  assert.equal(storedKeys().length, 4);
});

test('Ceiling_WhenOneTabsWriteWasRefusedWhileAnotherFilledTheList_ThenTheLaterAddStaysWithinMax', async () => {
  const a = await tab();
  const b = await tab();
  failing.set = true;
  a.anonymousListAdd(row(1), 3);
  failing.set = false;
  for (let n = 11; n <= 13; n++) b.anonymousListAdd(row(n), 3);
  a.anonymousListAdd(row(2), 3);
  const keys = storedKeys();
  assert.ok(keys.length <= 3, `stored ${keys.length} rows over a max of 3`);
});
