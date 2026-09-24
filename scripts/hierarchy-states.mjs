import { names, topLevel, SHARED_PLACEMENTS, WAVE_CATEGORIES } from './hierarchy-fixture.mjs';
import { until } from './tree-states.mjs';

const tree = page => page.locator('.munin-explorer-hierarchy');
const topRows = page => tree(page).locator(':scope > ul > li');
// A name's own span, never an ordinal: the view orders by presentationOrder, then by name.
const rows = (scope, name) => scope.getByText(name, { exact: true }).locator('xpath=ancestor::li[1]');
const label = row => row.locator(':scope > details > summary, :scope > .munin-explorer-hierarchy__leaf');
const branch = row => row.locator(':scope > details');
const visible = locator => locator.evaluate(el => el.checkVisibility());

async function arrived(page) {
  await topRows(page).first().waitFor({ state: 'visible', timeout: 15_000 });
  await until(async () => await tree(page).getAttribute('aria-busy') === 'false', 'hierarchy loaded');
}

// Counts and order as the fixture sends them, read off the rows rather than trusted.
async function assertTopLevel(page) {
  const drawn = await topRows(page).evaluateAll(items => items.map(li => {
    const own = li.querySelector(':scope > details > summary, :scope > .munin-explorer-hierarchy__leaf');
    return [own.querySelector(':scope > span[lang], :scope > span:not([class])')?.textContent,
      own.querySelector(':scope > .munin-explorer-hierarchy__count')?.firstChild?.textContent];
  }));
  const expected = topLevel.map(([name, count]) => [name, String(count)]);
  if (JSON.stringify(drawn) !== JSON.stringify(expected)) {
    throw new Error(`Top-level rows ${JSON.stringify(drawn)}, expected ${JSON.stringify(expected)}`);
  }
}

async function open(row, key = 'Enter') {
  const summary = branch(row).locator(':scope > summary');
  await summary.focus();
  await summary.press(key);
  await until(async () => await branch(row).evaluate(el => el.open), 'branch opened');
  if (!await summary.evaluate(el => el === document.activeElement)) {
    throw new Error('Opening a branch moved focus away from its summary');
  }
}

async function icons(row) {
  return label(row).locator(':scope > .munin-explorer-hierarchy__icons').count();
}

async function spoken(row) {
  return label(row).locator(':scope > .screenreader-only').count();
}

async function deep(page) {
  await arrived(page);
  await assertTopLevel(page);
  if (await tree(page).locator('details[open]').count()) throw new Error('Hierarchy branches must start collapsed');

  // Closed content has to stay out of sight under the host's own rules, not only the browser's.
  const buried = rows(tree(page), names.sharedGrandchild).first();
  if (await visible(buried)) throw new Error('A group inside a closed branch is on screen');

  const main = rows(tree(page), names.main);
  await open(main, 'Enter');
  const followUp = rows(main, names.followUp);
  await open(followUp, ' ');
  await open(rows(followUp, names.deepest), 'Enter');

  const placements = [
    rows(main, names.baseline), rows(followUp, names.wave), rows(followUp, names.uncategorised),
  ];
  for (const owner of placements) await open(owner);
  const shared = placements.map(owner => rows(owner, names.shared));
  if ((await Promise.all(shared.map(one => one.count()))).some(n => n !== 1)) {
    throw new Error(`${names.shared} must be placed once under each of its ${SHARED_PLACEMENTS} owners`);
  }

  // One group id under three owners: opening one placement must leave the other two shut.
  await open(shared[1], ' ');
  for (const other of [shared[0], shared[2]]) {
    if (await branch(other).evaluate(el => el.open)) throw new Error('Opening one placement opened another');
    if (await visible(rows(other, names.sharedChild))) throw new Error('A shut placement shows its children');
  }

  // Everything else, deepest last, so the state is the whole tree for geometry and axe.
  for (let pass = 0; pass < 10; pass += 1) {
    const closed = tree(page).locator('details:not([open]) > summary');
    if (await closed.count() === 0) break;
    await closed.first().click();
  }
  if (await tree(page).locator('details:not([open])').count()) throw new Error('A branch would not open');
  if (await tree(page).getByText(names.sharedGrandchild, { exact: true }).count() !== SHARED_PLACEMENTS) {
    throw new Error('The deepest group is missing from a placement');
  }

  const uncategorised = rows(followUp, names.uncategorised);
  if (await icons(uncategorised) || await spoken(uncategorised)) {
    throw new Error('A datasamling with no categories drew a glyph or said a category');
  }
  // One glyph and one name per category, not merely some: a row that drew the first of three
  // would otherwise pass, and the words are the only place the pairing is said at all.
  const wave = label(rows(followUp, names.wave));
  const glyphs = await wave.locator('.munin-explorer-hierarchy__icons > svg').count();
  const said = (await wave.locator(':scope > .screenreader-only').textContent() ?? '')
    .split(':').pop().split(',').map(one => one.trim()).filter(Boolean);
  if (glyphs !== WAVE_CATEGORIES.length || said.length !== WAVE_CATEGORIES.length) {
    throw new Error(`${names.wave} drew ${glyphs} glyphs and said ${said.length} of ` +
      `${WAVE_CATEGORIES.length} categories: ${said.join(' / ')}`);
  }
  if (await rows(main, names.empty).count() !== 1) throw new Error(`${names.empty} is not drawn`);
  if (await rows(main, names.empty).locator('.munin-explorer-hierarchy__count').count()) {
    throw new Error('A group of nought drew a count');
  }
}

