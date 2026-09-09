// What the component's STATE is measured against: after a press it refuses, does the browser's own
// control still say the same thing the component drew?
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
//   - every OTHER control the component draws. Two presses are measured here, both in the variable
//     explorer: the column picker's refusal to hide the last column, and a facet press dropped
//     because a fetch was already in flight;
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

/** The component's own root, for a message that says where it looked. */
const MOUNT = '.munin-explorer';

/** The picker is the one `<details>` in the header above the results. */
const PICKER = '.munin-explorer-header details';

/** One choice in the open picker — Stiler's name, borrowed, one per optional column. */
const ITEM = '.dropdown-choicepicker__item';

/** The facet sidebar. Its values are checkboxes; the dataperiode facet is fields and has none. */
const PANEL = '.munin-explorer-filters';

/** The route a facet press refetches, and so the one a staged drop has to hold open. */
const SEARCH = '/api/explorer/variables';

/** How long that fetch is held. Long enough to press again inside it, short enough to wait out. */
const HOLD_MS = 6000;

// How long a press the component declines is given to have written nothing. There is no arrival to
// wait for — that is what makes it a refusal — so this is a round trip and nothing more principled.
// Generous on purpose: too short and a slow runner reads the broken case and the working one alike,
// since what would still be in flight is exactly the write that proves the component fine.
const REFUSAL_MS = Number(process.env.STATE_REFUSAL_MS ?? 3000);

/** Playwright's default is generous; a control that is not there is not coming. */
const findTimeout = 15_000;

/**
 * What the picker says about each column, whichever shape its control is.
 *
 * A checkbox carries its state in `checked` and a toggle button in `aria-pressed`, and this file
 * has to survive the change from one to the other: the shape is helsedata's to choose, and the
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

export const assertions = [
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
    // ON MAIN TODAY THIS CANNOT FAIL, and saying so is the point: ColumnPicker draws each choice as
    // a `<button aria-pressed>`, which carries no state of its own for a browser to flip. It was
    // measured against the checkbox picker of Fhi.Metadata-f6az7 instead — with
    // SetUpdatesAttributeName("checked") it holds, without it this line reports the disagreement —
    // so the guard is standing for the shape that needs it before that shape lands.
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
];
