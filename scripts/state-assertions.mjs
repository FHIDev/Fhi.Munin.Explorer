// What the component's STATE is measured against: after a press it refuses, does the browser's own
// control still say the same thing the component drew? And two neighbouring questions that are here
// for the same reason — an href is a string in a render tree until a browser RESOLVES it, and what
// it resolves to depends on a <base> element bUnit has not got (Fhi.Metadata-l9l2n.114); and the
// sticky fact bar is drawn hidden and shown by an IntersectionObserver, so whether it appears at the
// right moment is a question about a viewport and a scroll position (Fhi.Metadata-35w0p.28).
//
// WHY THIS EXISTS. A browser flips a checkbox itself, before any handler runs, and a Blazor render
// that equals the render before it writes nothing back to the DOM. So a press the component
// declines leaves a visibly ticked box over a filter that is off, or over a column that is still on
// screen. `builder.SetUpdatesAttributeName("checked")` is the one line that unsticks it — it patches
// the previous render tree to match what the client did, so the next diff has an edit to write —
// and deleting it left the whole suite green: bUnit renders a render tree, the browser's flip never
// happens there, and the disagreement is invisible to every test in test/. (Fhi.Metadata-1s7z1)
//
// WHAT IT DOES NOT SEE, so nobody reads a green run as more than it is:
//   - whether a contents-nav press moves FOCUS as well as the viewport. ModernHost is a Blazor Web
//     App with an interactive router, and Blazor intercepts a same-page-with-hash press and scrolls
//     itself rather than leaving the browser to make the fragment jump that carries focus. What is
//     asserted below is therefore the half the component owns — that every target is focusable —
//     and a host that does not intercept is not measured anywhere;
//   - every OTHER control the component draws. tree-assertions.mjs adds the deterministic tree
//     cases; the six presses defined in this file are all in the variable
//     explorer. TWO the component refuses: the picker's refusal to hide the last column, and a
//     facet press dropped because a fetch was already in flight. FOUR it accepts, and each is
//     here because the browser holds state the render tree has not got — the facet tree's two
//     branch disclosures, that a shut branch leaves nothing behind for a Tab to land on and that
//     folding one over a ticked value leaves the value ticked, the toolbar's Ikoner switch,
//     where what is asked is what the redraw left alone, and a dataperiode field retyped as the
//     day it holds, where the browser holds the reader's spelling;
//   - the kildeutforsker, which hangs the same shared ColumnPicker over its own table and is not
//     visited at all;
//   - the facet panel's OTHER refusal, the rollback when a fetch fails. Measured while writing this
//     and deliberately left out: FetchRowsAsync renders once with the new filter before it asks, so
//     the render after the rollback differs from the render before it and the DOM is corrected
//     whether or not the call is there. An assertion on that path would hold either way, which is
//     no assertion at all — and it is why the comment beside that call names only the path the
//     call is load-bearing on;
//   - whether a TICKED control looks ticked. This reads `input.checked` and `aria-pressed`, which
//     is what a screen reader is told; a stylesheet drawing a mark of its own over the top is the
//     layout gate's business. The row chevron is the exception, and why is on its assertion: its
//     picture is the only thing that tells a sighted reader shut from open, and which picture it
//     is depends on `aria-expanded` reaching the cascade (Fhi.Metadata-l9l2n.84);
//   - the sticky bar PAINTING for a frame. The bar assertion below reads the state the observer
//     settled on, which is what the predicate decides; a load that painted the bar once and
//     corrected itself on the next frame would still read as clean here. That is paint timing, and
//     nothing headless in this repository sees it;
//   - the sticky bar under TWO mounted explorers, and under a circuit dropped mid-scroll. Both are
//     asserted in bUnit instead — the ids the module is handed, and the disconnect on disposal —
//     because neither sample host mounts two detail pages on one page;
//   - the sticky bar's hashchange path AS A HOST MAKES IT. The jumps below are measured now, and so
//     is the listener (Fhi.Metadata-14j7i) — but ModernHost's router takes the anchor press over and
//     moves the page with history.pushState, so the press assertion is carried by `scrollend` and
//     the hashchange one dispatches the event itself. No sample here leaves that press to the browser;
//   - the contents-nav mark after a PRESS. Under Stiler the bar's reveal lands a jump 76px short
//     (Fhi.Metadata-79t6z), so the mark is asserted at scroll positions and at each jump line.
//
// HOW IT DIFFERS FROM geometry-assertions.mjs. Those bodies run inside the page, because measuring
// a box is an expression. These do not: a state assertion has to PRESS something first, and a press
// is a browser action. So a body here runs in Node and is handed the Playwright page, and the
// helpers below are ordinary module functions rather than being inlined into every body.
//
// Each assertion carries a `control` as well: it breaks the DOM the way the missing call would —
// leaving the browser's flip standing — and state-scan.mjs requires `measure` to report it. An
// assertion that has quietly stopped measuring anything passes forever otherwise, which is the
// failure this whole file is about.

// Deterministic tree coverage and its controls live together, independently of capture contents.
import { treeAssertions } from './tree-assertions.mjs';
import { jumpPast, jumpToTop, scrollPast, scrollToTop } from './reader-scroll.mjs';

/** The contents nav of a detail view, in the column beside it. */
const TOC = '.munin-explorer-page__toc';

/** The component's own root, for a message that says where it looked. */
const MOUNT = '.munin-explorer';

/** The picker is the one `<details>` in the header above the results. */
const PICKER = '.munin-explorer-header details';

/** One choice in the open picker — Stiler's name, borrowed, one per optional column. */
const ITEM = '.dropdown-choicepicker__item';

/** The facet sidebar. Its values are checkboxes; the dataperiode facet is fields and has none. */
const PANEL = '.munin-explorer-filters';

/** The control that opens one row of a facet tree — the rows that have values under them. */
const DISCLOSURE = '.munin-explorer-filters__disclosure';

/**
 * What a Tab could land on inside a branch row.
 *
 * Read off the DOM rather than by tabbing through it: a shut branch's claim is that its values are
 * not rendered at all, which is the stronger half of "not focusable" and the only one that survives
 * a host stylesheet beating `[hidden]`. The Tab below measures the other half on the live page.
 */
const FOCUSABLE = 'a[href], button, input, select, textarea, [tabindex]:not([tabindex="-1"])';

/** The first branch in the panel a reader can actually see, with its ancestors already open. */
const firstShutBranch = page => page.locator(`${PANEL} ${DISCLOSURE}[aria-expanded="false"]:visible`).first();

/** The row one disclosure belongs to. */
const rowOf = disclosure => disclosure.locator('xpath=..');

/**
 * One disclosure, found by the names it answers to rather than by where it sits.
 *
 * Not by index: a press that refetches rebuilds the whole tree, and Playwright reads a `nth` that
 * has moved as the LAST match rather than as an error — a silent mis-measure in a file whose own
 * header exists to keep a green run from meaning more than it is. The verb in the name is the one
 * for the NEXT press, so a branch answers to one name shut and another open, and both are kept.
 */
const branchNamed = (page, names) => names
  .map(name => page.locator(PANEL).getByRole('button', { name, exact: true }))
  .reduce((found, next) => found.or(next));

const branchName = button => button.evaluate(one => one.getAttribute('aria-labelledby')
  .split(/\s+/).map(id => document.getElementById(id).textContent.trim()).join(' '));

/**
 * Open the first shut branch the panel draws, and hand back the names it now answers to.
 *
 * Both branch assertions begin here, and both need it open: neither can ask anything of a branch
 * that has disclosed nothing, and the name it wears open is only readable once it is. Pressed from
 * the keyboard because that is also the claim — a <button> answers Enter with no handler of its own
 * — and because two clicks on one control inside the double-click interval are a double-click,
 * which every disclosure in this package refuses by design. (Fhi.Metadata-zel47)
 */
async function openFirstShutBranch(page) {
  const panel = page.locator(PANEL);
  await panel.waitFor({ state: 'visible', timeout: findTimeout });

  const shut = firstShutBranch(page);
  await shut.waitFor({ state: 'visible', timeout: findTimeout });

  const names = [await branchName(shut)];
  const button = await shut.elementHandle();

  // The pin has to name one control, or every re-locate below could land on a second branch the
  // catalogue happens to have given the same name.
  if (await branchNamed(page, names).count() !== 1) {
    throw new Error(`the panel draws more than one branch named "${names[0]}" — no press can be pinned to one`);
  }

  await shut.focus();

  if (!await shut.evaluate(one => one === document.activeElement)) {
    throw new Error(`the disclosure "${names[0]}" would not take focus`);
  }

  await page.keyboard.press('Enter');

  // Against the element itself for this one step: it is mid-rename, so neither name finds it.
  await page.waitForFunction(
    one => one.getAttribute('aria-expanded') === 'true', button, { timeout: findTimeout });

  names.push(await branchName(button));

  if (names[1] === names[0]) {
    throw new Error(`the disclosure "${names[0]}" kept its name when it opened, so it names the wrong press`);
  }

  return { panel, names, label: names[0], disclosure: branchNamed(page, names) };
}

/** Press a pinned disclosure, from the keyboard, and wait until it says it has moved. */
async function pressBranch(page, names, expanded) {
  await branchNamed(page, names).focus();
  await page.keyboard.press(' ');
  await branchNamed(page, names).and(page.locator(`[aria-expanded="${expanded}"]`))
    .waitFor({ state: 'attached', timeout: findTimeout });
}

