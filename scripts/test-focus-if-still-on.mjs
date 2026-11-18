// node --test scripts/test-focus-if-still-on.mjs: the browser list's share moves focus to the code only while the reader
// is still on «Del liste» (Fhi.Metadata-emsc0.3.19). bUnit has no document.activeElement, so the decision is tested here.
import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';

class FakeElement {
  constructor(id) { this.id = id; }
  focus() { document.activeElement = this; }
}
globalThis.HTMLElement = FakeElement;
globalThis.window ??= { addEventListener() {}, removeEventListener() {} };

const body = new FakeElement('');
const elements = new Map();
globalThis.document = {
  body,
  activeElement: body,
  getElementById: id => elements.get(id) ?? null,
  addEventListener() {},
  removeEventListener() {},
};

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

test('FocusIfStillOn_WhenFocusIsStillOnTheShareButton_ThenItMovesToTheCode', () => {
  share.focus();
  assert.equal(focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);
});

test('FocusIfStillOn_WhenFocusIsOnNothing_ThenItMovesToTheCode', () => {
  assert.equal(focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);

  document.activeElement = null;
  assert.equal(focusIfStillOn('share', 'code'), true);
  assert.equal(document.activeElement, code);
});

test('FocusIfStillOn_WhenTheReaderMovedToAnyOtherControl_ThenFocusStaysThere', () => {
  // Any control, including ones no counter knew of: a column checkbox, a pager button, the table region.
  for (const id of ['column-checkbox', 'pager-next', 'table-region']) {
    const other = new FakeElement(id);
    elements.set(id, other);
    other.focus();
    assert.equal(focusIfStillOn('share', 'code'), false);
    assert.equal(document.activeElement, other);
  }
});

test('FocusIfStillOn_WhenTheCodeIsNotOnThePage_ThenNothingMoves', () => {
  share.focus();
  elements.delete('code');
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, share);
});

test('FocusIfStillOn_WhenTheShareButtonIsGoneAndFocusIsElsewhere_ThenNothingMoves', () => {
  const other = new FakeElement('other');
  other.focus();
  elements.delete('share');
  assert.equal(focusIfStillOn('share', 'code'), false);
  assert.equal(document.activeElement, other);
});
