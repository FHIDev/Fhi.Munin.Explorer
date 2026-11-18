// node --test scripts/test-focus-if-still-on.heldout.mjs: Safari and macOS Firefox never focus a clicked button, so body
// holds focus after a reader moved on. Only the pointerdown/keydown target shows it; focus alone cannot.
import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';

class FakeElement {
  constructor(id) { this.id = id; }
  focus() { document.activeElement = this; }
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

const { focusIfStillOn } = await import(new URL('../src/Fhi.Munin.Explorer/wwwroot/explorer-interop.js', import.meta.url));

let share;
let code;
beforeEach(() => {
  elements.clear();
  share = new FakeElement('share');
  code = new FakeElement('code');
  elements.set('share', share);
  elements.set('code', code);
  document.activeElement = body;
});

test('FocusIfStillOn_WhenAnotherControlWasClickedAndBodyHoldsFocus_ThenFocusIsNotMoved', () => {
  const checkbox = new FakeElement('column-checkbox');
  elements.set('column-checkbox', checkbox);
  deliver('pointerdown', share);
  deliver('pointerdown', checkbox);
  assert.equal(document.activeElement, body);
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, body);
});

test('FocusIfStillOn_WhenAnotherControlWasPressedByKeyAndBodyHoldsFocus_ThenFocusIsNotMoved', () => {
  const checkbox = new FakeElement('column-checkbox');
  elements.set('column-checkbox', checkbox);
  deliver('keydown', share);
  deliver('keydown', checkbox);
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, body);
});

test('FocusIfStillOn_WhenTheShareButtonWasPressedLastAndBodyHoldsFocus_ThenItMovesToTheCode', () => {
  deliver('pointerdown', share);
  assert.equal(focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);
});
