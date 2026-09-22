// The page states the accessibility scan visits beyond a plain page load: a name, and a function
// that drives a loaded page into it. Add one here and as a `path::state` target in
// check-accessibility.sh — or in check-hostile-host.sh, for a state that stages boxes rather than
// an accessibility tree, or in check-component-state.sh, for one that stages a press; all three
// read this same list. Why at all: AGENTS.md, "It scans states, not only pages".
//
// Controls are found by the name a reader presses them under, from `Texts.cs` in Norwegian since
// both samples mount with `Language="no"`. A control this file cannot find stops the scan as a
// TOOLING failure rather than leaving the page in a state nobody entered but axe reports under.
//
// States wait for content, never merely for the page. The data comes from axe-stub-api.mjs, so
// "no rows yet" means the component is broken rather than that a network call is slow.
//
// A state may also ASSERT, and the disclosure states here do: `kilde-hierarchy-*` and
// `kilde-facets` check what the press did rather than only staging it, because a browser is the
// only runner that has a native <details> toggle, the focus it leaves behind and a Blazor
// re-render landing on top of it. Such a failure reports as a state error rather than as a failing
// test, so the bUnit test that cannot stage the press names the state it defers to.

import { treeStates } from './tree-states.mjs';
import { scrollPast } from './reader-scroll.mjs';

/** Playwright's default action timeout is generous; a control that is not there is not coming. */
const findTimeout = 15_000;

/** The signal that data arrived, not merely that the shell rendered. */
function rowsArePresent(page, selector) {
  return page.locator(selector).first().waitFor({ state: 'visible', timeout: findTimeout });
}

async function press(scope, name) {
  const button = scope.getByRole('button', { name, exact: true }).first();
  await button.waitFor({ state: 'visible', timeout: findTimeout });
  await button.click();
}