/**
 * How long the rise of the refetch a tick provokes is waited for.
 *
 * Swallowed rather than required: against a local stub the panel can go busy and back inside one
 * round trip, so the rise is caught where it can be and the FALL is what is actually waited on. A
 * budget of its own because it is a ceiling on a wait that is allowed to time out.
 */
const BUSY_MS = 2000;

/**
 * Open the first shut branch and tick the first value it discloses, waiting out the refetch.
 *
 * Where both assertions that need a ticked value under a branch begin — folding one away, and
 * redrawing the rows without their icons — so the refetch handshake and the guard below live in
 * one place. Under the row's own list and never the row's own checkbox: what has to survive is a
 * value the BRANCH disclosed, and a branch row carries a checkbox of its own at every level but a
 * kildetype heading.
 */
async function tickFirstDisclosedValue(page) {
  const { panel, names, label, disclosure } = await openFirstShutBranch(page);

  const facet = disclosure.locator('xpath=ancestor::details[1]');
  const box = rowOf(disclosure).locator('ul input[type=checkbox]:not(:checked)').first();

  await box.waitFor({ state: 'visible', timeout: findTimeout });

  // A real pointer press, unlike the disclosures either side of it: the browser's own flip of this
  // box before any handler runs is the whole subject of this file.
  await box.click();

  // Both edges of the refetch, so what either assertion then does to the tree is done to the tree
  // the answer rebuilt.
  await panel.and(page.locator('[aria-busy="true"]'))
    .waitFor({ state: 'visible', timeout: BUSY_MS }).catch(() => {});
  await panel.and(page.locator('[aria-busy="false"]'))
    .waitFor({ state: 'visible', timeout: findTimeout });

  const chosen = await chosenInFacet(facet);

  if (chosen === 0) {
    throw new Error(`ticking a value under "${label}" chose nothing, so there is nothing to hold on to`);
  }

  return { panel, names, label, facet, chosen };
}

/**
 * How much of what a Tab could land on inside a row is the branch's disclosed content.
 *
 * Counted apart from the row's own controls rather than by subtracting them: whether a row carries
 * a checkbox of its own is the payload's answer, since the kildetype heading that has none is
 * lifted away when a catalogue has only one kildetype. Everything deeper sits in a row of its own.
 */
const disclosedControls = row => row.evaluate(
  (item, selector) => [...item.querySelectorAll(selector)]
    .filter(control => control.closest('li') !== item).length,
  FOCUSABLE);

/** The route a facet press refetches, and so the one a staged drop has to hold open. */
const SEARCH = '/api/explorer/variables';

/** How long that fetch is held. Long enough to press again inside it, short enough to wait out. */
const HOLD_MS = 6000;

// How long a press the component declines is given to have written nothing. There is no arrival to
// wait for — that is what makes it a refusal — so this is a round trip and nothing more principled.
// Generous on purpose: too short and a slow runner reads the broken case and the working one alike,
// since what would still be in flight is exactly the write that proves the component fine.
const REFUSAL_MS = Number(process.env.STATE_REFUSAL_MS ?? 3000);

// How long the contents-nav press is given to ARRIVE, which is what makes it a different budget
// from REFUSAL_MS above: there is an arrival to wait for here — the named section on screen, or the
// reader taken off the page — so this is a ceiling on a press that does neither, not a flat spend.
const JUMP_MS = Number(process.env.STATE_JUMP_MS ?? 5000);

/** The dataperiode from-field, one day inside the captured range as it writes it, and respelled. */
const DATE_FROM = 'Fra og med';
const DATE_WRITTEN = '01.01.2000';
const DATE_RESPELLED = '1.1.2000';

/** Playwright's default is generous; a control that is not there is not coming. */
const findTimeout = 15_000;

/**
 * Press the contents nav's first entry from the keyboard, and wait until the page has answered.
 *
 * From the keyboard because that is also the claim: a link answers Enter with no handler of its
 * own. Either answer ends the wait — the named section on screen, or the reader somewhere else
 * entirely — so a working jump costs what the scroll costs rather than a flat budget.
 *
 * The timeout is swallowed on purpose: a press that does neither is the finding `measure` is about
 * to report, and throwing here would be read as the harness failing instead of as the page.
 */
async function pressNavEntry(page, id, where) {
  await page.locator(`${TOC} a`).first().focus();
  await page.keyboard.press('Enter');

  await page.waitForFunction(({ target, path, query, href }) => {
    if (location.href === href) {
      return false;
    }

    if (location.pathname !== path || location.search !== query) {
      return true;
    }

    const box = document.getElementById(target)?.getBoundingClientRect();

    return box !== undefined && box.top >= 0 && box.top < innerHeight;
  }, { target: id, ...where }, { timeout: JUMP_MS }).catch(() => {});
}

/**
 * What the picker says about each column, whichever shape its control is.
 *
 * A checkbox carries its state in `checked` and a toggle button in `aria-pressed`. Both are read
 * because the shape is helsedata's to choose — this picker has already been each of them — and the
 * disagreement being measured is the same either way.
 */
const readPicker = page => page.locator(`${PICKER} ${ITEM}`).evaluateAll(items => items.map(item => {
  const box = item.querySelector('input[type=checkbox]');
  const button = item.querySelector('button[aria-pressed]');
  const control = box ?? button;

  return {
    label: (item.textContent ?? '').trim(),
    shape: box === null ? 'button' : 'checkbox',
    on: box === null ? button?.getAttribute('aria-pressed') === 'true' : box.checked,
    locked: control?.getAttribute('aria-disabled') === 'true',
    missing: control === null,
  };
}));

/** Which columns the result header actually draws, by the word above them. */
const drawnColumns = page => page.locator(`${MOUNT} [role=columnheader]`).evaluateAll(cells =>
  // The sort arrow is a span inside the active column's cell, so the text is the label plus " ↑".
  cells.map(cell => (cell.textContent ?? '').replace(/[↑↓]/g, '').trim()));

/**
 * Press one choice in the open picker.
 *
 * The label where there is one, because a checkbox this package draws may be hidden behind the
 * `<label>` that dresses it; the control itself where there is not.
 *
 * `force` is not optional on the locked choice, and the reason is the whole point of the assertion
 * below: the picker marks a refusing column `aria-disabled` rather than `disabled`, so a reader can
 * still reach it and press it, while Playwright's actionability check reads the ARIA state as
 * disabled and waits forever. Forcing skips that check and nothing else — the click is still a real
 * one at the element's own centre, which is what makes the browser flip the box.
 */
async function pressChoice(page, index, force = false) {
  const item = page.locator(`${PICKER} ${ITEM}`).nth(index);
  const label = item.locator('label');
  const target = await label.count() > 0 ? label.first() : item.locator('input, button').first();

  await target.waitFor({ state: 'visible', timeout: findTimeout });
  await target.click({ force });
}

/**
 * Wait until the result header stops drawing `label`.
 *
 * The header and not the control: a checkbox unticks in the browser before the circuit has been
 * told anything, so waiting on the control would wait for nothing and measure a press the server
 * has not processed.
 */
const waitForColumnGone = (page, label) => page.waitForFunction(
  ({ selector, wanted }) => ![...document.querySelectorAll(selector)]
    .some(cell => (cell.textContent ?? '').replace(/[↑↓]/g, '').trim() === wanted),
  { selector: `${MOUNT} [role=columnheader]`, wanted: label },
  { timeout: findTimeout });

/** How many of a facet's values the PANEL says are chosen — `Kilde (2)` on its own summary line. */
const chosenInFacet = facet => facet.locator(':scope > summary').evaluate(summary => {
  const match = /\((\d+)\)\s*$/.exec(summary.textContent ?? '');

  return match === null ? 0 : Number(match[1]);
});

/** How many of a facet's boxes the BROWSER has ticked, nested values included. */
const tickedInFacet = facet => facet.locator(':scope input[type=checkbox]:checked').count();

/** The row over the facets holding the two switches that redraw the tree without narrowing it. */
const TOOLBAR = '.munin-explorer-filters__toolbar';

/** The decorative glyph slot a facet row draws in front of its name, where it draws one. */
const ICONS = '.munin-explorer-filters__icons';

/** The Ikoner switch's own label, which is what tells it from Nivålinjer beside it. */
const ICONS_SWITCH = 'Ikoner';

/**
 * Press one of the toolbar's switches, from the keyboard, and wait until it says it has moved.
 *
 * By its accessible name and not by index: the row holds two switches drawn from one shape, and a
 * press that landed on the other would still leave every reading below looking sane. From the
 * keyboard because a <button role="switch"> answering Space with no handler of ours is half of
 * what makes the control a control.
 */
async function pressSwitch(page, name, checked) {
  const control = page.locator(TOOLBAR).getByRole('switch', { name, exact: true });

  await control.waitFor({ state: 'visible', timeout: findTimeout });
  await control.focus();
  await page.keyboard.press(' ');
  await control.and(page.locator(`[aria-checked="${checked}"]`))
    .waitFor({ state: 'attached', timeout: findTimeout });
}


/** The sticky fact bar a detail page pins to the top once its hero row has gone. */
const STUCKBAR = '.munin-explorer-page__stuckbar';

/** The hero fact row the bar watches. */
const HERO = '.munin-explorer-page__facts';

