// node --test scripts/test-focus-if-still-on.heldout2.mjs: import is side-effect free, and a bare VoiceOver click counts as a press.
import assert from 'node:assert/strict';
import { test } from 'node:test';

class FakeElement {
  constructor(id, parent = null) { this.id = id; this.parent = parent; }
  focus() { document.activeElement = this; }
  contains(other) { for (let at = other; at; at = at.parent) { if (at === this) { return true; } } return false; }
}
globalThis.HTMLElement = FakeElement;
const windowListeners = [];
globalThis.window ??= {};
window.addEventListener = (type, fn) => windowListeners.push({ type, fn });
window.removeEventListener = () => {};

const body = new FakeElement('');
const elements = new Map();
const listeners = [];
globalThis.document = {
  body,
  activeElement: body,
  getElementById: id => elements.get(id) ?? null,
  addEventListener: (type, fn) => listeners.push({ type, fn }),
  removeEventListener() {},
};

const mod = await import(new URL('../src/Fhi.Munin.Explorer/wwwroot/explorer-interop.js', import.meta.url));
const recordedAfterImport = listeners.length + windowListeners.length;
const deliver = (type, target) => listeners.filter(l => l.type === type).forEach(l => l.fn({ target }));

test('Import_WhenTheModuleIsLoaded_ThenNoListenerIsAddedToDocumentOrWindow', () => {
  // The module header promises import is optional and side-effect free; press tracking must be opt-in.
  assert.equal(recordedAfterImport, 0);
});

test('FocusIfStillOn_WhenOnlyASyntheticClickOnTheShareButtonFollowedAnOlderPointerdown_ThenItMovesToTheCode', () => {
  // VoiceOver activates the button with a lone click; tracking must be switched on explicitly, and once is enough.
  assert.equal(typeof mod.watchPresses, 'function', 'watchPresses() must be exported');
  mod.watchPresses();
  mod.watchPresses();
  const perType = type => listeners.filter(l => l.type === type).length;
  assert.ok(perType('click') >= 1, 'a click listener is registered');
  assert.equal(perType('pointerdown'), 1);

  const share = new FakeElement('share');
  const code = new FakeElement('code');
  elements.set('share', share);
  elements.set('code', code);
  document.activeElement = body;
  deliver('pointerdown', new FakeElement('elsewhere'));
  deliver('click', share);
  assert.equal(mod.focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);
});
