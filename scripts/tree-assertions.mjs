import { names } from './tree-fixture.mjs';
import { panel, tree, standalone, row, boxes, disclosure, chips, fold, until } from './tree-states.mjs';

const openBranches = page => tree(page).locator('button[aria-expanded="true"]')
  .evaluateAll(buttons => buttons.map(button => button.getAttribute('aria-controls')).sort());

// Hold the host's request so neither busy edge can disappear between browser round trips.
async function toggle(page, stub, control) {
  await stub.hold('/api/explorer/filters', 30_000);
  try {
    await control.click();
    await until(async () => (await stub.holding()).length === 0, 'filter request consumed the hold');
    await until(async () => await panel(page).getAttribute('aria-busy') === 'true', 'filter fetch started');
  } finally {
    await stub.release();
  }
  await until(async () => await panel(page).getAttribute('aria-busy') === 'false', 'filter response rendered');
}

// `ticks` is each drawn placement in tree order. A tree tick is scoped to its placement, while the
// standalone facet means the group everywhere, so the two are expected separately (Fhi.Metadata-59dh0).
async function selection(page, name, ticks, everywhere, chosen = ticks.some(Boolean)) {
  const values = boxes(tree(page), name);
  if (await values.count() !== ticks.length) return `${name}: expected ${ticks.length} tree placements`;
  const drawn = await values.evaluateAll(items => items.map(item => item.checked));
  if (JSON.stringify(drawn) !== JSON.stringify(ticks)) {
    return `${name}: tree placements ${JSON.stringify(drawn)}, expected ${JSON.stringify(ticks)}`;
  }
  const options = boxes(standalone(page), name);
  if ((await options.evaluateAll(items => items.map(item => item.checked))).some(value => value !== everywhere)) {
    return `${name}: standalone facet disagrees with the selection`;
  }
  if (await chips(page).count() !== Number(chosen)) return `${name}: expected ${Number(chosen)} chip(s)`;
  if (chosen && !(await chips(page).innerText()).includes(name)) return `${name}: chip names another selection`;
  if (await boxes(standalone(page), names.excluded).count() || await boxes(standalone(page), names.container).count()) {
    return 'Filter="2" leaked into the standalone checkbox options';
  }
  return null;
}

const placements = [[names.offered, 2], [names.excluded, 2], [names.unset, 1],
  [names.unassigned, 1], [names.root, 1], [names.child, 1], ['Gruppe 120', 1]];

const only = (count, at) => Array.from({ length: count }, (_, index) => index === at);

// The DOM property itself, which markup cannot carry: what explorer-interop.js sets after render.
const mixed = (scope, name) => boxes(scope, name).first().evaluate(box => box.indeterminate);

