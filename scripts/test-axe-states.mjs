import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import { chromium } from 'playwright';
import { states } from './axe-states.mjs';

let browser;
before(async () => {
  const channel = process.env.PLAYWRIGHT_BROWSER_CHANNEL;
  browser = await chromium.launch(channel ? { channel } : {});
});
after(async () => { await browser?.close(); });

const link = '<li><a class="munin-explorer-hierarchy__open" href="/collection">Open collection</a></li>';
const branch = (name, children) => `<li><details><summary>${name}</summary><ul>${children}</ul></details></li>`;

async function withHierarchy(children, check) {
  const page = await browser.newPage();
  try {
    await page.setContent(`
      <button>Tromsøundersøkelsen</button>
      <div class="munin-explorer-hierarchy"><ul>${children}</ul></div>
    `);
    await check(page);
  } finally {
    await page.close();
  }
}

// Native details hides descendants without removing them from the DOM; a mock cannot prove
// that the state opens enough ancestors to make its chosen link reachable.
test('HierarchyOpen_WhenOnlyANestedBranchHasALink_ThenTheLinkBecomesVisible', async () => {
  await withHierarchy(
    branch('Empty', '<li>No collections</li>')
      + branch('Parent', branch('Child', branch('Grandchild', link)))
      + branch('Unneeded sibling', '<li>No collections</li>'),
    async page => {
      await states['kilde-hierarchy-open'](page);
      assert.equal(await page.locator('.munin-explorer-hierarchy__open:visible').count(), 1);
      assert.equal(await page.getByText('Unneeded sibling', { exact: true })
        .evaluate(summary => summary.parentElement.open), false);
    },
  );
});

test('HierarchyOpen_WhenATopLevelLinkFollowsAClosedBranch_ThenNoBranchNeedsOpening', async () => {
  await withHierarchy(branch('Hidden link first', link) + link, async page => {
    await states['kilde-hierarchy-open'](page);
    assert.equal(await page.locator('.munin-explorer-hierarchy__open:visible').count(), 1);
    assert.equal(await page.locator('details[open]').count(), 0);
  });
});

test('HierarchyOpen_WhenNoBranchContainsALink_ThenTheStateFails', async () => {
  await withHierarchy(branch('Parent', branch('Empty child', '<li>No collections</li>')), async page => {
    await assert.rejects(
      states['kilde-hierarchy-open'](page),
      /No datasamling link is visible and no hierarchy branch remains to open/,
    );
  });
});
