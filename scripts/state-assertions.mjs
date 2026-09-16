// What the component's STATE is measured against: after a press it refuses, does the browser's own
// control still say the same thing the component drew? And, since Fhi.Metadata-l9l2n.114, one
// neighbouring question that is here for the same reason — an href is a string in a render tree
// until a browser RESOLVES it, and what it resolves to depends on a <base> element bUnit has not
// got.
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
//     and a host that does not intercept is not measured anywhere. The facet search's own rescue IS
//     measured, and it is the one place here where document.activeElement is the whole question;
//   - every OTHER control the component draws. Eight presses are measured here, all in the variable
//     explorer. TWO the component refuses: the picker's refusal to hide the last column, and a
//     facet press dropped because a fetch was already in flight. FIVE it accepts, and each is here
//     because the browser holds state the render tree has not got — the facet tree's two branch
//     disclosures, that a shut branch leaves nothing behind for a Tab to land on and that folding
//     one over a ticked value leaves the value ticked; the toolbar's Ikoner switch, where what is
//     asked is what the redraw left alone; and two at the variabelgruppe level of the tree, where
//     what the browser flipped is one placement of a group and every other placement — and the
//     folds the reader arrived with — have to come back from a render (Fhi.Metadata-km3zb,
//     Fhi.Metadata-g51gg). ONE is neither a refusal nor a tick: the facet search committed by
//     moving focus away, whose whole subject is where focus is afterwards (Fhi.Metadata-6we8a);
//   - three shapes of that tree the captured fixture has not got, listed in check-component-state.sh
//     and covered in test/ instead: a group placed at a delkilde, a group placed at a kilde, and
//     the standalone Variabelgruppe facet populated at all (Fhi.Metadata-4wdnn);
//   - the kildeutforsker, which hangs the same shared ColumnPicker over its own table and is not
//     visited at all;
//   - the facet panel's OTHER refusal, the rollback when a fetch fails. Measured while writing this
//     and deliberately left out: FetchRowsAsync renders once with the new filter before it asks, so
//     the render after the rollback differs from the render before it and the DOM is corrected
//     whether or not the call is there. An assertion on that path would hold either way, which is
//     no assertion at all — and it is why the comment beside that call names only the path the
//     call is load-bearing on;
//   - whether the control LOOKS ticked. This reads `input.checked` and `aria-pressed`, which is
//     what a screen reader is told; a stylesheet drawing a mark of its own over the top is the
//     layout gate's business, not this one's.
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

import { NO_MATCH, VARIABELGRUPPE, groupRows, kildeFacet } from './axe-states.mjs';

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

/**
 * How many values the facet whose summary opens with `label` says are chosen, or null for no
 * such facet.
 *
 * Read off the summary line for the reason `chosenInFacet` is: that number and the chips over the
 * results are one projection, so it is what the panel says the FILTER holds rather than what is
 * ticked on screen. Wanted here for the standalone Variabelgruppe facet, which draws none of the
 * tree's rows and still has to count a group ticked in it. (Fhi.Metadata-km3zb)
 */
const chosenInFacetNamed = (page, label) => page.locator(`${PANEL} details > summary`)
  .evaluateAll((summaries, wanted) => {
    const found = summaries.find(one => (one.textContent ?? '').trim().startsWith(wanted));

    if (found === undefined) {
      return null;
    }

    const match = /\((\d+)\)\s*$/.exec(found.textContent ?? '');

    return match === null ? 0 : Number(match[1]);
  }, label);

/**
 * Which branches of the tree are open, by the id each discloses.
 *
 * The id and not the position: `BranchId` is built from the value key, which for a variabelgruppe
 * is where the row is DRAWN rather than the id ticking it, so two placements of one group have two
 * of these — and a refetch that rebuilt the tree keeps them all. A shut branch carries no
 * `aria-controls` at all, so what this returns is exactly the open set.
 */