export const treeAssertions = [
  {
    name: 'a tree selection half-ticks the levels above it and nothing else (Fhi.Metadata-cjezd)',
    kind: 'invariant', states: ['tree-populated'],
    async stage(page, stub) {
      await toggle(page, stub, boxes(tree(page), names.excluded).first());
      // After the render the module sets the property; give it the round trip.
      await until(async () => await mixed(tree(page), names.kilde), 'kilde half-ticked');
    },
    async measure(page) {
      for (const level of [names.kilde, names.delkilde, names.first]) {
        if (!await mixed(tree(page), level)) return `${level}: not half-ticked above a chosen placement`;
        if (await boxes(tree(page), level).first().evaluate(box => box.checked)) return `${level}: ticked, not half-ticked`;
      }
      if (await mixed(tree(page), names.excluded)) return `${names.excluded}: the chosen placement is half-ticked`;
      if (await mixed(tree(page), names.second)) return `${names.second}: half-ticked with nothing chosen under it`;
      return null;
    },
    async control(page) {
      await boxes(tree(page), names.kilde).first().evaluate(box => { box.indeterminate = false; });
    },
  },
  {
    name: 'a populated variabelgruppe tree starts collapsed without selecting anything',
    kind: 'invariant', states: ['tree-collapsed'],
    async stage() {},
    async measure(page) {
      if (await tree(page).locator('button[aria-expanded="true"], li > ul').count()) {
        return 'A collapsed tree rendered branch children';
      }
      return await chips(page).count() ? 'A disclosure selected a filter' : null;
    },
    async control(page) {
      await row(tree(page), names.kilde).evaluate(item => item.append(document.createElement('ul')));
    },
  },
  ...placements.map(([name, count]) => ({
    name: `${name}: one tree selection ticks only its own placement and produces one chip`,
    kind: 'invariant', states: ['tree-populated', 'tree-empty-results'],
    async stage(page, stub) {
      const before = await openBranches(page);
      await toggle(page, stub, boxes(tree(page), name).first());
      return { before };
    },
    async measure(page, { before }) {
      const finding = await selection(page, name, only(count, 0), false);
      if (finding !== null) return finding;
      if (JSON.stringify(await openBranches(page)) !== JSON.stringify(before)) {
        return `${name}: selection changed branch disclosure state`;
      }
      return null;
    },
    async control(page) {
      await boxes(tree(page), name).first().evaluate(box => { box.checked = false; });
    },
  })),
  {
    name: 'keyboard collapse and reopen preserve the selected group and sibling disclosures',
    kind: 'invariant', states: ['tree-populated', 'tree-empty-results'],
    async stage(page, stub) {
      await toggle(page, stub, boxes(tree(page), names.excluded).first());
      const before = await openBranches(page);
      await fold(tree(page), names.first, false);
      const hidden = await row(tree(page), names.first).locator(':scope > ul').count();
      // The placement left on screen is the other one, which a scoped tick leaves unticked; the chip stays.
      const chosen = await selection(page, names.excluded, [false], false, true);
      await fold(tree(page), names.first, true);
      return { before, hidden, chosen };
    },
    async measure(page, { before, hidden, chosen }) {
      if (hidden) return 'A folded branch retained child controls';
      if (chosen !== null) return `Folding lost the selection: ${chosen}`;
      if (JSON.stringify(await openBranches(page)) !== JSON.stringify(before)) {
        return 'Reopening a branch changed its siblings';
      }
      return selection(page, names.excluded, only(2, 0), false);
    },
    async control(page) {
      await disclosure(tree(page), names.second).evaluate(button => button.setAttribute('aria-expanded', 'false'));
    },
  },
  ...['standalone', 'chip'].map(surface => ({
    name: `clearing a group from its ${surface} clears every tree placement`,
    kind: 'invariant', states: ['tree-populated'],
    async stage(page, stub) {
      const name = surface === 'chip' ? names.excluded : names.offered;
      await toggle(page, stub, boxes(tree(page), name).first());
      let selected = await selection(page, name, only(2, 0), false);
      if (surface === 'standalone') {
        // Unticked over a scoped tick, so its first press chooses the group everywhere.
        await toggle(page, stub, boxes(standalone(page), name));
        selected ??= await selection(page, name, [true, true], true);
      }
      await toggle(page, stub, surface === 'chip' ? chips(page).getByRole('button') : boxes(standalone(page), name));
      return { name, selected };
    },
    async measure(page, { name, selected }) {
      return selected ?? await selection(page, name, [false, false], false);
    },
    async control(page, { name }) {
      await boxes(tree(page), name).last().evaluate(box => { box.checked = true; });
    },
  })),
  {
    name: 'a facet search that empties the tree restores focus after blur',
    kind: 'invariant', states: ['tree-populated'],
    async stage(page) {
      const search = panel(page).locator('input.munin-explorer-filters__search');
      await search.click();
      await search.pressSequentially('ingen-grupper-matcher-dette');
      await page.locator('input.searchbox__freetext').first().focus();
      await until(async () => await tree(page).locator('li').count() === 0, 'facet search cleared rows');
    },
    async measure(page) {
      const search = panel(page).locator('input.munin-explorer-filters__search');
      if (await search.count() !== 1) return 'The empty facet lost its search field';
      if (await tree(page).locator('li').count()) return 'The facet search did not empty the tree';
      try {
        await until(() => search.evaluate(box => document.activeElement === box), 'focus rescued after blur');
      } catch {
        return 'Focus was not returned to the empty facet search';
      }
      return null;
    },
    async control(page) {
      await panel(page).locator('input.munin-explorer-filters__search').evaluate(box => box.remove());
    },
  },
];
