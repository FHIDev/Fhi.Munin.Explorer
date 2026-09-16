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

/**
 * The variabelgruppe the `filters-variabelgrupper` state narrows the Kilde facet to.
 *
 * A catalogue name, so a re-capture can take it away — which is why every read of it below throws
 * rather than returning nothing. Chosen for two properties the capture is thin in: the group is
 * drawn under two datasamlinger of one kilde, and its `filter` is `"2"`. (Fhi.Metadata-g51gg)
 */
export const VARIABELGRUPPE = 'Assistert befruktning';

/**
 * A term the Kilde facet matches nothing at all with.
 *
 * Not a catalogue name and deliberately not a word: the facet matches over every level under a
 * kilde, the variabelgrupper included, so anything that reads like Norwegian risks matching one of
 * the catalogue's group names and staging a narrowed facet in place of an empty one.
 */
export const NO_MATCH = 'zzzz-ingen-treff-zzzz';

/**
 * The Kilde facet, found by the one control only it has: its own search box.
 *
 * By that box rather than by the heading word, because `FieldSource` names the facet and a column
 * alike — and rather than by position, because `FacetGroups` drops a facet the API answered
 * nothing for, so the index moves with the payload.
 */
export const kildeFacet = page => page.locator('.munin-explorer-filters details')
  .filter({ has: page.locator('input.munin-explorer-filters__search') })
  .first();

/**
 * Every row of the filter panel whose own label names `name`, in the order they are drawn.
 *
 * By the label's own text and not by a role query, because the count inside it joins the
 * checkbox's accessible name and that count is cross-filtered — a locator holding `Kosthold (5)`
 * stops matching the moment a tick moves the 5. `index` is into the panel's `<li>` list, which is
 * how a caller presses one; `selectable` says the row is a checkbox rather than a container.
 */
export const groupRows = (page, name) => page.locator('.munin-explorer-filters li')
  .evaluateAll((items, wanted) => items
    .map((item, index) => {
      const label = item.querySelector(':scope > label');

      if (label === null || !(label.textContent ?? '').trim().startsWith(`${wanted} (`)) {
        return null;
      }

      const box = label.querySelector('input[type=checkbox]');

      return { index, selectable: box !== null, checked: box?.checked ?? false };
    })
    .filter(row => row !== null), name);

/**
 * Wait until the tree draws `name` at two placements, and hand those rows back.
 *
 * Polled from here rather than with `waitForFunction`, so the one reader above is what both the
 * wait and the assertions read: a predicate copied into the page would be a second definition of
 * "which row is this group" and free to drift from it.
 */
async function untilTwoPlacements(page, name) {
  const deadline = Date.now() + findTimeout;

  for (;;) {
    const rows = await groupRows(page, name);

    if (rows.length >= 2) {
      return rows;
    }

    if (Date.now() > deadline) {
      throw new Error(`the kilde tree drew ${rows.length} row(s) for "${name}" where it draws two: ` +
        'the fixture no longer carries a group placed under two datasamlinger of one kilde');
    }

    await page.waitForTimeout(250);
  }
}

export const states = {
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
  },

  // The Kilde facet's tree open down to its variabelgrupper — the level Fhi.Metadata-g51gg added.
  // filters-level-lines above already draws them, by unfolding the whole captured catalogue, which
  // is the wrong vehicle for a PRESS: a tick rebuilds every row of it. The facet's own search keeps
  // the matching kilde and opens the branches standing between it and the match, so this state
  // reaches the same level in the rows of one kilde.
  //
  // VARIABELGRUPPE names a group the capture draws under two datasamlinger of one kilde, both of
  // them hanging straight off that kilde with no delkilde between — the majority shape in the
  // catalogue, and the one an inner join loses. Its `filter` is `"2"`, which the tree draws as a
  // checkbox all the same, the opt-out being the standalone facet's rule alone
  // (Fhi.Metadata-fbe3w). Every shape the assertions in state-assertions.mjs need, in one state.
  'filters-variabelgrupper': async page => {
    const panel = page.locator('.munin-explorer-filters');
    await panel.waitFor({ state: 'visible', timeout: findTimeout });

    // On an <input>, which is where Stiler's rule for the name is scoped, so a box that moved to
    // another element fails here rather than drawing at the page's own size on helsedata.
    const box = panel.locator('input.munin-explorer-filters__search');
    await box.first().waitFor({ state: 'visible', timeout: findTimeout });

    // Committed with Enter rather than by clicking away, which is what onchange also answers to:
    // a commit that narrows puts focus back in this box (Fhi.Metadata-6we8a), so a click on some
    // other control to fire it would be a press the rescue then takes the reader off.
    await box.first().fill(VARIABELGRUPPE);
    await box.first().press('Enter');

    // On two placements of the group, never on the row count: reaching one would pass against a
    // tree that had stopped repeating a group under every datasamling its variables are in, which
    // is the shape both assertions turn on.
    const rows = await untilTwoPlacements(page, VARIABELGRUPPE);

    // Each of them a checkbox, which is the opt-out claim: this group carries Filter="2" in the
    // capture, and a container row here would mean the tree had started reading it.
    if (rows.some(row => !row.selectable)) {
      throw new Error(`"${VARIABELGRUPPE}" is drawn as a container in the kilde tree, which reads ` +
        'the standalone facet\'s opt-out that is not its rule');
    }
  },

  // The Kilde facet with nothing left in it: a term matching no kilde, no delkilde, no datasamling
  // and no variabelgruppe. The facet stays standing rather than dropping out, because dropping out
  // would take the box the reader has to widen the term in away with it (Fhi.Metadata-l9l2n.67) —
  // so what axe judges here is a disclosure holding a message and a search field and no list at
  // all, which is the one empty state either explorer reaches without the stub answering
  // differently than it does.
  'filters-no-match': async page => {
    const panel = page.locator('.munin-explorer-filters');
    await panel.waitFor({ state: 'visible', timeout: findTimeout });

    const facet = kildeFacet(page);
    await facet.locator('li').first().waitFor({ state: 'visible', timeout: findTimeout });

    const box = panel.locator('input.munin-explorer-filters__search').first();
    await box.fill(NO_MATCH);
    await box.press('Enter');

    await facet.locator('li').first().waitFor({ state: 'detached', timeout: findTimeout });

    // The facet itself, and the box inside it. Either one gone is the defect this state exists to
    // keep an eye on, and axe reports no violations in a facet that is no longer on the page.
    if (await facet.count() !== 1 || await box.count() !== 1) {
      throw new Error('a search matching nothing took the Kilde facet or its own search box away');
    }
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
  },

  // A variable row opened. The panel under the row is the largest block of markup in the package
  // that only exists after a click — every property, the statistics block and the owner buttons.
  // It fetches, so the wait is on the region reporting itself done rather than on it appearing.
  'variable-detail': async page => {
    const row = page.locator('button.munin-explorer-dataitem-main__name').first();
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