export const states = {
  // The script guards inventory these literal names without launching a browser.
  'tree-collapsed': treeStates['tree-collapsed'],
  'tree-populated': treeStates['tree-populated'],
  'tree-empty-results': treeStates['tree-empty-results'],
  'tree-no-match': treeStates['tree-no-match'],
  'kilde-hierarchy-collapsed': async page => {
    const name = page.getByRole('button', { name: 'Tromsøundersøkelsen', exact: true });
    await name.waitFor({ state: 'visible', timeout: findTimeout });
    await name.click();
    await page.locator('.munin-explorer-hierarchy > ul > li').first()
      .waitFor({ state: 'visible', timeout: findTimeout });
    if (await page.locator('.munin-explorer-hierarchy details[open]').count()) {
      throw new Error('Hierarchy branches must start collapsed');
    }
  },
  'kilde-hierarchy-expanded': async page => {
    await states['kilde-hierarchy-collapsed'](page);
    const branch = page.locator('.munin-explorer-hierarchy > ul > li > details')
      .filter({ has: page.locator('ul > li > details') }).first();
    const summary = branch.locator(':scope > summary');
    await summary.focus();
    await page.keyboard.press('Enter');
    await branch.locator(':scope > ul').waitFor({ state: 'visible', timeout: findTimeout });
    await page.keyboard.press('Space');
    await branch.locator(':scope > ul').waitFor({ state: 'hidden', timeout: findTimeout });
    if (!await summary.evaluate(el => el === document.activeElement)) {
      throw new Error('Collapsing a branch moved focus away from its summary');
    }
    await page.keyboard.press('Enter');
    const child = branch.locator(':scope > ul > li > details').first();
    await child.locator(':scope > summary').waitFor({ state: 'visible', timeout: findTimeout });
    await child.locator(':scope > summary').focus();
    await page.keyboard.press('Space');
    await child.locator(':scope > ul > li').first().waitFor({ state: 'visible', timeout: findTimeout });
    if (await page.locator('.munin-explorer-hierarchy > ul > li > details[open]').count() !== 1) {
      throw new Error('Opening one branch changed a sibling disclosure');
    }
  },
  // The link that opens a datasamling, which only a host wiring DatasamlingHref draws — /kilder's
  // wrapper is that host. It asserts where the link is: inside a <summary> it would join the
  // summary's accessible name and toggle the node on the press that followed it.
  'kilde-hierarchy-open': async page => {
    await states['kilde-hierarchy-collapsed'](page);
    if (!await page.locator('.munin-explorer-hierarchy__open').count()) {
      // A delkilde's datasamlinger are not drawn until it is opened, and which kind the fixture
      // puts at the top is not this state's to depend on.
      for (const summary of await page.locator('.munin-explorer-hierarchy > ul > li > details > summary').all()) {
        await summary.click();
      }
    }
    await page.locator('.munin-explorer-hierarchy__open').first()
      .waitFor({ state: 'visible', timeout: findTimeout });
    if (await page.locator('.munin-explorer-hierarchy summary .munin-explorer-hierarchy__open').count()) {
      throw new Error('The datasamling link must not be a descendant of a <summary>');
    }
  },
  // And the page it opens, which is DatasamlingView — the same component Runa renders. Followed by
  // its own href rather than by a click, so the state does not turn on whether the host's router
  // intercepted the press.
  'kilde-datasamling': async page => {
    await states['kilde-hierarchy-open'](page);
    const href = await page.locator('.munin-explorer-hierarchy__open').first().getAttribute('href');
    if (!href) {
      throw new Error('The datasamling link must carry a real href');
    }
    await page.goto(new URL(href, page.url()).toString());
    await page.locator('.munin-explorer-datasamling').first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },
  'kilde-hierarchy-metadata': async page => {
    await states['kilde-hierarchy-collapsed'](page);
    const metadata = page.locator('.munin-explorer-hierarchy__metadata');
    await metadata.locator(':scope > summary').click();
    await metadata.locator('table').first().waitFor({ state: 'visible', timeout: findTimeout });
  },
  // The two list pages as they load. They wait for a row rather than for the page, because the
  // data can fail to arrive and an empty list is a page axe reports no violations in — which is
  // how this gate read green against an unreachable API for as long as it has existed.
  'variables-list': page => rowsArePresent(page, 'button.munin-explorer-dataitem-main__name'),
  'kilder-list': page => rowsArePresent(page, 'button.munin-explorer-kilder__name'),

  // The selection ribbon at its widest, which kilder-list above never reaches: ticking a row swaps
  // the handover's label for a longer one and puts the reset beside it. Measured at 320px, because
  // a handover that cannot wrap overflows there while the untouched page fits (Fhi.Metadata-kvgu7).
  'kilder-ticked': async page => {
    await rowsArePresent(page, 'button.munin-explorer-kilder__name');
    const tick = page.locator('.munin-explorer-kilder tbody input[type=checkbox]').first();
    await tick.waitFor({ state: 'visible', timeout: findTimeout });
    await tick.check();
    await page
      .locator('.munin-explorer-selection button.button-square--secondary')
      .waitFor({ state: 'visible', timeout: findTimeout });
  },

  // A long facet at rest, capped, and the control that lifts the cap pressed twice. The only state
  // here where the cap is on — every other opens its facets through Utvid alle, which lifts them —
  // and the only runner where the press lands on a DOM bUnit did not draw. (Fhi.Metadata-35w0p.31)
  'facet-cap': async page => {
    await rowsArePresent(page, 'button.munin-explorer-dataitem-main__name');

    // Pressed only where it is on screen, on kilde-facets' terms: above the sample's 1024px
    // breakpoint the toggle is display:none and the panel is unfolded already.
    const toggle = page.locator('.munin-explorer-filters__toggle');
    if (await toggle.isVisible()) {
      await toggle.click();
    }

    // The capped facet is found by the control, not by a heading: the fixture decides which facet
    // runs past ten and a name here would rot on re-capture. Matched collapsed, because an expanded
    // facet keeps the same control — the only way back — and would otherwise read as a capped one.
    const collapsed = page.locator('.munin-explorer-filters__facets > details')
      .filter({ has: page.locator(':scope > button[aria-expanded="false"]') })
      .first()
      .locator(':scope > button[aria-expanded="false"]');
    await collapsed.waitFor({ state: 'attached', timeout: findTimeout }).catch(() => {
      throw new Error('No facet on the page draws a capped value list');
    });

    // Re-anchored on the list the control names, because the presses below flip the attribute
    // matched on above: a locator holding it would stop resolving to this facet once the cap lifts.
    const controls = await collapsed.getAttribute('aria-controls');
    if (!controls) {
      throw new Error('The control names no value list at all');
    }

    const facet = page.locator('.munin-explorer-filters__facets > details')
      .filter({ has: page.locator(`:scope > button[aria-controls="${controls}"]`) });
    const control = facet.locator(':scope > button[aria-expanded]');

    const summary = facet.locator(':scope > summary');
    await summary.focus();
    await page.keyboard.press('Enter');
    await control.waitFor({ state: 'visible', timeout: findTimeout });

    const values = facet.locator(':scope > ul');

    // The control names the list it reveals, and names the one that is actually there: an
    // aria-controls pointing at an id nothing carries is a reference a screen reader drops in
    // silence.
    if (await values.getAttribute('id') !== controls) {
      throw new Error('The control names an id its facet\'s value list does not carry');
    }

    // Counted through the id rather than over every facet's rows: the others are shut and still
    // render their values, so a page-wide count never moves.
    const rows = () => values.locator(':scope > li').count();
    const rowsAre = (how, n) => page.waitForFunction(
      ([id, want, mode]) => {
        const drawn = document.getElementById(id)?.querySelectorAll(':scope > li').length;
        return mode === 'more' ? drawn > want : drawn === want;
      },
      [controls, n, how],
      { timeout: findTimeout });

    const capped = await rows();
    const offer = (await control.innerText()).replace(/\s+/g, ' ').trim();
    const promised = Number(offer.match(/\d+/)?.[0]);

    if (!Number.isInteger(promised) || promised < 1) {
      throw new Error(`The control offers no number to reveal: ${offer}`);
    }

    await control.click();
    await rowsAre('more', capped);

    // The count on the button was a promise about the list: what the press revealed has to be
    // exactly it, or the reader was told a number the panel does not have.
    const revealed = await rows() - capped;
    if (revealed !== promised) {
      throw new Error(`The control offered ${promised} more values and revealed ${revealed}`);
    }
    if (await control.getAttribute('aria-expanded') !== 'true') {
      throw new Error('The press revealed the rest and left aria-expanded false');
    }

    // And back, because the way out is the half a cap is judged on. The DOM and the component
    // have to agree twice, not once.
    await control.click();
    await rowsAre('same', capped);

    if (await control.getAttribute('aria-expanded') !== 'false') {
      throw new Error('The second press put the cap back and left aria-expanded true');
    }
    if ((await control.innerText()).replace(/\s+/g, ' ').trim() !== offer) {
      throw new Error('The control came back offering a different remainder than it began with');
    }

    // Left expanded for the scan: the revealed values are markup no other state puts on screen,
    // and axe reports no violations in what a cap is holding back.
    await control.click();
    await rowsAre('more', capped);
  },

  // The filter tree unfolded, with the guide lines drawn (Fhi.Metadata-wcbxi): axe skips what a
  // closed <details> hides, and since Fhi.Metadata-adog5 it would see nothing of the source tree
  // at all — a shut branch renders no values, so every level below the first is absent rather than
  // hidden. Utvid alle reaches both, which is why it is the one press here. Nivålinjer is
  // deliberately NOT pressed — the lines are on at first render since Fhi.Metadata-dfygj, so
  // pressing it would scan this state with them gone.
  'filters-level-lines': async page => {
    const panel = page.locator('.munin-explorer-filters');
    await panel.waitFor({ state: 'visible', timeout: findTimeout });

    await press(panel, 'Utvid alle');

    await page
      .locator('.munin-explorer-filters[data-level-lines="true"] ul ul')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });

    // And the branches themselves, so a press that unfolded the facets and stopped at the tree
    // leaves this state failing to arrive rather than scanning a tree that is not there.
    await page
      .locator('.munin-explorer-filters__disclosure[aria-expanded="true"]')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });

    // And a datasamling row drawing its datakategori glyphs, on the same terms: a payload whose
    // datasamlinger carry no categories renders the tree with none of Fhi.Metadata-evoil's markup
    // in it, and axe reports no violations in what is not there.
    await page
      .locator('.munin-explorer-filters__icons')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });

    // And a kilde row wearing its kildetype badge, for the same reason — the stub retypes one
    // kilde to biobank so there is such a row on screen at all. (Fhi.Metadata-aw203)
    await page
      .locator('.munin-explorer-filters__badge')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });

    // The Ikonforklaring legend, which Utvid alle opens with the facets: axe skips what a shut
    // <details> hides, so without the press its eighteen rows would be scanned in no state at all.
    // (Fhi.Metadata-zllxt)
    await page
      .locator('.munin-explorer-filters__legend-item')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },

  // The result count quotes the search term, and a searched code is one unbroken word. The stub
  // ignores the query and serves the same page, which is all this state needs: the count is drawn
  // from what was typed. (Fhi.Metadata-ofg1h)
  'explorer-search-code': async page => {
    const box = page.locator('input.searchbox__freetext').first();
    await box.waitFor({ state: 'visible', timeout: findTimeout });
    await box.fill('V_LMR.VARE_ADMINISTRASJONSVEI_BESKRIVELSE');
    await press(page, 'Søk');

    // On the count QUOTING it: that <p> is drawn from first paint, so waiting for the element
    // would pass on a search that never ran.
    await page.locator('.munin-explorer-results__toolbar .caption')
      .filter({ hasText: 'V_LMR.VARE_ADMINISTRASJONSVEI_BESKRIVELSE' })
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },

  // The same tree with the Ikoner switch pressed, which is the one state where a facet row names
  // itself without its datakategori words: the glyphs are aria-hidden and the words stand in for
  // them, so both leave together and what is left has to name the row on its own. The badge is a
  // fact rather than decoration and is asserted still there — a press that took it with the
  // pictures would be a name losing a word, which axe cannot see. (Fhi.Metadata-kd9ts)
  'filters-node-icons-off': async page => {
    await states['filters-level-lines'](page);

    const panel = page.locator('.munin-explorer-filters');
    const control = panel.getByRole('switch', { name: 'Ikoner', exact: true });

    await control.waitFor({ state: 'visible', timeout: findTimeout });
    await control.click();

    await panel.locator('.munin-explorer-filters__icons').first()
      .waitFor({ state: 'detached', timeout: findTimeout });

    if (await panel.locator('.munin-explorer-filters__badge').count() === 0) {
      throw new Error('Turning the node icons off took the kildetype badge with them');
    }

    // And the legend goes with the pictures it explains, rather than standing over a tree that
    // draws none. (Fhi.Metadata-zllxt)
    if (await panel.locator('.munin-explorer-filters__legend').count() !== 0) {
      throw new Error('The icon legend outlived the glyphs it names');
    }
  },

  // A variable row opened. The panel under the row is the largest block of markup in the package
  // that only exists after a click — every property, the statistics block and the owner buttons.
  // It fetches, so the wait is on the region reporting itself done rather than on it appearing.
  // The row's chevron opens it; the name opens the whole variable instead (Fhi.Metadata-35w0p.34).
  'variable-detail': async page => {
    const row = page.locator('button.munin-explorer-dataitem__expand-toggle').first();
    await row.waitFor({ state: 'visible', timeout: findTimeout });
    await row.click();

    await page
      .locator('.munin-explorer-detail[aria-busy="false"]')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },

  // The whole variable, which is a view and not the panel above it: `VariableView` with a contents
  // nav of its own. The one state in either sample drawing that nav under `VariableExplorer`, whose
  // query moves by history.replaceState and so is past NavigationManager (Fhi.Metadata-l9l2n.114).
  'variable-whole': async page => {
    await states['variable-detail'](page);
    await press(page, 'Vis hele variabelen');
    await page.locator('.munin-explorer-whole').first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },

  // A kilde opened in the kildeutforsker. The drill-in replaces the table with `KildeView`, a
  // component the default-state scan never sees at all. It fetches too, so the same wait applies.
  'kilde-drilldown': async page => {
    const name = page.locator('button.munin-explorer-kilder__name').first();
    await name.waitFor({ state: 'visible', timeout: findTimeout });
    await name.click();

    await page
      .locator('.munin-explorer-drilldown[aria-busy="false"]')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },
  // The same drill-in scrolled past its own hero fact row, which is the only state in this file
  // where the sticky bar is on screen at all: the markup renders it hidden and the package's
  // browser module is what shows it, so axe sees none of it in any state above
  // (Fhi.Metadata-35w0p.28).
  'kilde-stuckbar': async page => {
    await states['kilde-drilldown'](page);

    const row = page.locator('.munin-explorer-page__facts').first();
    await row.waitFor({ state: 'visible', timeout: findTimeout });

    // Stepped rather than jumped, and shared with state-assertions.mjs so the step and the
    // overshoot cannot drift apart between the two gates that depend on them (Fhi.Metadata-14j7i).
    await scrollPast(page, await row.getAttribute('id'));

    // Waited for rather than assumed: a bar that never arrives is a state nobody entered, and axe
    // reports no violations in markup that is still display:none.
    await page.locator('.munin-explorer-page__stuckbar--on')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },
  // A kilde row opened on its datasamlinger. The panel only exists after a press, so everything
  // in it - the colspan cell, the nested tables, the headings and the live region - is invisible
  // to the kilder-list scan above (Fhi.Metadata-mq24y).
  'kilder-expanded': async page => {
    const toggle = page.locator('.munin-explorer-kilder__expand-toggle').first();
    await toggle.waitFor({ state: 'visible', timeout: findTimeout });
    await toggle.click();

    await page
      .locator('.munin-explorer-kilder__expanded table')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },

  // The kilde table's column picker, open, with a column turned on that the default view does not
  // draw. Two things only exist in this state: the disclosure and its toggles, and the cells of
  // the seven columns behind it - and those cells are what a column added to the header and not to
  // the body would put under the wrong heading (Fhi.Metadata-ay3zz).
  'kilder-columns': async page => {
    const picker = page.locator('.munin-explorer-header details').first();
    await picker.waitFor({ state: 'visible', timeout: findTimeout });
    await picker.locator('summary').click();

    // Found through the item rather than by role: the box carries no text of its own, its name
    // coming from the sibling label span, and check() rather than click() so the state is what is
    // asked for and not whatever a press toggles it to (Fhi.Metadata-f6az7).
    const toggle = picker
      .locator('.dropdown-choicepicker__item', { hasText: 'Dataansvarlig' })
      .locator('input[type=checkbox]')
      .first();
    await toggle.waitFor({ state: 'visible', timeout: findTimeout });
    await toggle.check();

    await page
      .locator('.munin-explorer-kilder thead th', { hasText: 'Dataansvarlig' })
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },

  // The kilder table with all three of its count columns drawn. Delkilder sits in the component's
  // hidden set, so the list as it loads shows two of the three, and the geometry pin on Stiler's
  // alignment rule would otherwise never measure that column at all (Fhi.Metadata-y7ilr).
  'kilder-counts': async page => {
    await rowsArePresent(page, 'button.munin-explorer-kilder__name');

    const picker = page.locator('.munin-explorer-header details').first();
    await picker.waitFor({ state: 'visible', timeout: findTimeout });
    await picker.locator('summary').click();

    // Through the item and not by role, for the reason kilder-columns above gives: the box carries
    // no text of its own, and check() asks for a state rather than flipping whatever is there.
    const toggle = picker
      .locator('.dropdown-choicepicker__item', { hasText: 'Delkilder' })
      .locator('input[type=checkbox]')
      .first();
    await toggle.waitFor({ state: 'visible', timeout: findTimeout });
    await toggle.check();

    await page
      .locator('.munin-explorer-kilder thead th', { hasText: 'Delkilder' })
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });

    // Folded again before anything is measured: this state is here for the table, and every
    // geometry assertion would otherwise be measuring a dropdown hanging open over it.
    if (await picker.evaluate(el => el.open)) {
      await picker.locator('summary').click();
    }
    if (await picker.evaluate(el => el.open)) {
      throw new Error('The column picker stayed open after its summary was pressed');
    }
  },

  // The kildeutforsker's facet panel: a second facet opened from the keyboard, a value ticked
  // inside it, and then the Utvid alle / Skjul alle pair over the top. Nothing in the component
  // mirrors the folds — `open` is written once per fold press and left alone between them — so this
  // is the only place any of it is exercised at all: bUnit re-serialises the markup from the render
  // tree and never runs a browser's native <details> toggle, let alone a Blazor diff arriving over
  // one (Fhi.Metadata-co3sf, Fhi.Metadata-l9l2n.60).
  'kilde-facets': async page => {
    await rowsArePresent(page, 'button.munin-explorer-kilder__name');

    // Pressed only where it is on screen. Above the sample's 1024px sidebar breakpoint the toggle
    // is display:none and the panel is unfolded already, and a click on it there would fold the
    // very thing this state is here to scan.
    const toggle = page.locator('.munin-explorer-filters__toggle');
    if (await toggle.isVisible()) {
      await toggle.click();
    }

    const facets = page.locator('.munin-explorer-filters__facets > details');
    await facets.first().waitFor({ state: 'visible', timeout: findTimeout });

    const open = () => page.locator('.munin-explorer-filters__facets > details[open]').count();
    if (await open() !== 1) {
      throw new Error('The facet panel must open exactly one facet and fold the rest');
    }

    // Held by position, never by `:not([open])`: a locator written on the fold re-resolves on every
    // use, so the moment the press lands it names the NEXT folded facet and the wait below sits on
    // a <ul> that is hidden by design.
    const foldedIndex = await facets.evaluateAll(all => all.findIndex(facet => !facet.open));
    if (foldedIndex < 0) {
      throw new Error('The facet panel drew no folded facet to open');
    }

    const folded = facets.nth(foldedIndex);
    const values = folded.locator(':scope > ul');

    // Asserted BEFORE the press, and this is the half that matters: the values are rendered whether
    // or not the facet is open, so a host stylesheet that beats the browser's own hiding leaves 39
    // databehandlere on screen under a shut disclosure. That is how the first attempt at this panel
    // shipped 3798px of folded facet to helsedata (Fhi.Metadata-co3sf).
    if (await values.isVisible()) {
      throw new Error('A folded facet is showing its values — the disclosure is hiding nothing');
    }

    const summary = folded.locator(':scope > summary');
    await summary.focus();
    await page.keyboard.press('Enter');
    await values.waitFor({ state: 'visible', timeout: findTimeout });

    if (!await summary.evaluate(el => el === document.activeElement)) {
      throw new Error('Opening a facet moved focus away from its summary');
    }
    if (await open() !== 2) {
      throw new Error('Opening a second facet folded the one already open');
    }

    // And then a re-render over the top of it, which is the claim the component rests on: `open` is
    // written by a fold press and by nothing else, so narrowing the list cannot collapse what the
    // reader opened.
    // The wait is on the summary gaining its count, because that element comes back over the
    // circuit — the tick alone lands in the browser before Blazor has diffed anything.
    await values.locator('input[type=checkbox]').first().check();
    await folded.locator(':scope > summary .munin-explorer-filters__chosen')
      .waitFor({ state: 'visible', timeout: findTimeout });

    if (await open() !== 2) {
      throw new Error('Narrowing the list folded a facet the reader had opened');
    }

    // The chips the tick puts over the results, waited for rather than assumed: they are the newest
    // markup this state reaches, and axe reports no violations in an element that never rendered.
    await page.locator('.munin-explorer-filters__chip').first()
      .waitFor({ state: 'visible', timeout: findTimeout });

    // Utvid alle, and the trap it must not cost. A press rebuilds every disclosure under a new key
    // so the new `open` lands; every render after it has to leave them alone again. The facet
    // folded below is folded by the READER, in the browser, which is the half no test in this
    // repository can stage — bUnit's DOM cannot disagree with the render tree, and this defect is
    // that disagreement. (Fhi.Metadata-l9l2n.60)
    const total = await facets.count();
    const foldRow = page.locator('.munin-explorer-filters > .munin-explorer-filters__toolbar > button');

    // The pair through the direct-child chain Stiler's pinning rule uses, so a row emitted one
    // level deeper — which is how that rule fails, in silence — is a red run rather than a
    // screenshot nobody takes.
    if (await foldRow.count() !== 2) {
      throw new Error('The facet panel drew no Utvid alle / Skjul alle pair as a child of the panel');
    }

    const openCountIs = expected => page.waitForFunction(
      n => document.querySelectorAll('.munin-explorer-filters__facets > details[open]').length === n,
      expected,
      { timeout: findTimeout });

    await foldRow.first().click();
    await openCountIs(total);

    // Folded from the keyboard, and on the facet the panel opens by default: what the assertions
    // below turn on is the DOM disagreeing with the last `open` this component rendered.
    const first = facets.first();
    await first.locator(':scope > summary').focus();
    await page.keyboard.press('Enter');
    await openCountIs(total - 1);

    // The narrowing render, twice — the tick taken off and put back on, so the state axe finally
    // scans still has the chips in it.
    const chosen = folded.locator(':scope > summary .munin-explorer-filters__chosen');

    await values.locator('input[type=checkbox]').first().uncheck();
    await chosen.waitFor({ state: 'detached', timeout: findTimeout });

    if (await first.evaluate(el => el.open)) {
      throw new Error('Narrowing re-opened a facet the reader folded after Utvid alle');
    }

    await values.locator('input[type=checkbox]').first().check();
    await chosen.waitFor({ state: 'visible', timeout: findTimeout });

    if (await first.evaluate(el => el.open) || await open() !== total - 1) {
      throw new Error('Narrowing undid the fold the reader left after Utvid alle');
    }
  },

  // The composed explorer on /utforsker, which is the only page in either sample that draws the
  // page-level tablist. The front page mounts VariableSearch on its own, so neither the tabs nor
  // the reader's list panel appears in any state above (Fhi.Metadata-l9l2n.39).
  //
  // The wait is on a result row, not on the tablist: the tabs render before the search answers,
  // so waiting on them would scan a page whose data never arrived.
  'explorer-tabs': page => rowsArePresent(page, 'button.munin-explorer-dataitem-main__name'),

  // The same page with the second tab open. The list panel is in the DOM from the first render —
  // `hidden`, so axe skips it — and only becomes something to judge once the tab is pressed.
  'explorer-list-tab': async page => {
    await rowsArePresent(page, 'button.munin-explorer-dataitem-main__name');

    // getByRole('tab'), not 'button': an explicit role="tab" replaces the element's implicit
    // button role, so the press helper above cannot see it.
    const tab = page.getByRole('tab', { name: 'Variabelliste', exact: true }).first();
    await tab.waitFor({ state: 'visible', timeout: findTimeout });
    await tab.click();

    await page
      .locator('[role=tabpanel]:not([hidden]) .munin-explorer-data-list')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
  },
};