/** The class Fhi.Helsedata.Stiler draws a shown bar with, and the one the module toggles. */
const STUCKBAR_ON = 'munin-explorer-page__stuckbar--on';

/** Short enough that a detail page's hero row starts well below the fold on either sample page. */
const SHORT_VIEWPORT = { width: 1280, height: 300 };

// A contents-nav press and then the module's answer to it. Longer than reader-scroll.mjs's settle,
// which starts from a scroll that has already happened: here the router's own has to land first.
const PRESS_SETTLE_MS = 600;

/** The bar as the DOM has it, and as a screen reader would reach it. */
async function barState(page, barId) {
  const dom = await page.locator(`#${barId}`).evaluate((bar, on) => ({
    on: bar.classList.contains(on),
    hidden: bar.hasAttribute('hidden'),
    ariaHidden: bar.getAttribute('aria-hidden'),
  }), STUCKBAR_ON);

  // Read from the accessibility tree rather than from the attributes above, which is the whole
  // point: hidden it must reach nobody, and shown it must not be a second copy of the heading.
  let tree = '';
  try {
    tree = (await page.locator(`#${barId}`).ariaSnapshot()) ?? '';
  } catch {
    tree = '';
  }

  return { ...dom, tree: tree.replace(/\s+/g, ' ').trim() };
}

/** Where the hero row sits relative to the fold: below it, above it, or on screen. */
const heroAgainstFold = (page, rowId) => page.evaluate(id => {
  const box = document.getElementById(id).getBoundingClientRect();

  return { top: box.top, bottom: box.bottom, fold: window.innerHeight, scrollY: window.scrollY };
}, rowId);

/**
 * The short viewport, the ids, the bar's name, and the geometry every bar assertion below assumes:
 * a hero row starting BELOW the fold, and a page that scrolls clear of it at all. Checked here
 * rather than in a `measure`, which would report a fixture too short to scroll as a defect.
 */
async function stageStickyBar(page) {
  const bar = page.locator(STUCKBAR).first();
  const row = page.locator(HERO).first();

  await bar.waitFor({ state: 'attached', timeout: findTimeout });
  await row.waitFor({ state: 'attached', timeout: findTimeout });

  const barId = await bar.getAttribute('id');
  const rowId = await row.getAttribute('id');

  if (!barId || !rowId) {
    throw new Error(`the bar (${barId}) and its hero row (${rowId}) must both carry an id: ` +
      'the module watches one and drives the other by id, and two mounts share neither');
  }

  // The name alone, without the code beside it, so the accessibility-tree read below is looking
  // for the words the page's own heading carries.
  const name = (await bar.locator('.munin-explorer-page__stuckbar-name > span').first().textContent())
    ?.replace(/\s+/g, ' ').trim();

  if (!name) {
    throw new Error('the bar names nothing, so a second copy of the heading could not be told from none');
  }

  await page.setViewportSize(SHORT_VIEWPORT);
  await scrollToTop(page);

  const start = await heroAgainstFold(page, rowId);

  if (start.top <= start.fold) {
    throw new Error(`the hero row starts ${start.top}px down a ${start.fold}px viewport, so it is ` +
      'already on screen: this assertion would be measuring the wrong half of the predicate');
  }

  await scrollPast(page, rowId);

  const past = await heroAgainstFold(page, rowId);

  if (past.bottom >= 0) {
    throw new Error('the page will not scroll past its own hero row, so the bar can never be shown');
  }

  await scrollToTop(page);

  return { barId, rowId, name };
}

/**
 * Drives the bar from an IntersectionObserver alone: `whole` is the module before
 * Fhi.Metadata-14j7i, `upwards-half-dropped` the predicate without its `top < 0` half. BOTH nodes
 * are replaced by copies, which takes the real module out — its references leave the page with them.
 */
async function standInModule(page, { barId, rowId }, predicate) {
  await page.evaluate(({ barName, rowName, on, rule }) => {
    const row = document.getElementById(rowName);
    const freshRow = row.cloneNode(true);

    row.replaceWith(freshRow);

    const bar = document.getElementById(barName);
    const freshBar = bar.cloneNode(true);

    bar.replaceWith(freshBar);

    new IntersectionObserver(([entry]) => {
      const shown = rule === 'whole'
        ? !entry.isIntersecting && entry.boundingClientRect.top < 0
        : !entry.isIntersecting;

      freshBar.classList.toggle(on, shown);
      freshBar.hidden = !shown;
      freshBar.setAttribute('aria-hidden', String(!shown));
    }).observe(freshRow);
  }, { barName: barId, rowName: rowId, on: STUCKBAR_ON, rule: predicate });
}

/** What a bar that should be showing has to say, or null. */
function notShowing(state, where) {
  return state.on && !state.hidden && state.ariaHidden === 'false'
    ? null
    : `${where} and the bar did not follow: on=${state.on}, hidden=${state.hidden}, ` +
      `aria-hidden=${state.ariaHidden}`;
}

/** What a bar that should be away has to say, the accessibility tree included, or null. */
function stillShowing(state, name, where) {
  if (state.on || !state.hidden || state.ariaHidden !== 'true') {
    return `${where} and the bar stayed: on=${state.on}, hidden=${state.hidden}, ` +
      `aria-hidden=${state.ariaHidden}`;
  }

  // The bar repeats the page's own title, so one left in the tree is a second copy of a heading
  // the reader already has on screen — the WCAG half of Fhi.Metadata-14j7i.
  return state.tree.includes(name)
    ? `${where}, the bar is hidden and a screen reader still reaches it as ${state.tree}: ` +
      `"${name}" is announced twice over a page already showing its own title`
    : null;
}

/**
 * The row disclosures whose GLYPH is their state, one per surface. The picture is a cascade (base
 * icon rules at rest, hover rules over them; Fhi.Metadata-trfs0) that no test in test/ resolves.
 */
const CHEVRONS = [
  {
    surface: "Kelda's kilde row",
    state: 'kilder-list',
    toggle: '.munin-explorer-kilder__expand-toggle',
    icon: '.munin-explorer-kilder__expand-icon',
  },
  {
    surface: "Runa's result row",
    state: 'variables-list',
    toggle: 'ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name',
    icon: '.munin-explorer-dataitem-main__expand-icon',
  },
];

/** The four states and the file each one owes, which is the whole of what a reader can see. */
const CHEVRON_GLYPHS = [
  ['shut, pointer away', 'icon_down.svg'],
  ['shut, row hovered', 'icon_down--blue.svg'],
  ['open, pointer away', 'icon_up.svg'],
  ['open, row hovered', 'icon_up--blue.svg'],
];

/** Somewhere no row is, so a `:hover` rule is out of the cascade for the readings that want it. */
const POINTER_AWAY = { x: 0, y: 0 };

/**
 * Drive one chevron through its four states and hand back the picture drawn in each.
 *
 * The toggle is re-found for every reading rather than held: a press re-renders the row and the
 * span carrying the glyph class is replaced under it. Hovering the toggle hovers its row as well,
 * which is what the `tr:hover` and `__item__row:hover` rules key on.
 */
