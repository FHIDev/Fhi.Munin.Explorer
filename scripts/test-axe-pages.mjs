// Keeps the state-to-page table whole: a state Varde cannot place would report on no page (DevInfra-og3.42).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { states } from './axe-states.mjs';
import { criteriaOf, pageOf, pageOfState } from './axe-pages.mjs';

test('AxePages_WhenAStateExists_ThenItNamesAPage', () => {
  assert.deepEqual(Object.keys(states).filter(state => !pageOfState.has(state)), []);
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
