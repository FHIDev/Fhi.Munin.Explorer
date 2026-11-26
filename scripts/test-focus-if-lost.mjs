// node --test scripts/test-focus-if-lost.mjs: after a browser list row's «Fjern», focus moves only when the reader's place
// left with the pressed button. Tab, a click on the host page or on another control keeps them where they went
// (Fhi.Metadata-emsc0.3.13).
import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';

class FakeElement {
  constructor(id) { this.id = id; this.isConnected = true; }
  focus() { if (!this.refusesFocus) { document.activeElement = this; } }
  contains(node) { return node === this; }
}
globalThis.HTMLElement = FakeElement;
globalThis.window ??= { addEventListener() {}, removeEventListener() {} };

const body = new FakeElement('');
const elements = new Map();
const listeners = new Map();
globalThis.document = {
  body,
  activeElement: body,
  getElementById: id => elements.get(id) ?? null,
  addEventListener(type, fn) { listeners.set(type, [...(listeners.get(type) ?? []), fn]); },
  removeEventListener(type, fn) { listeners.set(type, (listeners.get(type) ?? []).filter(f => f !== fn)); },
};
const deliver = (type, target) => (listeners.get(type) ?? []).slice().forEach(fn => fn({ type, target }));

const MODULE = new URL('../src/Fhi.Munin.Explorer/wwwroot/explorer-interop.js', import.meta.url);
const module = await import(MODULE);
const { focusIfLost } = module;
module.watchPresses();

let pressed;
let next;
beforeEach(() => {
  elements.clear();
  pressed = new FakeElement('remove-kjonn');
  next = new FakeElement('remove-vekt');
  elements.set('remove-vekt', next);
  document.activeElement = body;
});

// The pressed «Fjern» leaves with its row, which drops the browser's focus to the body.
function pressAndRemove(type = 'keydown') {
  deliver(type, pressed);
  pressed.isConnected = false;
  document.activeElement = body;
}

test('FocusIfLost_WhenThePressedButtonLeftAndBodyHoldsFocus_ThenFocusMovesToTheTarget', () => {
  pressAndRemove();
  assert.equal(focusIfLost('remove-vekt'), true);
  assert.equal(document.activeElement, next);
});

test('FocusIfLost_WhenTheButtonWasClickedAndLeft_ThenFocusMovesToTheTarget', () => {
  // Safari never focuses a clicked button: the press, not the focus, says where the reader was.
  pressAndRemove('pointerdown');
  assert.equal(focusIfLost('remove-vekt'), true);
});

test('FocusIfLost_WhenTheReaderTabbedToAnotherControl_ThenFocusStaysThere', () => {
  pressAndRemove();
  const alder = new FakeElement('remove-alder');
  document.activeElement = alder;
  assert.equal(focusIfLost('remove-vekt'), false);
  assert.equal(document.activeElement, alder);
});

test('FocusIfLost_WhenTheReaderClickedTheHostPageAndBodyHoldsFocus_ThenFocusIsNotMoved', () => {
  pressAndRemove();
  const hostText = new FakeElement('');
  deliver('pointerdown', hostText);
  assert.equal(focusIfLost('remove-vekt'), false);
  assert.equal(document.activeElement, body);
});

test('FocusIfLost_WhenAKeyLandsOnTheBodyAfterThePressedButtonLeft_ThenFocusStillMoves', () => {
  // A Shift or an arrow with nothing focused is not the reader going somewhere.
  pressAndRemove();
  deliver('keydown', body);
  assert.equal(focusIfLost('remove-vekt'), true);
});

test('FocusIfLost_WhenNothingHoldsFocusAtAll_ThenFocusMovesToTheTarget', () => {
  pressAndRemove();
  document.activeElement = null;
  assert.equal(focusIfLost('remove-vekt'), true);
  assert.equal(document.activeElement, next);
});

test('FocusIfLost_WhenTheTargetRefusesFocus_ThenItAnswersFalse', () => {
  pressAndRemove();
  next.refusesFocus = true;
  assert.equal(focusIfLost('remove-vekt'), false);
});

test('FocusIfLost_WhenThePressWatchNeverRan_ThenFocusOnNothingIsEnough', async () => {
  // A fresh copy of the module, whose watch the view could not start: nothing was ever pressed as far as it knows.
  const unwatched = await import(`${MODULE.href}?unwatched`);
  pressAndRemove();
  assert.equal(unwatched.focusIfLost('remove-vekt'), true);
  document.activeElement = new FakeElement('remove-alder');
  assert.equal(unwatched.focusIfLost('remove-vekt'), false);
});

test('FocusIfLost_WhenTheTargetIsNotOnThePage_ThenNothingIsFocused', () => {
  pressAndRemove();
  elements.delete('remove-vekt');
  assert.equal(focusIfLost('remove-vekt'), false);
  assert.equal(document.activeElement, body);
});
