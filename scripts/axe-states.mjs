// The page states the accessibility scan visits beyond a plain page load: a name, and a function
// that drives a loaded page into it. Add one here and as a `path::state` target in
// check-accessibility.sh. Why at all: AGENTS.md, "It scans states, not only pages".
//
// Controls are found by the name a reader presses them under, from `Texts.cs` in Norwegian since
// both samples mount with `Language="no"`. A control this file cannot find stops the scan as a
// TOOLING failure rather than leaving the page in a state nobody entered but axe reports under.
//
// States wait for content, never merely for the page. The data comes from axe-stub-api.mjs, so
// "no rows yet" means the component is broken rather than that a network call is slow.

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
  // The two list pages as they load. They wait for a row rather than for the page, because the
  // data can fail to arrive and an empty list is a page axe reports no violations in — which is
  // how this gate read green against an unreachable API for as long as it has existed.
  'variables-list': page => rowsArePresent(page, 'button.munin-explorer-dataitem-main__name'),
  'kilder-list': page => rowsArePresent(page, 'button.munin-explorer-kilder__name'),

  // The filter tree with every facet unfolded and the guide lines drawn. This is the state the
  // 1.16:1 level lines shipped in (Fhi.Metadata-wcbxi): unfolding first matters because axe skips
  // what a closed <details> hides, so the lines have to be on screen to be judged at all.
  'filters-level-lines': async page => {
    const panel = page.locator('.munin-explorer-filters');
    await panel.waitFor({ state: 'visible', timeout: findTimeout });

    await press(panel, 'Utvid alle');
    await press(panel, 'Nivålinjer');

    await page
      .locator('.munin-explorer-filters[data-level-lines="true"] ul ul')
      .first()
      .waitFor({ state: 'visible', timeout: findTimeout });
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
};
