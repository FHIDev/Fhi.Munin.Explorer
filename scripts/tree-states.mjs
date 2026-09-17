import { TREE_SEARCH, EMPTY_SEARCH, LARGE_COUNT, names } from './tree-fixture.mjs';

export const panel = page => page.locator('.munin-explorer-filters');
export const tree = page => panel(page).locator('details').filter({
  has: page.locator(':scope > summary').filter({ hasText: /^Kilde(?:\s|$)/ }),
});
export const standalone = page => panel(page).locator('details').filter({
  has: page.locator(':scope > summary').filter({ hasText: /^Variabelgruppe(?:\s|$)/ }),
});
// A name is scoped to its facet and placement; no ordinal survives a Blazor refetch.
export const row = (scope, name) => scope.getByText(name, { exact: true }).locator('xpath=ancestor::li[1]');
export const boxes = (scope, name) => row(scope, name).locator(':scope > label > input[type=checkbox]');
export const disclosure = (scope, name) => row(scope, name).locator(':scope > button[aria-expanded]');
export const chips = page => page.locator('.munin-explorer-filters__chip');

export async function until(read, description) {
  const deadline = Date.now() + 15_000;
  while (!await read()) {
    if (Date.now() >= deadline) throw new Error(`Timed out: ${description}`);
    await new Promise(resolve => setTimeout(resolve, 50));
  }
}

export async function fold(scope, name, open) {
  const button = disclosure(scope, name);
  await button.focus();
  await button.press('Space');
  await until(async () => await button.getAttribute('aria-expanded') === String(open), `${name} disclosure`);
}

async function load(page, empty = false) {
  const box = page.locator('input.searchbox__freetext').first();
  await box.fill(empty ? EMPTY_SEARCH : TREE_SEARCH);
  await page.getByRole('button', { name: 'Søk', exact: true }).click();
  await boxes(tree(page), names.kilde).waitFor({ state: 'attached' });
  // Narrow layouts now start with the whole filter panel folded behind its own toggle.
  if (!await panel(page).isVisible()) {
    await page.getByRole('button', { name: 'Vis filtre', exact: true }).click();
  }
  await boxes(tree(page), names.kilde).waitFor({ state: 'visible' });
  await until(async () => await panel(page).getAttribute('aria-busy') === 'false', 'tree response rendered');
  if (await tree(page).locator('button[aria-expanded="true"]').count()) {
    throw new Error('The fixture tree must arrive collapsed');
  }
  if (await chips(page).count()) throw new Error('Loading the tree selected a filter');
}

async function populate(page, empty = false) {
  await load(page, empty);
  await panel(page).getByRole('button', { name: 'Utvid alle', exact: true }).click();
  await until(async () => await boxes(tree(page), 'Gruppe 120').count() === 1, 'large tree expanded');
  for (const [name, count] of [[names.offered, 2], [names.excluded, 2], [names.unset, 1],
    [names.unassigned, 1], [names.root, 1], [names.child, 1]]) {
    if (await boxes(tree(page), name).count() !== count) throw new Error(`Missing tree placement: ${name}`);
  }
  for (const [parent, child] of [[names.kilde, names.direct], [names.delkilde, names.unassigned],
    [names.kilde, names.root], [names.direct, names.unset]]) {
    const children = row(tree(page), parent).locator(':scope > ul > li > label > span');
    if (await children.filter({ hasText: new RegExp(`^${child}$`) }).count() !== 1) {
      throw new Error(`${child} must be placed directly under ${parent}`);
    }
  }
  if (await boxes(standalone(page), names.excluded).count() ||
      await boxes(standalone(page), names.container).count()) {
    throw new Error('Filter="2" was offered as a standalone checkbox');
  }
  if (await row(standalone(page), names.container).locator(':scope > span').count() !== 1 ||
      await boxes(standalone(page), names.child).count() !== 1) {
    throw new Error('The opted-out ancestor must remain a container for its offered child');
  }
  for (const name of [names.offered, names.unset]) {
    if (await boxes(standalone(page), name).count() !== 1) throw new Error(`Missing standalone option: ${name}`);
  }
  if (await row(tree(page), names.large).locator(':scope > ul > li').count() !== LARGE_COUNT) {
    throw new Error('Large tree lost groups');
  }
}

export const treeStates = {
  'tree-collapsed': page => load(page),
  'tree-populated': page => populate(page),
  'tree-empty-results': async page => {
    await populate(page, true);
    await page.getByText(/Ingen variabler passet søket/).first().waitFor({ state: 'visible' });
    if (await page.locator('button.munin-explorer-dataitem-main__name').count()) {
      throw new Error('The empty-result state still draws variables');
    }
  },
  'tree-no-match': async page => {
    await populate(page);
    const search = panel(page).locator('input.munin-explorer-filters__search');
    await search.fill('ingen-grupper-matcher-dette');
    await search.press('Enter');
    await until(async () => await tree(page).locator('li').count() === 0, 'facet search cleared rows');
  },
};
