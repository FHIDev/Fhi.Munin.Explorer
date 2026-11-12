// Keeps the state-to-page table whole: a state Varde cannot place would report on no page (DevInfra-og3.42).
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { states } from './axe-states.mjs';
import { criteriaOf, pageOf, pageOfState, resultLine, scanFinishedLine } from './axe-pages.mjs';

test('AxePages_WhenAStateExists_ThenItNamesAPage', () => {
  assert.deepEqual(Object.keys(states).filter(state => !pageOfState.has(state)), [], 'give each of these its Varde page in scripts/axe-pages.mjs');
});

test('AxePages_WhenTheTableNamesAState_ThenTheStateExists', () => {
  assert.deepEqual([...pageOfState.keys()].filter(state => !(state in states)), []);
});

test('AxePages_WhenATargetHasNoState_ThenItsPathNamesTheExplorer', () => {
  assert.equal(pageOf('http://localhost:5099/kilder', null), 'explorer/kildeutforsker');
  assert.equal(pageOf('http://localhost:5099/', null), 'explorer/variabelutforsker');
  assert.equal(pageOf('http://localhost:5099/elsewhere', null), null);
});

test('CriteriaOf_WhenTagsMixLevelsAndCriteria_ThenOnlyTheCriteriaAreKept', () => {
  assert.deepEqual(criteriaOf(['wcag2aa', 'wcag143', 'wcag21aa', 'cat.color', 'wcag412', 'wcag1410', 'wcag143']), ['1.4.10', '1.4.3', '4.1.2']);
});

// The devbox's report builder (DevInfra devbox/chart/files/a11y-report.mjs) reads these keys by name.
test('ResultLine_WhenWritten_ThenItCarriesTheKeysTheDevboxReads', () => {
  assert.deepEqual(resultLine('http://localhost:5099/kilder', 'kilde-drilldown', 320, [{ rule: 'r', criteria: ['1.4.3'] }]),
    { page: 'explorer/kilde', state: 'kilde-drilldown', width: 320, violations: [{ rule: 'r', criteria: ['1.4.3'] }] });
  assert.deepEqual(scanFinishedLine, { scan: 'finished' });
});

// The completion lines are written in two other files; without them every devbox run reads as unknown, silently.
test('CompletionLines_WhenTheScanAndTheGateFinish_ThenBothAreWritten', () => {
  const scan = readFileSync(new URL('./axe-scan.mjs', import.meta.url), 'utf8');
  const gate = readFileSync(new URL('./check-accessibility.sh', import.meta.url), 'utf8');
  assert.match(scan, /appendFileSync\(resultsFile, `\$\{JSON\.stringify\(scanFinishedLine\)\}\\n`\)/);
  assert.match(gate, /printf '\{"gate":"finished","axeRuns":%s\}\\n' "\$axe_runs" >> "\$AXE_RESULTS_FILE"/);
  // Only when both passes reached a verdict, and counting the picker-off pass where it runs.
  assert.match(gate, /\[ "\$scan_status" -le 1 \] && \[ "\$off_status" -le 1 \]; then\n  printf '\{"gate"/);
  assert.match(gate, /axe_runs=1\nif \[ -z "\$\{SCAN_SHARD:-\}" \] \|\| \[ "\$\{SCAN_SHARD%%\/\*\}" = 1 \]; then\n  axe_runs=2/);
});
