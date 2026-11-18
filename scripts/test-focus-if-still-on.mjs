// node --test scripts/test-focus-if-still-on.mjs: the browser list's share moves focus to the code only while the reader
// is still on «Del liste» (Fhi.Metadata-emsc0.3.19). bUnit has no document.activeElement, so the decision is tested here.
import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';

class FakeElement {
  constructor(id, parent = null) { this.id = id; this.parent = parent; this.focusable = true; }
  focus() { if (this.focusable) { document.activeElement = this; } }
  contains(other) { for (let at = other; at; at = at.parent) { if (at === this) { return true; } } return false; }
}
globalThis.HTMLElement = FakeElement;
globalThis.window ??= { addEventListener() {}, removeEventListener() {} };

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

const { focusIfStillOn } = await import(new URL('../src/Fhi.Munin.Explorer/wwwroot/explorer-interop.js', import.meta.url));

// A press as the browser sends it: Safari and Firefox on macOS leave focus on body after a click.
const press = (type, target) => listeners.filter(l => l.type === type).forEach(l => l.fn({ target }));

let share;
let code;
const add = (id, parent = null) => { const one = new FakeElement(id, parent); elements.set(id, one); return one; };
beforeEach(() => {
  elements.clear();
  share = add('share');
  code = add('code');
  document.activeElement = body;
  press('pointerdown', share);
});

test('FocusIfStillOn_WhenFocusIsStillOnTheShareButton_ThenItMovesToTheCode', () => {
  share.focus();
  assert.equal(focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);
});

test('FocusIfStillOn_WhenFocusIsOnNothingAndTheShareWasPressedLast_ThenItMovesToTheCode', () => {
  assert.equal(focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);

  document.activeElement = null;
  press('pointerdown', new FakeElement('label', share));
  assert.equal(focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);
});

test('FocusIfStillOn_WhenTheReaderMovedToAnyOtherControl_ThenFocusStaysThere', () => {
  for (const id of ['column-checkbox', 'pager-next', 'table-region']) {
    const other = add(id);
    other.focus();
    assert.equal(focusIfStillOn('share', 'code'), false);
    assert.equal(document.activeElement, other);
  }
});

test('FocusIfStillOn_WhenAClickElsewhereLeftFocusOnNothing_ThenFocusIsNotTaken', () => {
  // Safari: the clicked checkbox or the page text never takes focus, so body is all activeElement says.
  press('pointerdown', add('column-checkbox'));
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, body);

  press('keydown', add('pager-next'));
  assert.equal(focusIfStillOn('share', 'code'), false);
});

test('FocusIfStillOn_WhenTheCodeIsNotOnThePageOrWillNotTakeFocus_ThenItSaysSo', () => {
  share.focus();
  code.focusable = false;
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, share);

  elements.delete('code');
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, share);
});

test('FocusIfStillOn_WhenTheShareButtonIsGoneAndFocusIsElsewhere_ThenNothingMoves', () => {
  const other = add('other');
  other.focus();
  elements.delete('share');
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, other);
});