async function chevronPictures(page, { toggle, icon }) {
  const button = () => page.locator(toggle).first();
  await button().waitFor({ state: 'visible', timeout: findTimeout });

  const settle = want => page.waitForFunction(
    ([one, state]) => document.querySelector(one)?.getAttribute('aria-expanded') === state,
    [toggle, want], { timeout: findTimeout });

  // `none` rather than a throw for either way a chevron draws nothing — no glyph span at all, or a
  // span with no picture, the defect the resting `icon-keyboard-arrow-up` rule was added for. Read
  // inside the button so a missing span is a reading rather than a locator timing out.
  const drawn = () => button().evaluate((one, within) => {
    const glyph = one.querySelector(within);
    return glyph === null ? 'none'
      : /[^\/"']+\.svg/.exec(getComputedStyle(glyph).backgroundImage)?.[0] ?? 'none';
  }, icon);

  if (await button().getAttribute('aria-expanded') !== 'false') {
    await button().click();
    await settle('false');
  }

  const pictures = [];
  await page.mouse.move(POINTER_AWAY.x, POINTER_AWAY.y);
  pictures.push(await drawn());
  await button().hover();
  pictures.push(await drawn());

  await button().click();
  await settle('true');
  await page.mouse.move(POINTER_AWAY.x, POINTER_AWAY.y);
  pictures.push(await drawn());
  await button().hover();
  pictures.push(await drawn());

  return pictures;
}

/** One assertion per surface: the same four states and the same four files, different names. */
const chevronAssertions = CHEVRONS.map(chevron => ({
  name: `${chevron.surface} draws the chevron of the state it is in, at rest and under the pointer`,
  kind: 'invariant',
  states: [chevron.state],

  // Nothing to arrange: the row is the page's own first one, and the assertion presses it itself.
  async stage(page) {
    await page.locator(chevron.toggle).first().waitFor({ state: 'visible', timeout: findTimeout });
    return chevron;
  },

  async measure(page, staged) {
    const pictures = await chevronPictures(page, staged);

    const wrong = CHEVRON_GLYPHS
      .map(([state, owed], at) => ({ state, owed, drawn: pictures[at] }))
      .filter(one => one.drawn !== one.owed);

    return wrong.length === 0 ? null : wrong
      .map(one => `${one.state}: ${one.owed} owed, ${one.drawn} drawn`)
      .join('; ');
  },

  // Both halves, because breaking one proves only that one fires. The rest state is the base icon
  // rules' since Stiler 0.1.110 (Fhi.Metadata-trfs0), so they go with the explorer's hover rules;
  // matched on a pattern because the CSSOM normalises selectorText.
  async control(page) {
    await page.evaluate(() => {
      const base = /^\.icon-keyboard-arrow-(down|up)$/;
      const deleted = { shut: 0, open: 0 };

      for (const sheet of document.styleSheets) {
        let rules;
        try {
          rules = sheet.cssRules;
        } catch {
          continue;
        }

        for (let at = rules.length - 1; at >= 0; at -= 1) {
          const selector = (rules[at].selectorText ?? '').trim();
          if (!selector.includes('expand-icon') && !base.test(selector)) continue;

          // Runa's rules key the picture on the name button's aria-expanded (Fhi.Metadata-35w0p.78).
          const half = /icon-keyboard-arrow-down|aria-expanded="false"/.test(selector) ? 'shut'
            : /icon-keyboard-arrow-up|aria-expanded="true"/.test(selector) ? 'open' : null;

          if (half !== null) {
            sheet.deleteRule(at);
            deleted[half] += 1;
          }
        }
      }

      // Louder than a control that silently removes nothing, per half: that reads as an assertion
      // which has stopped measuring, and sends the next reader after a defect that is not there.
      for (const [half, count] of Object.entries(deleted)) {
        if (count === 0) throw new Error(`no ${half}-state chevron rule was reachable to delete`);
      }
    });
  },
}));

export const assertions = [
  {
    name: 'the compact collection action remains focused until focus leaves the returning hero',
    kind: 'invariant',
    states: ['variable-datasamling'],
    async stage(page) {
      await page.setViewportSize({ width: 1440, height: 700 });
      const root = page.locator('.munin-explorer-datasamling');
      const barId = await root.locator(STUCKBAR).getAttribute('id');
      const rowId = await root.locator(HERO).getAttribute('id');
      const action = root.locator(':scope > .munin-explorer-page__actions a').first();
      const href = await action.getAttribute('href');
      if (!href || !barId || !rowId) throw new Error('collection action and observed hero must exist');
      return { barId, rowId, href };
    },
    async measure(page, { barId, rowId, href }) {
      const bar = page.locator(`#${barId}`);
      const action = bar.locator('a');
      await scrollPast(page, rowId);
      await action.waitFor({ state: 'visible', timeout: findTimeout });
      if (await action.getAttribute('href') !== href) return 'compact action and header disagree on destination';
      // The module's own switch, not isVisible: since Stiler 79t6z the bar is 0px tall either way.
      // All three, because it writes all three: a bar left aria-hidden reaches no screen reader.
      const shown = () => bar.evaluate((b, on) => b.classList.contains(on) && !b.hidden &&
        b.getAttribute('aria-hidden') === 'false', STUCKBAR_ON);
      await action.focus();
      await scrollToTop(page);
      if (!await shown() || !await action.evaluate(e => e === document.activeElement)) {
        return 'returning hero hid the focused compact action';
      }
      await page.locator('.munin-explorer-datasamling > .munin-explorer-page__actions a').first().focus();
      await page.waitForTimeout(100);
      return await shown() ? 'bar stayed visible after focus left it and the hero returned' : null;
    },
    async control(page, { barId, rowId }) {
      await page.evaluate(({ barId, rowId }) => {
        // Detach the real observer's bar so callback ordering cannot undo this broken behavior.
        const original = document.getElementById(barId);
        const bar = original.cloneNode(true);
        original.replaceWith(bar);
        new IntersectionObserver(([entry]) => {
          const on = !entry.isIntersecting && entry.boundingClientRect.top < 0;
          bar.hidden = !on;
          bar.classList.toggle('munin-explorer-page__stuckbar--on', on);
          bar.setAttribute('aria-hidden', String(!on));
        }).observe(document.getElementById(rowId));
      }, { barId, rowId });
    },
  },
  ...treeAssertions,
  {
    name: 'nested criteria fragment clears a fixed host header without a contents entry',
    kind: 'invariant',
    states: ['variable-datasamling'],
    async stage(page) {
      await page.locator('#munin-explorer-criteria').waitFor({ state: 'visible', timeout: findTimeout });
      // Freeze the rendered markup and real stylesheet for a native browser jump. The sample's
      // interactive router replaces the document on this mirrored URL; its routing is a separate test.
      const markup = await page.evaluate(() => {
        const snapshot = document.documentElement.cloneNode(true);
        snapshot.querySelectorAll('script').forEach(script => script.remove());
        snapshot.querySelector('base').href = document.baseURI;
        return '<!doctype html>' + snapshot.outerHTML;
      });
      const fixtureUrl = new URL('/__criteria-fragment-fixture', page.url()).href;
      await page.route(fixtureUrl, route => route.fulfill({ contentType: 'text/html', body: markup }));
      await page.goto(fixtureUrl, { waitUntil: 'domcontentloaded' });
      await page.waitForFunction(() =>
        getComputedStyle(document.getElementById('munin-explorer-criteria')).scrollMarginTop === '140px');
      // ModernHost has no fixed chrome. Give the jump an obstruction and enough document tail
      // that reaching the end cannot accidentally keep an unstyled anchor below the header.
      await page.evaluate(() => {
        const header = document.createElement('div');
        header.id = 'fragment-test-header';
        Object.assign(header.style, { position: 'fixed', top: '0', left: '0', right: '0',
          height: '60px', zIndex: '99999', background: 'white' });
        document.body.append(header);
        const tail = document.createElement('div');
        tail.style.height = '150vh';
        document.body.append(tail);
      });
    },
    async measure(page) {
      for (const width of [1440, 320]) {
        await page.setViewportSize({ width, height: 900 });
        await page.evaluate(() => {
          history.replaceState(history.state, '', location.pathname + location.search);
          window.scrollTo({ top: 0, behavior: 'instant' });
          location.hash = 'munin-explorer-criteria';
        });
        try {
          await page.waitForFunction(() => {
            const target = document.getElementById('munin-explorer-criteria');
            const heading = target.firstElementChild.getBoundingClientRect();
            const header = document.getElementById('fragment-test-header').getBoundingClientRect();
            const margin = parseFloat(getComputedStyle(target).scrollMarginTop) || 0;
            return Math.abs(target.getBoundingClientRect().top - margin) <= 2 &&
              heading.top >= header.bottom && heading.bottom <= innerHeight && scrollY > 0;
          }, null, { timeout: 3000 });
        } catch {
          return `criteria heading did not clear the fixed header after a fragment jump at ${width}px`;
        }
        const independent = await page.locator('#munin-explorer-criteria').evaluate(target =>
          target.hasAttribute('data-nav-section') ||
          !!document.querySelector('.munin-explorer-page__toc a[href$="#munin-explorer-criteria"]'));
        if (independent) return 'criteria acquired an independent navigation target';
      }
      return null;
    },
    async control(page) {
      await page.locator('#munin-explorer-criteria').evaluate(target => target.classList.remove('munin-explorer-page__anchor'));
    },
  },
  {
    name: 'every contents-nav link resolves to this page rather than to the host base',
    // Nothing about one defect is encoded here: it asks what the BROWSER makes of each href and
    // requires this page's own address, whatever the attribute happens to say.
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed', 'variable-whole'],

    // The one thing no test in test/ can ask. The attribute reads "#metadata" in the broken build
    // and "/kilder?kilde=…#munin-explorer-metadata" in the fixed one, and bUnit can see both — but what broke on
    // helsedata is that a bare fragment resolves against the document's <base href="/">, which
    // their Optimizely layout sets, so every entry navigated to the site root and dropped the open
    // kilde. bUnit has no base element and no URL resolver; ModernHost's App.razor sets one.
    //
    // Both staged states are here because the two halves of the fix are reached differently.
    // /kilder is the host's own wrapper, which navigates, so the nav is built from the circuit's
    // address; /utforsker is VariableExplorer, which mirrors with history.replaceState and so is
    // the address NavigationManager cannot answer for and the cascade has to carry.
    async stage(page) {
      const nav = page.locator(TOC);
      await nav.waitFor({ state: 'visible', timeout: findTimeout });

      // Without one the resolved value equals the document's own address whatever the href says,
      // so a green run would be measuring nothing at all. Checked and not carried out of here:
      // nothing downstream reads it, and a staged field nothing consumes reads as a promise.
      const base = await page.evaluate(() => document.querySelector('base')?.href ?? null);

      if (base === null) {
        throw new Error('this host emits no <base href>, so it cannot tell the defect from the fix');
      }

      const entries = await nav.locator('a').evaluateAll(links => links.map(link => ({
        label: (link.textContent ?? '').trim(),
        id: link.getAttribute('href')?.split('#')[1] ?? '',
      })));

      if (entries.length === 0) {
        throw new Error(`${TOC} drew no links, so there is no href to resolve`);
      }

      // Named as the malformed link it is. Left to the reads below, an empty id would surface as
      // `#` naming no element — true, and a description of the wrong defect — and would then feed
      // the resolved comparison a URL nothing could ever match.
      const fragmentless = entries.filter(one => one.id === '');

      if (fragmentless.length > 0) {
        throw new Error(`${TOC} draws link(s) ${fragmentless.map(one => `"${one.label}"`).join(', ')} ` +
          'whose href carries no #fragment, so they name no section at all');
      }

      const query = await page.evaluate(() => location.search);

      if (query === '') {
        throw new Error(`${await page.evaluate(() => location.pathname)} carries no query, ` +
          'so a dropped one would not show');
      }

      return { entries };
    },

    // Every read is made here and not in `stage`, which is what makes the control below say
    // anything: it rewrites the hrefs after staging, so a read taken once before that could never
    // notice. Findings are collected, not returned, so the control fires all of them it breaks.
    async measure(page, { entries }) {
      const findings = [];

      const resolved = await page.locator(`${TOC} a`).evaluateAll(links => links.map(link => ({
        id: link.getAttribute('href')?.split('#')[1] ?? '',
        href: link.href,
        wanted: `${location.origin}${location.pathname}${location.search}#${link.getAttribute('href')?.split('#')[1] ?? ''}`,
      })));

      const astray = resolved.find(one => one.href !== one.wanted);

      if (astray !== undefined) {
        findings.push(`the link to #${astray.id} resolves to ${astray.href} where this page is ` +
          `${astray.wanted}: the href is being resolved against the <base>, not against the page`);
      }

      // The component's half of moving focus. Whether the browser takes it depends on the host —
      // see the header — but a target that is not focusable can never be given it.
      const unfocusable = await page.evaluate(ids => ids
        .filter(id => document.getElementById(id)?.getAttribute('tabindex') !== '-1'),
      resolved.map(one => one.id));

      if (unfocusable.length > 0) {
        findings.push(`section(s) ${unfocusable.join(', ')} carry no tabindex="-1", so a fragment ` +
          'jump to them scrolls the reader there and leaves their next Tab back in the nav');
      }

      // Last, because it is the one read that can take the page away: pressed under the control the
      // reader lands on the host base, and the hrefs and sections above are no longer there to read.
      const before = await page.evaluate(() =>
        ({ path: location.pathname, query: location.search, href: location.href }));

      await pressNavEntry(page, entries[0].id, before);

      const after = await page.evaluate(() => ({ path: location.pathname, query: location.search }));

      if (after.path !== before.path || after.query !== before.query) {
        findings.push(`pressing "${entries[0].label}" moved the reader from ${before.path}${before.query} ` +
          `to ${after.path}${after.query}: the jump left the page instead of scrolling within it`);
      } else {
        const jumped = await page.evaluate(id => {
          const box = document.getElementById(id)?.getBoundingClientRect();

          return box === undefined ? null : box.top >= 0 && box.top < innerHeight;
        }, entries[0].id);

        if (jumped !== true) {
          findings.push(jumped === null
            ? `"${entries[0].label}" names #${entries[0].id}, which is not in the document`
            : `pressing "${entries[0].label}" left #${entries[0].id} off screen`);
        }
      }

      // Indented to land under the scanner's own prefix, so two findings read as two lines rather
      // than as one sentence that lost its full stop.
      return findings.length === 0 ? null : findings.join('\n         ');
    },

    // The bead's own defect, put back by hand: a bare fragment. The attribute then reads exactly
    // what it read in the broken build, and every read in `measure` is made after this.
    async control(page) {
      await page.locator(`${TOC} a`).evaluateAll(links => links.forEach(link => {
        const id = link.getAttribute('href')?.split('#')[1];

        // Staging refused a fragmentless link, so reaching this is the harness having changed
        // under itself rather than a finding — and `#` alone would be a control of nothing.
        if (!id) {
          throw new Error('a contents-nav link has no #fragment to strip it back to');
        }

        link.setAttribute('href', `#${id}`);
      }));
    },
  },
  {
    name: 'a refused column toggle leaves the picker agreeing with the columns drawn',
    // Nothing about one defect is encoded here: it asks the picker and the header the same
    // question and requires one answer, whichever control the picker is built from.
    kind: 'invariant',
    states: ['variables-list'],

    // Turn every optional column off but one, then press the one that refuses. The picker will not
    // hide the last visible column — see VariableSearch.Columns.cs, ColumnLocked — so this press is
    // declined, the render after it equals the render before it, and the browser's own flip is all
    // that would be left saying otherwise.
    //
    // Measured against ColumnPicker.cs as it stands: with SetUpdatesAttributeName("checked") this
    // holds, with that line removed and the host rebuilt it reports the disagreement, and the facet
    // assertion below stays green through the removal.
    async stage(page) {
      const picker = page.locator(PICKER).first();
      await picker.waitFor({ state: 'visible', timeout: findTimeout });
      await picker.locator('summary').click();

      let choices = await readPicker(page);
      if (choices.length === 0) {
        throw new Error(`the picker at ${PICKER} drew no ${ITEM} to press`);
      }
      if (choices.some(choice => choice.missing)) {
        throw new Error(`a ${ITEM} holds neither a checkbox nor a button with aria-pressed`);
      }

      // Bounded, so a press that stops reducing the count is a message rather than a hang. One
      // iteration per optional column is already more than the seven can need.
      for (let guard = choices.length + 1; choices.filter(c => c.on).length > 1; guard--) {
        if (guard === 0) {
          throw new Error('pressing the picker never got down to one visible column');
        }

        const index = choices.findIndex(choice => choice.on && !choice.locked);
        if (index < 0) {
          throw new Error('more than one column is on and none of them can be turned off');
        }

        const { label } = choices[index];
        await pressChoice(page, index);
        await waitForColumnGone(page, label);
        choices = await readPicker(page);
      }

      const last = choices.findIndex(choice => choice.on);
      if (last < 0) {
        throw new Error('every optional column is off, so no press can be refused');
      }
      if (!choices[last].locked) {
        // The lock is what makes the press a refusal. Without it this would stage an ordinary
        // toggle and then measure the agreement that any toggle keeps.
        throw new Error(`the last visible column "${choices[last].label}" is not locked`);
      }

      await pressChoice(page, last, true);

      await page.waitForTimeout(REFUSAL_MS);

      return { label: choices[last].label, index: last };
    },

    async measure(page, { label, index }) {
      const choices = await readPicker(page);
      const choice = choices[index];
      if (choice === undefined || choice.label !== label) {
        return `the picker no longer lists "${label}" where it did — nothing was measured`;
      }

      const drawn = (await drawnColumns(page)).includes(label);

      // Reported rather than passed over: a press that went through is the picker breaking its own
      // rule, and a run that called that a pass would be measuring nothing.
      if (!drawn) {
        return `the picker hid "${label}", the last visible column, which it refuses to do`;
      }

      if (choice.on) {
        return null;
      }

      return `the ${choice.shape} for "${label}" says the column is off while the header still ` +
        'draws it: the refused press left the browser\'s own flip standing';
    },

    // The flip the missing call would leave behind, put there by hand. Nothing re-renders, so it
    // stands exactly as it would have.
    async control(page, { index }) {
      await page.locator(`${PICKER} ${ITEM}`).nth(index).evaluate(item => {
        const box = item.querySelector('input[type=checkbox]');
        if (box === null) {
          item.querySelector('button[aria-pressed]').setAttribute('aria-pressed', 'false');
          return;
        }

        box.checked = false;
      });
    },
  },

  {
    name: 'a facet press dropped mid-fetch leaves its checkbox agreeing with the filter in force',
    kind: 'invariant',
    states: ['variables-list'],

    // The one refusal the panel has that writes nothing at all. ApplyFilterAsync returns at
    // `if (_loading)` while a fetch is in flight, so the render the press provokes is equal to the
    // render before it — and an equal render writes nothing back to the DOM, leaving the browser's
    // own flip of the box as the only thing that moved.
    //
    // Deliberately NOT the rolled-back path the same method has, though the bead named both:
    // measured here, a failed fetch corrects the box on its own, because FetchRowsAsync renders
    // once with the new filter before it asks and the render after the rollback therefore differs.
    // An assertion on that path holds with the call and without it, which is no assertion at all.
    async stage(page, stub) {
      const panel = page.locator(PANEL);
      await panel.waitFor({ state: 'visible', timeout: findTimeout });

      // Every facet unfolded, because a `<details>` that is shut hides the box from the pointer.
      // By the word a reader presses it under, as axe-states.mjs finds its controls.
      const expand = panel.getByRole('button', { name: 'Utvid alle', exact: true }).first();
      await expand.waitFor({ state: 'visible', timeout: findTimeout });
      await expand.click();

      const facets = panel.locator('details');
      const count = await facets.count();

      for (let index = 0; index < count; index++) {
        const facet = facets.nth(index);
        const box = facet.locator(':scope input[type=checkbox]').first();
        if (await box.count() === 0 || await box.isChecked() || await chosenInFacet(facet) !== 0) {
          continue;
        }

        // The same box twice. A second value would work as well and risks more: narrowing renumbers
        // every other facet's counts, and a value that drops out of the list takes its keyed <li>
        // and the DOM state being measured with it.
        await stub.hold(SEARCH, HOLD_MS);

        // Armed here and spent by the page, so a throw before the fetch takes it would leave it
        // standing — and an unspent hold stops the NEXT run's pre-flight for a reason that belongs
        // to this one. Dropping one the fetch has already taken is a no-op.
        try {
          await box.click();

          // `aria-busy` on the panel is `_loading` itself — the very flag ApplyFilterAsync drops a
          // press on — so waiting for it is waiting for the precondition rather than guessing at it
          // with a sleep. Without it the second press is an ordinary toggle and this stages nothing.
          await panel.and(page.locator('[aria-busy="true"]'))
            .waitFor({ state: 'visible', timeout: findTimeout });

          await box.click();

          // And out the other side, which is the render after the held fetch lands. Measuring
          // before it would read the DOM while the press's own render was still in flight — and
          // that render is exactly what the working case writes and the broken one does not.
          await panel.and(page.locator('[aria-busy="false"]'))
            .waitFor({ state: 'visible', timeout: HOLD_MS + findTimeout });

          // Read back rather than assumed spent. A hold nothing ever asked for would leave a press
          // that was never dropped looking exactly like one that was, and every line below would
          // hold.
          if ((await stub.holding()).some(one => one.path === SEARCH)) {
            throw new Error(`the press did not refetch ${SEARCH}, so nothing was dropped`);
          }
        } finally {
          await stub.release();
        }

        return { index };
      }

      throw new Error(`no facet in ${PANEL} drew an unticked checkbox over an empty filter`);
    },

    async measure(page, { index }) {
      const facet = page.locator(`${PANEL} details`).nth(index);
      if (await facet.count() === 0) {
        return 'the facet the press was staged in is gone — nothing was measured';
      }

      const chosen = await chosenInFacet(facet);
      const ticked = await tickedInFacet(facet);

      // Reported for the reason the picker's refusal is: a second press that went through is the
      // panel failing to drop it, and a run that read that as a pass would have measured nothing.
      if (chosen !== 1) {
        return `the panel says ${chosen} value(s) are chosen where the dropped press should have ` +
          'left exactly the one the first press chose';
      }

      if (ticked === chosen) {
        return null;
      }

      return `${ticked} checkbox(es) are ticked in a facet the panel says holds ${chosen}: ` +
        "the dropped press left the browser's own flip standing";
    },

    // The flip the missing call would leave behind, put there by hand. Nothing re-renders, so it
    // stands exactly as it would have.
    async control(page, { index }) {
      await page.locator(`${PANEL} details`).nth(index)
        .locator(':scope input[type=checkbox]').first()
        .evaluate(box => { box.checked = !box.checked; });
    },
  },

  {
    name: 'a shut branch leaves nothing under it for a Tab to land on',
    kind: 'invariant',
    states: ['variables-list'],

    // The one thing about this control no test in test/ can ask, and the reason it is here rather
    // than only in bUnit: whether the values a branch discloses are really gone from the page the
    // reader is tabbing through, and whether the button answers Enter and Space at all. bUnit
    // renders a render tree — there is no tab order in one, and no key to press.
    async stage(page) {
      let branch = await openFirstShutBranch(page);
      // A grouping row has no checkbox. Descend until Tab can exercise the selectable-row case.
      while (await rowOf(branch.disclosure).locator(':scope > label input[type=checkbox]').count() === 0) {
        branch = await openFirstShutBranch(page);
      }
      const { names, label, disclosure } = branch;

      const disclosed = await disclosedControls(rowOf(disclosure));

      await pressBranch(page, names, 'false');

      return { names, label, disclosed };
    },

    async measure(page, { names, label, disclosed }) {
      const disclosure = branchNamed(page, names);
      const drawn = await disclosure.count();

      if (drawn !== 1) {
        return `the panel draws ${drawn} branches named "${label}" where it drew one — nothing was measured`;
      }

      // Reported rather than passed over: a branch that disclosed nothing is one this assertion
      // could not have failed on, whatever the collapse then did.
      if (disclosed < 1) {
        return `opening "${label}" put nothing a reader can reach on the page, so shutting it ` +
          'again proves nothing';
      }

      if (await disclosure.getAttribute('aria-expanded') !== 'false') {
        return `Space did not shut "${label}" — the control does not answer the keyboard`;
      }

      const row = rowOf(disclosure);
      const left = await disclosedControls(row);
      const lists = await row.locator('ul').count();

      if (left > 0 || lists > 0) {
        return `"${label}" is shut and its row still holds ${lists} list(s) and ${left} control(s): ` +
          'a shut branch renders its values rather than leaving them out, so a stylesheet is all ' +
          'that stands between them and the tab order';
      }

      // The row's own checkbox stays reachable; only disclosed descendants must leave the tab order.
      await disclosure.focus();
      await page.keyboard.press('Tab');

      const inside = await row.evaluate(
        item => item.contains(document.activeElement) && document.activeElement.closest('li') !== item);

      return inside
        ? `Tab off "${label}" landed inside a shut descendant row`
        : null;
    },

    // The defect this replays is the design it was chosen over: draw the values anyway and hide
    // them with `hidden`. That is one author rule away from being visible and focusable, which is
    // how two tab panels came to be drawn at once on 2026-09-03.
    async control(page, { names }) {
      await rowOf(branchNamed(page, names)).evaluate(row => {
        const list = document.createElement('ul');
        list.hidden = true;
        list.innerHTML = '<li><label><input type="checkbox"> Skjult verdi</label></li>';
        row.appendChild(list);
      });
    },
  },

  {
    name: 'folding a branch over a ticked value leaves the value ticked and the filter in force',
    kind: 'invariant',
    states: ['variables-list'],

    // Expansion is the panel's state and selection is the filter's, and the two must not be
    // coupled: a reader tidying the tree away would otherwise drop a filter they never released.
    // Measured in a browser because the round trip is what makes it worth asking — the value goes
    // out of the DOM and comes back from a render, not from a patch.
    async stage(page) {
      const { names, label, facet, chosen } = await tickFirstDisclosedValue(page);

      await pressBranch(page, names, 'false');

      // Read while it is shut: the chip row and the facet's own count are drawn off the filter
      // rather than off what is on screen, so both have to survive the value leaving the DOM.
      const whileShut = await chosenInFacet(facet);
      const chips = await page.locator('.munin-explorer-filters__chip').count();

      await pressBranch(page, names, 'true');

      return { names, label, chosen, whileShut, chips };
    },

    async measure(page, { names, label, chosen, whileShut, chips }) {
      const disclosure = branchNamed(page, names);

      // Given a moment to settle rather than read on the instant: the tick's refetch lands on its
      // own schedule, and a render arriving between the last press and this read would otherwise
      // be reported as a branch that had gone.
      await disclosure.and(page.locator('[aria-expanded="true"]'))
        .waitFor({ state: 'attached', timeout: findTimeout }).catch(() => {});

      const drawn = await disclosure.count();

      if (drawn !== 1) {
        return `the panel draws ${drawn} branches named "${label}" where it drew one — nothing was measured`;
      }

      if (await disclosure.getAttribute('aria-expanded') !== 'true') {
        return `"${label}" is shut again after being reopened — nothing was measured`;
      }

      if (whileShut !== chosen) {
        return `the facet said ${chosen} value(s) chosen with "${label}" open and ${whileShut} ` +
          'with it shut: folding a branch away changed the filter';
      }

      if (chips < 1) {
        return 'the ticked value drew no chip over the results while its branch was shut, so the ' +
          'only control left over that filter went with the branch';
      }

      const facet = disclosure.locator('xpath=ancestor::details[1]');
      const nowChosen = await chosenInFacet(facet);
      const ticked = await tickedInFacet(facet);

      if (nowChosen !== chosen) {
        return `reopening "${label}" left the facet saying ${nowChosen} value(s) chosen where it ` +
          `said ${chosen} before it was shut`;
      }

      return ticked === nowChosen
        ? null
        : `${ticked} checkbox(es) are ticked in a facet the panel says holds ${nowChosen}: the ` +
          'reopened branch drew its value at odds with the filter it is in';
    },

    // The flip the missing call would leave behind, put there by hand, exactly as the picker's and
    // the facet's own controls do it.
    async control(page, { names }) {
      await rowOf(branchNamed(page, names))
        .locator('ul input[type=checkbox]:checked').first()
        .evaluate(box => { box.checked = false; });
    },
  },

  {
    name: 'turning the node icons off redraws the rows and leaves what is ticked ticked',
    kind: 'invariant',
    states: ['variables-list'],

    // Why a browser rather than bUnit. The glyph slot sits INSIDE the <label> the checkbox is in,
    // so turning it off edits the subtree around a control whose state the browser holds and the
    // render tree does not: `input.checked` is the browser's, and a patch that replaced the input
    // instead of editing beside it would drop a tick the component still believes in. bUnit
    // re-reads its own render tree, where the tick is an attribute, and agrees with itself either
    // way. The press is also the one thing here that is not a refusal — it is a redraw the
    // component accepts — so what is asked is what the redraw left alone. (Fhi.Metadata-kd9ts)
    async stage(page) {
      const { panel, names, label, chosen } = await tickFirstDisclosedValue(page);

      const drawn = await panel.locator(ICONS).count();

      if (drawn === 0) {
        throw new Error('the panel drew no node icons at all, so turning them off proves nothing');
      }

      const lines = await panel.getAttribute('data-level-lines');

      await pressSwitch(page, ICONS_SWITCH, 'false');

      const whileOff = await panel.locator(ICONS).count();

      await pressSwitch(page, ICONS_SWITCH, 'true');

      return { names, label, chosen, drawn, whileOff, lines };
    },

    async measure(page, { names, label, chosen, drawn, whileOff, lines }) {
      const panel = page.locator(PANEL);
      const disclosure = branchNamed(page, names);
      const branches = await disclosure.count();

      if (branches !== 1) {
        return `the panel draws ${branches} branches named "${label}" where it drew one — nothing was measured`;
      }

      // Reported rather than passed over: a switch that redrew nothing is one this assertion could
      // not have failed on, whatever the rows then did.
      if (whileOff !== 0) {
        return `${whileOff} icon slot(s) were still drawn with the switch off, so the press redrew nothing`;
      }

      const back = await panel.locator(ICONS).count();

      if (back !== drawn) {
        return `${back} icon slot(s) came back where ${drawn} went away, so the switch is not its own undo`;
      }

      if (await panel.getAttribute('data-level-lines') !== lines) {
        return 'the level lines moved with the node icons: one switch in the toolbar drove the other';
      }

      const facet = disclosure.locator('xpath=ancestor::details[1]');
      const nowChosen = await chosenInFacet(facet);
      const ticked = await tickedInFacet(facet);

      if (nowChosen !== chosen) {
        return `the facet says ${nowChosen} value(s) are chosen where it said ${chosen} before the ` +
          'icons were turned off and on: redrawing the rows changed the filter';
      }

      return ticked === nowChosen
        ? null
        : `${ticked} checkbox(es) are ticked in a facet the panel says holds ${nowChosen}: redrawing ` +
          "the rows without their icons left the browser's own tick at odds with the filter";
    },

    // The same hand-made flip the two above use — here it stands for the redraw having taken the
    // checkbox with the slot beside it and put back one the browser had never ticked.
    async control(page, { names }) {
      await rowOf(branchNamed(page, names))
        .locator('ul input[type=checkbox]:checked').first()
        .evaluate(box => { box.checked = false; });
    },
  },
  {
    name: 'a dataperiode field retyped as the day it already holds shows that day as the field writes it',
    kind: 'invariant',
    states: ['variables-list'],

    // The same equal-render trap over a text value. 1.1.2000 over an applied 01.01.2000 changes no
    // filter, so the render equals the one before it, and only SetUpdatesAttributeName("value")
    // gives the diff an edit to write. bUnit's value never held the reader's spelling at all.
    async stage(page) {
      const panel = page.locator(PANEL);
      await panel.waitFor({ state: 'visible', timeout: findTimeout });

      const expand = panel.getByRole('button', { name: 'Utvid alle', exact: true }).first();
      await expand.waitFor({ state: 'visible', timeout: findTimeout });
      await expand.click();

      const field = panel.getByLabel(DATE_FROM, { exact: true });
      await field.waitFor({ state: 'visible', timeout: findTimeout });

      // Applied first, both edges of the refetch waited out, so the second entry meets a filter
      // that already holds the day.
      await field.fill(DATE_WRITTEN);
      await field.press('Enter');
      await panel.and(page.locator('[aria-busy="true"]'))
        .waitFor({ state: 'visible', timeout: BUSY_MS }).catch(() => {});
      await panel.and(page.locator('[aria-busy="false"]'))
        .waitFor({ state: 'visible', timeout: findTimeout });

      if (await field.inputValue() !== DATE_WRITTEN) {
        throw new Error(`typing ${DATE_WRITTEN} into "${DATE_FROM}" did not leave ${DATE_WRITTEN} in it`);
      }

      await field.fill(DATE_RESPELLED);
      await field.press('Enter');

      // A ceiling on the write that proves the call is there, swallowed so the missing write is
      // measure's finding rather than the harness's.
      await field.evaluate((input, wanted) => new Promise(resolve => {
        const deadline = Date.now() + wanted.ms;
        const poll = () => (input.value === wanted.value || Date.now() > deadline
          ? resolve()
          : setTimeout(poll, 50));
        poll();
      }), { value: DATE_WRITTEN, ms: REFUSAL_MS });

      return {};
    },

    async measure(page) {
      const field = page.locator(PANEL).getByLabel(DATE_FROM, { exact: true });

      if (await field.count() !== 1) {
        return `the panel no longer draws one field labelled "${DATE_FROM}" — nothing was measured`;
      }

      if (await field.getAttribute('aria-invalid') === 'true') {
        return `the field refused ${DATE_RESPELLED}, a spelling of the day it holds`;
      }

      const shown = await field.inputValue();

      return shown === DATE_WRITTEN
        ? null
        : `the field shows "${shown}" over an applied ${DATE_WRITTEN}: the equal render left the ` +
          "reader's spelling standing";
    },

    // What the missing call leaves behind: the reader's spelling, which nothing re-renders over.
    async control(page) {
      await page.locator(PANEL).getByLabel(DATE_FROM, { exact: true })
        .evaluate((input, typed) => { input.value = typed; }, DATE_RESPELLED);
    },
  },
  {
    name: 'the sticky fact bar stays away until the hero row has left the viewport upwards',
    // The predicate whole, and both halves of it: `!isIntersecting` alone is also true of a hero
    // row still BELOW the fold, which is where every page load starts on a short viewport.
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed', 'variable-whole'],

    // The one thing no test in test/ can ask. bUnit renders a render tree and never runs the
    // module, so the bar's hidden state is all bUnit can see; whether the OBSERVER agrees with it
    // needs a viewport, a scroll position and an IntersectionObserver, which only a browser has.
    stage: stageStickyBar,

    // Both halves, in the order a reader meets them: the load, then the scroll. Re-established here
    // rather than in `stage` so the control below has something to break — and so the second half
    // of the first read is also "scrolling back up puts it away again".
    async measure(page, { barId, rowId, name }) {
      await scrollToTop(page);

      const before = await barState(page, barId);

      if (before.on || !before.hidden || before.ariaHidden !== 'true') {
        const where = await heroAgainstFold(page, rowId);

        return `the bar is showing before the reader has scrolled at all — hero row ${where.top}px ` +
          `down a ${where.fold}px viewport, scrollY ${where.scrollY} — which is the flash that ` +
          'dropping the `boundingClientRect.top < 0` half of the predicate causes on every load';
      }

      if (before.tree.includes(name)) {
        return `a hidden bar is still in the accessibility tree as ${before.tree}: a screen reader ` +
          `meets "${name}" twice on a page that shows the bar to nobody`;
      }

      await scrollPast(page, rowId);

      const after = await barState(page, barId);

      if (!after.on || after.hidden || after.ariaHidden !== 'false') {
        return `the hero row has left the viewport upwards and the bar did not follow: ` +
          `on=${after.on}, hidden=${after.hidden}, aria-hidden=${after.ariaHidden}`;
      }

      if (!after.tree.includes(name)) {
        return `the bar is on screen and the accessibility tree has none of it (${after.tree || 'empty'}): ` +
          'it is shown to sighted readers and hidden from everyone else';
      }

      // The half axe cannot answer: it reports violations, and a bar announced as a second copy of
      // the page's own title is not one. A heading node here is exactly that copy.
      return /\bheading\b/.test(after.tree)
        ? `the shown bar is a heading in the accessibility tree (${after.tree}): it repeats the ` +
          'page title, so a screen reader reads the outline as having two of them'
        : null;
    },

    // The bead's own defect, put back by hand: the `top < 0` half dropped, so `measure`'s first
    // read is the load it flashes on. The bar is swapped as well as the row, which is what takes
    // the real module off it — since Fhi.Metadata-14j7i a live one would correct this on scrollend.
    async control(page, staged) {
      await standInModule(page, staged, 'upwards-half-dropped');
    },
  },
  {
    name: 'one jump straight past the hero row still shows the sticky fact bar',
    // The scroll an IntersectionObserver cannot report: from above the row to below it in a single
    // frame, which is what the contents nav's own press and any window.scrollTo are. Deliberately
    // NOT reader-scroll's stepped scroll, which is the one shape the observer alone does see.
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed'],

    stage: stageStickyBar,

    async measure(page, { barId, rowId, name }) {
      await scrollToTop(page);

      const before = await barState(page, barId);

      if (before.on) {
        return 'the bar was already showing at the top, so a jump could not be what turned it on';
      }

      await jumpPast(page, rowId);

      const where = await heroAgainstFold(page, rowId);

      if (where.bottom >= 0) {
        throw new Error(`one jump left the hero row ${where.bottom}px into a ${where.fold}px ` +
          'viewport, so the bar is not owed: the jump did not clear the row');
      }

      const after = await barState(page, barId);
      const missed = notShowing(after, 'the hero row was jumped clear of in one step');

      if (missed !== null) {
        return missed;
      }

      return after.tree.includes(name)
        ? null
        : 'the bar is on screen after a jump and the accessibility tree has none of it ' +
          `(${after.tree || 'empty'}): it is shown to sighted readers and hidden from everyone else`;
    },

    // The module as it was: the whole predicate, but reported only on a crossing. A jump gives the
    // observer nothing to deliver, so the bar keeps the state it had at the top.
    async control(page, staged) {
      await standInModule(page, staged, 'whole');
    },
  },
  {
    name: 'one jump back to the top puts the sticky fact bar away again',
    // The direction that matters for WCAG: a stale bar here is a pinned duplicate of the page
    // title over a page already showing its own, and a screen reader meets the name twice.
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed'],

    stage: stageStickyBar,

    async measure(page, { barId, rowId, name }) {
      // Stepped, so the bar is SHOWING by the one route the observer alone can manage: what is
      // being measured here is the jump back, and staging it with a jump would measure both.
      await scrollToTop(page);
      await scrollPast(page, rowId);

      const shown = await barState(page, barId);
      const missed = notShowing(shown, 'the hero row was scrolled past');

      if (missed !== null) {
        return missed;
      }

      await jumpToTop(page);

      const where = await heroAgainstFold(page, rowId);

      if (where.bottom <= 0) {
        throw new Error(`one jump to 0 left the hero row ${where.bottom}px above the fold, ` +
          `scrollY ${where.scrollY}: the page never came back to where a load leaves it`);
      }

      return stillShowing(await barState(page, barId), name,
        'the reader jumped back to the top in one step');
    },

    async control(page, staged) {
      await standInModule(page, staged, 'whole');
    },
  },
  {
    name: 'a contents-nav press carries the sticky fact bar with it, both ways',
    // The reader's own version of the jump above, through the control that makes it: an in-page
    // anchor. Its two entries are chosen by measuring where each one lands rather than by name,
    // because which section clears the hero row is a fact about this page's lengths.
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed'],

    async stage(page) {
      const staged = await stageStickyBar(page);
      const links = page.locator(`${TOC} a[href*="#"]`);
      const count = await links.count();
      let past = -1;
      let short = -1;

      for (let at = 0; at < count && (past < 0 || short < 0); at += 1) {
        await scrollToTop(page);
        await links.nth(at).click();
        await page.waitForTimeout(PRESS_SETTLE_MS);

        const where = await heroAgainstFold(page, staged.rowId);

        if (where.bottom < 0 && past < 0) past = at;
        if (where.bottom > 0 && short < 0) short = at;
      }

      if (past < 0 || short < 0) {
        throw new Error(`of ${count} contents entries none lands ${past < 0 ? 'clear of' : 'short of'} ` +
          'the hero row, so one of the two presses this assertion needs cannot be made');
      }

      await scrollToTop(page);

      return { ...staged, past, short };
    },

    async measure(page, { barId, rowId, name, past, short }) {
      const links = page.locator(`${TOC} a[href*="#"]`);

      await scrollToTop(page);
      await links.nth(past).click();
      await page.waitForTimeout(PRESS_SETTLE_MS);

      const below = await barState(page, barId);
      const missed = notShowing(below, 'a contents-nav press jumped past the hero row');

      if (missed !== null) {
        return missed;
      }

      await links.nth(short).click();
      await page.waitForTimeout(PRESS_SETTLE_MS);

      const where = await heroAgainstFold(page, rowId);

      if (where.bottom <= 0) {
        throw new Error(`the entry staged as landing short of the hero row left it ${where.bottom}px ` +
          'above the fold: the page has moved under the assertion');
      }

      return stillShowing(await barState(page, barId), name,
        'a second press landed short of the hero row');
    },

    async control(page, staged) {
      await standInModule(page, staged, 'whole');
    },
  },
  {
    name: 'a hashchange alone brings the sticky fact bar back to what the hero row says',
    // The other half of the fix, and the half no sample host can reach: ModernHost's router takes
    // the anchor press over and moves the page with history.pushState, which fires no hashchange.
    // So the event is dispatched, over a bar put out of step by hand — a host's staleness, staged.
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed'],

    stage: stageStickyBar,

    async measure(page, { barId, rowId, name }) {
      await scrollToTop(page);
      await scrollPast(page, rowId);

      const missed = notShowing(await barState(page, barId), 'the hero row was scrolled past');

      if (missed !== null) {
        return missed;
      }

      // Put out of step without scrolling, so nothing but the hashchange can put it back: the
      // observer has no crossing to report and `scrollend` cannot fire over a page that is still.
      await page.evaluate(({ id, on }) => {
        const bar = document.getElementById(id);

        bar.classList.remove(on);
        bar.hidden = true;
        bar.setAttribute('aria-hidden', 'true');
      }, { id: barId, on: STUCKBAR_ON });

      await page.evaluate(() => window.dispatchEvent(
        new HashChangeEvent('hashchange', { oldURL: location.href, newURL: location.href })));
      await page.waitForTimeout(PRESS_SETTLE_MS);

      const after = await barState(page, barId);
      const ignored = notShowing(after, 'a hashchange arrived over a bar out of step with the row');

      return ignored ?? (after.tree.includes(name)
        ? null
        : 'the hashchange showed the bar and the accessibility tree has none of it ' +
          `(${after.tree || 'empty'}): it is shown to sighted readers and hidden from everyone else`);
    },

    // The module as it was, which listens for nothing: the hand-made staleness above is what a
    // host's jump leaves behind, and an observer with no crossing to report never clears it.
    async control(page, staged) {
      await standInModule(page, staged, 'whole');
    },
  },
  {
    name: 'exactly one contents-nav entry is current, and it follows the reader to the last',
    // Stiler keys the mark on the attribute's PRESENCE, so "false" on an entry draws it as current
    // just as "location" does (Fhi.Metadata-35w0p.15).
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed', 'variable-whole'],

    async stage(page) {
      await page.locator(`${TOC} a`).first().waitFor({ state: 'visible', timeout: findTimeout });

      const ids = await page.locator(`${TOC} a`).evaluateAll(links =>
        links.map(link => link.getAttribute('href')?.split('#')[1] ?? ''));

      if (ids.length < 2 || ids.includes('')) {
        throw new Error(`${TOC} drew ${ids.length} link(s), some without a #fragment, so there is ` +
          'no second entry for the mark to move to');
      }

      if (await page.evaluate(() => document.scrollingElement.scrollHeight <= innerHeight)) {
        throw new Error('the page does not scroll, so the mark has nowhere to follow the reader');
      }

      return { ids };
    },

    async measure(page, { ids }) {
      const marks = () => page.locator(`${TOC} a`).evaluateAll(links => links
        .filter(link => link.hasAttribute('aria-current'))
        .map(link => ({ id: link.getAttribute('href')?.split('#')[1] ?? '', value: link.getAttribute('aria-current') })));

      const wrong = (found, where, wanted) => {
        if (found.length !== 1 || found[0].value !== 'location') {
          return `${where}, the nav marks ${found.length === 0 ? 'no entry' : found.map(one => `#${one.id}="${one.value}"`).join(', ')}: ` +
            'exactly one link must carry aria-current, as "location", and no other link may carry it in any form';
        }

        return wanted !== undefined && found[0].id !== wanted
          ? `${where}, the nav marks #${found[0].id} where #${wanted} is the section the reader is in`
          : null;
      };

      const settle = () => page.evaluate(() => new Promise(done =>
        requestAnimationFrame(() => requestAnimationFrame(done))));

      await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
      await settle();

      const top = wrong(await marks(), 'at the top of the page', ids[0]);

      if (top !== null) {
        return top;
      }

      // Every reachable position a quarter viewport apart, the way a reader scrolls.
      for (;;) {
        const moved = await page.evaluate(() => {
          const before = window.scrollY;

          window.scrollBy({ top: Math.floor(innerHeight / 4), behavior: 'instant' });

          return window.scrollY !== before;
        });

        await settle();

        const finding = wrong(await marks(), `at scrollY ${await page.evaluate(() => Math.round(window.scrollY))}`);

        if (finding !== null) {
          return finding;
        }

        if (!moved) {
          break;
        }
      }

      const end = wrong(await marks(), 'scrolled to the end', ids[ids.length - 1]);

      if (end !== null) {
        return end;
      }

      // A section scrolled to its own jump line is the reader in that section, which is where a
      // fragment jump lands; the tail sections that cannot reach the line are the end case above.
      let reached = 0;

      for (const id of ids) {
        const landed = await page.evaluate(target => {
          const section = document.getElementById(target);
          const root = document.scrollingElement;

          section.scrollIntoView({ behavior: 'instant' });

          const padding = getComputedStyle(root).scrollPaddingTop;
          const line = ((padding.endsWith('%') ? parseFloat(padding) * root.clientHeight / 100 : parseFloat(padding)) || 0)
            + (parseFloat(getComputedStyle(section).scrollMarginTop) || 0);

          return Math.abs(section.getBoundingClientRect().top - line) <= 1;
        }, id);

        await settle();

        if (landed) {
          reached++;

          const finding = wrong(await marks(), `with #${id} scrolled to its jump line`, id);

          if (finding !== null) {
            return finding;
          }
        }
      }

      if (reached === 0) {
        throw new Error('no section could be scrolled to its jump line, so where the line sits was never measured');
      }

      return null;
    },

    // The commonest scroll-spy defect put back by hand: a spy that marks the current entry and
    // never unmarks the last one. The real spy is left holding a detached copy of the column.
    async control(page) {
      await page.evaluate(toc => {
        const column = document.querySelector(toc);
        const fresh = column.cloneNode(true);

        column.replaceWith(fresh);
        fresh.querySelectorAll('[aria-current]').forEach(link => link.removeAttribute('aria-current'));

        document.addEventListener('scroll', () => {
          let current = null;

          for (const link of fresh.querySelectorAll('a')) {
            const section = document.getElementById(link.getAttribute('href').split('#')[1]);

            if (section.getBoundingClientRect().top <= (parseFloat(getComputedStyle(section).scrollMarginTop) || 0) + 1) {
              current = link;
            }
          }

          (current ?? fresh.querySelector('a')).setAttribute('aria-current', 'location');
        }, { capture: true, passive: true });
      }, TOC);
    },
  },
  ...chevronAssertions,
];