const openBranchIds = page => page.locator(`${PANEL} ${DISCLOSURE}[aria-expanded="true"]`)
  .evaluateAll(buttons => buttons.map(one => one.getAttribute('aria-controls')));

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

/**
 * Tick the checkbox on one row of the panel and wait out the refetch it provokes.
 *
 * A real pointer press, because the browser's own flip of the box before any handler runs is the
 * whole subject of this file. Both edges of `aria-busy` are waited for — the rise where it can be
 * caught, since against a local stub the panel can go busy and back inside one round trip — so
 * what is read afterwards is the tree the answer rebuilt rather than the one the press left.
 */
async function tickRow(page, index) {
  const panel = page.locator(PANEL);

  await panel.locator('li').nth(index).locator(':scope > label input[type=checkbox]').click();

  await panel.and(page.locator('[aria-busy="true"]'))
    .waitFor({ state: 'visible', timeout: 2000 }).catch(() => {});
  await panel.and(page.locator('[aria-busy="false"]'))
    .waitFor({ state: 'visible', timeout: findTimeout });
}

export const assertions = [
  {
    name: 'every contents-nav link resolves to this page rather than to the host base',
    // Nothing about one defect is encoded here: it asks what the BROWSER makes of each href and
    // requires this page's own address, whatever the attribute happens to say.
    kind: 'invariant',
    states: ['kilde-hierarchy-collapsed', 'variable-whole'],

    // The one thing no test in test/ can ask. The attribute reads "#metadata" in the broken build
    // and "/kilder?kilde=…#metadata" in the fixed one, and bUnit can see both — but what broke on
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
    name: 'a variabelgruppe ticked at one placement is ticked at every other place the tree draws it',
    kind: 'invariant',
    states: ['filters-variabelgrupper'],

    // A group hangs under every datasamling its variables are in, so the tree draws it at more than
    // one place and every placement carries the one VariabelgruppeIds entry (Fhi.Metadata-km3zb).
    // The reader flips ONE of those boxes and the browser writes that one itself; every other
    // placement has to arrive from the render that follows, computed against a previous render tree
    // Blazor has patched at the pressed box alone.
    //
    // VariableSearchTest asks the same question of the render tree and this asks it of the page, so
    // read this as the round trip rather than as the only place it is asked: the answer is a real
    // one off the stub, the tree is rebuilt whole rather than re-rendered in hand, and the press is
    // a pointer press whose flip lands before any handler runs.
    async stage(page) {
      const placements = await groupRows(page, VARIABELGRUPPE);

      // The state asserted two and a checkbox on each, so reaching this is the harness having
      // changed under itself rather than a finding.
      if (placements.length < 2) {
        throw new Error(`the tree draws ${placements.length} row(s) for "${VARIABELGRUPPE}"`);
      }
      if (placements.some(row => row.checked)) {
        throw new Error(`"${VARIABELGRUPPE}" is ticked already, so no press of ours chose it`);
      }

      await tickRow(page, placements[0].index);

      return { pressed: placements[0].index, count: placements.length };
    },

    async measure(page, { pressed, count }) {
      const findings = [];
      const placements = await groupRows(page, VARIABELGRUPPE);

      if (placements.length !== count) {
        return `the tree draws ${placements.length} row(s) for "${VARIABELGRUPPE}" where it drew ` +
          `${count} — nothing was measured`;
      }

      // Reported rather than passed over, for the reason the picker's refusal is: a press that
      // chose nothing at all is a run that measured nothing.
      const chosen = await chosenInFacetNamed(page, 'Variabelgruppe');

      if (chosen !== 1) {
        return `the Variabelgruppe facet says ${chosen} group(s) are chosen where the one press ` +
          'should have chosen exactly one';
      }

      const untouched = placements.filter(row => row.index !== pressed && !row.checked);

      if (untouched.length > 0) {
        findings.push(`${untouched.length} of the ${count} rows the tree draws for ` +
          `"${VARIABELGRUPPE}" are unticked while the filter holds it: a placement the reader never ` +
          'pressed is drawn at odds with the one selection every placement shares');
      }

      // One chip and not one per placement. The chips are drawn off the filter, so two would mean
      // the selection itself had been keyed by where the row is rather than by the group.
      const chips = await page.locator('.munin-explorer-filters__chip').count();

      if (chips !== 1) {
        findings.push(`${chips} chip(s) stand over the results for one ticked group, where the one ` +
          'id it writes should draw exactly one');
      }

      return findings.length === 0 ? null : findings.join('\n         ');
    },

    // The flip that never arrived, put back by hand: the placement the reader did not press, left
    // as a render failing to write it would have left it.
    async control(page, { pressed }) {
      const placements = await groupRows(page, VARIABELGRUPPE);
      const other = placements.find(row => row.index !== pressed);

      if (other === undefined) {
        throw new Error(`"${VARIABELGRUPPE}" has no second placement to leave unwritten`);
      }

      await page.locator(`${PANEL} li`).nth(other.index)
        .locator(':scope > label input[type=checkbox]')
        .evaluate(box => { box.checked = false; });
    },
  },

  {
    name: 'a tick inside the tree leaves every branch the reader opened still open',
    kind: 'invariant',
    states: ['filters-variabelgrupper'],

    // The other half of the claim the assertion above and the fold assertion before it make between
    // them: expansion is the panel's state and selection is the filter's, and neither moves the
    // other. That one measures collapsing over a selection; this one measures selecting under an
    // expansion, which is the direction a refetch can break — a tick rebuilds the whole tree from a
    // fresh /filters answer, and _expandedBranches is deliberately not cleared when one arrives.
    //
    // It OVERLAPS a bUnit test on purpose, and the overlap is worth stating rather than dressing
    // up: Branches_WhenOneIsShutAfterExpandAll_ThenItStaysShutThroughTheNextAnswer asks the same
    // question of the render tree, and a clear added to _expandedBranches would redden both. What
    // this adds is the round trip — a real answer off the stub rebuilding the whole tree, with the
    // folds read back off the page as aria-controls ids that survived it — and the branches being
    // ones the FACET SEARCH opened rather than ones a press did. Read it as the same rule measured
    // one layer out, not as the only place the rule is measured.
    async stage(page) {
      const before = await openBranchIds(page);

      // The state opens the branches standing between the kilde and the match, so none is the
      // harness having changed under itself: there would be no group row to press either.
      if (before.length === 0) {
        throw new Error('no branch of the tree is open, so a tick cannot leave one open');
      }
      if (before.some(id => id === null)) {
        throw new Error('an open disclosure carries no aria-controls, so it names no list at all');
      }

      const placements = await groupRows(page, VARIABELGRUPPE);

      if (placements.length === 0) {
        throw new Error(`the tree drew no row for "${VARIABELGRUPPE}" to tick`);
      }

      await tickRow(page, placements[0].index);

      return { before };
    },

    async measure(page, { before }) {
      // Reported rather than passed over: a tick that chose nothing is a render this assertion
      // could not have failed on, whatever the refetch then did to the folds.
      const chosen = await chosenInFacetNamed(page, 'Variabelgruppe');

      if (chosen !== 1) {
        return `the Variabelgruppe facet says ${chosen} group(s) are chosen, so the tick that was ` +
          'to provoke the refetch never landed — nothing was measured';
      }

      const after = await openBranchIds(page);
      const shut = before.filter(id => !after.includes(id));

      if (shut.length === 0) {
        return null;
      }

      return `${shut.length} of the ${before.length} branches open before the tick are shut after ` +
        'it: the answer it provoked reset the folds, so a reader narrowing inside the tree loses ' +
        'the place they narrowed from';
    },

    // The fold the reader would have lost, taken away by hand: the button drawn shut and the list
    // it disclosed gone, exactly as a render off a cleared _expandedBranches would have left it.
    async control(page) {
      await page.locator(`${PANEL} ${DISCLOSURE}[aria-expanded="true"]`).first()
        .evaluate(button => {
          document.getElementById(button.getAttribute('aria-controls'))?.remove();
          button.setAttribute('aria-expanded', 'false');
          button.removeAttribute('aria-controls');
        });
    },
  },

  {
    name: 'a facet search that empties the facet puts focus back in its own box',
    kind: 'invariant',
    states: ['variables-list'],

    // Focus after a re-render, which is the one thing here no render tree holds: onchange fires
    // BECAUSE focus has left the box, so a commit that narrows rewrites the list the reader who
    // tabbed out is now standing in, and the component puts them back (Fhi.Metadata-6we8a). bUnit
    // can see that FocusAsync was called and not where document.activeElement ended up.
    //
    // Committed by moving focus to another control rather than with Enter, and that is what makes
    // the rescue load-bearing: Enter keeps focus in the box whether or not the component asks for
    // it, so an assertion staged that way would hold with the call gone and measure nothing.
    async stage(page) {
      const panel = page.locator(PANEL);
      await panel.waitFor({ state: 'visible', timeout: findTimeout });

      const facet = kildeFacet(page);
      const box = panel.locator('input.munin-explorer-filters__search').first();

      await facet.locator('li').first().waitFor({ state: 'visible', timeout: findTimeout });
      await box.waitFor({ state: 'visible', timeout: findTimeout });

      // Where focus is taken so that the commit fires at all. The results' own search field,
      // because it is a real control a reader leaves this box for and is on screen at this width
      // without scrolling.
      const elsewhere = page.locator('input.searchbox__freetext').first();
      await elsewhere.waitFor({ state: 'visible', timeout: findTimeout });

      const drawn = await facet.locator('li').count();

      await box.click();
      await box.fill(NO_MATCH);
      await elsewhere.focus();

      // The narrowed facet, waited for rather than slept through: the commit travels over the
      // circuit, and the rescue lands in the render that answers it.
      await facet.locator('li').first().waitFor({ state: 'detached', timeout: findTimeout });

      return { drawn };
    },

    async measure(page, { drawn }) {
      const findings = [];
      const facet = kildeFacet(page);

      // Reported rather than passed over: a facet that is gone, or that still lists its kilder, is
      // one this assertion could not have failed on.
      if (await facet.count() !== 1) {
        return 'a search matching nothing took the Kilde facet off the page, so the box the reader ' +
          'has to widen the term in went with it';
      }

      const left = await facet.locator('li').count();

      if (left !== 0) {
        return `the facet still lists ${left} of the ${drawn} rows it drew, so the term matched ` +
          'something after all — nothing was measured';
      }

      const box = page.locator(`${PANEL} input.munin-explorer-filters__search`);

      if (await box.count() !== 1) {
        findings.push('the facet kept its heading and lost its search box, so there is no way back ' +
          'from a term that matches nothing');
      } else {
        if (await box.inputValue() !== NO_MATCH) {
          findings.push(`the box reads "${await box.inputValue()}" where the reader left ` +
            `"${NO_MATCH}", so the term they have to widen is not the one on screen`);
        }

        if (!await box.evaluate(one => one === document.activeElement)) {
          const where = await page.evaluate(() => {
            const one = document.activeElement;

            return one === null || one === document.body ? 'nothing at all' : one.tagName.toLowerCase();
          });

          findings.push(`focus is on ${where} rather than in the facet's own box: the commit ` +
            'rewrote the list the reader was standing in and left them somewhere else');
        }
      }

      return findings.length === 0 ? null : findings.join('\n         ');
    },

    // Focus left where the commit would have left it without the rescue: on the control the reader
    // moved to, a screen away from the term they now have to widen.
    async control(page) {
      await page.locator('input.searchbox__freetext').first().focus();
    },
  },
];