async function status(page, text) {
  await tree(page).getByText(text, { exact: true }).waitFor({ state: 'visible', timeout: 15_000 });
  if (await tree(page).locator('ul').count()) throw new Error('A hierarchy with nothing to show drew a list');
}

// The variable explorer's drill-in, reached the way a reader reaches it: a row, then its kilde.
async function drilled(page, iconsOff) {
  const row = page.locator('button.munin-explorer-dataitem-main__name').first();
  await row.waitFor({ state: 'visible', timeout: 15_000 });
  if (iconsOff) {
    // The facets are a fetch of their own: wait for the panel, or for the toggle folding it away.
    const toggle = page.getByRole('button', { name: 'Vis filtre', exact: true });
    await until(async () => await page.locator('.munin-explorer-filters').isVisible() ||
      await toggle.isVisible(), 'filter panel or its toggle');
    if (!await page.locator('.munin-explorer-filters').isVisible()) {
      await toggle.click();
    }
    const control = page.locator('.munin-explorer-filters').getByRole('switch', { name: 'Ikoner', exact: true });
    await control.click();
    await until(async () => await control.getAttribute('aria-checked') === 'false', 'Ikoner off');
  }
  await row.click();
  await page.getByRole('button', { name: 'Vis datakilde', exact: true }).first().click();
  await arrived(page);
  await assertTopLevel(page);

  const glyphs = await tree(page).locator('.munin-explorer-hierarchy__icons').count();
  const words = await tree(page).locator('summary > .screenreader-only, .munin-explorer-hierarchy__leaf > .screenreader-only').count();
  if (iconsOff ? glyphs + words !== 0 : glyphs === 0 || words === 0) {
    throw new Error(`Ikoner ${iconsOff ? 'off' : 'on'} drew ${glyphs} glyph slots and ${words} spoken categories`);
  }
}

export const hierarchyStates = {
  'kilde-hierarchy-deep': deep,
  'kilde-hierarchy-empty': async page => status(page,
    'Ingen delkilder, datasamlinger eller variabelgrupper er tilgjengelige.'),
  'kilde-hierarchy-error': async page => {
    const error = 'Kunne ikke laste delkilder, datasamlinger og variabelgrupper nå.';
    await status(page, error);
    const retry = tree(page).getByRole('button', { name: 'Prøv å laste strukturen på nytt', exact: true });
    // The error reads the same before and after, so the press is proven by the busy flag it raised.
    await tree(page).evaluate(el => {
      window.__hierarchyBusy = false;
      new MutationObserver(records => {
        window.__hierarchyBusy ||= records.some(r => r.oldValue === 'true' || el.getAttribute('aria-busy') === 'true');
      }).observe(el, { attributes: true, attributeFilter: ['aria-busy'], attributeOldValue: true });
    });
    await retry.focus();
    await retry.press('Enter');
    await until(async () => await page.evaluate(() => window.__hierarchyBusy) &&
      await tree(page).getAttribute('aria-busy') === 'false', 'retry fetched and settled');
    await status(page, error);
    if (!await retry.evaluate(el => el === document.activeElement)) {
      throw new Error('Retrying moved focus off the retry button');
    }
  },
  'variable-kilde-hierarchy': page => drilled(page, false),
  'variable-kilde-hierarchy-icons-off': page => drilled(page, true),
};
