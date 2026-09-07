// Breaks the hostile host on purpose, one defect at a time, and insists the matching geometry
// assertion says so. Exits 1 when an assertion stayed quiet, 2 when it could not run.
//
// WHY. A scanner that reports success without looking is worse than no scanner, and this
// repository has already shipped two of them: the drift guard that skipped and said nothing
// (assert-drift-ran.sh) and the sample stylesheets nothing compared against Stiler
// (Fhi.Metadata-3dwar). geometry-scan.mjs is the same shape of risk — every assertion in it can
// only be trusted while something proves it still fires. So this hands each one a page carrying
// the defect it was written for and requires a finding, exactly as
// assert-portability-guard-armed.sh hands the compiler a banned symbol.
//
// Each case measures TWICE at one width: unbroken, where the assertion must hold, and broken,
// where it must not. The unbroken half is not ceremony — it is what separates "the assertion
// fires" from "the assertion always fires here".
//
// Usage:  node geometry-negative-control.mjs <base-url>
// Called by check-hostile-host.sh, which owns starting the host.
import { chromium } from 'playwright';
import { states } from './axe-states.mjs';
import { assertions, selectors } from './geometry-assertions.mjs';

const base = process.argv[2];
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);

if (!base) {
  console.error('usage: node geometry-negative-control.mjs <base-url>');
  process.exit(2);
}

const css = content => page => page.addStyleTag({ content });

// `setup` runs before the unbroken measurement and stays for the broken one, so a case can build
// the arrangement it needs — a table too wide for its box — and then break only the one thing
// under test.
const cases = [
  {
    assertion: 'no horizontal overflow',
    defect: 'an element wider than the page',
    path: '/kilder', state: 'kilder-list', width: 1440,
    apply: page => page.evaluate(() => document.querySelector('.munin-explorer')
      .insertAdjacentHTML('beforeend', '<div style="width:4000px;height:8px"></div>')),
  },
  {
    assertion: 'the component stays inside the box the host gave it',
    defect: 'a scroll box that clips its content instead of scrolling it',
    path: '/kilder', state: 'kilder-list', width: 1024,
    setup: css('.munin-explorer-kilder { min-width: 1600px; }'),
    apply: css('.munin-explorer-kilder-scroll { overflow-x: hidden !important; }'),
  },
  {
    assertion: 'the component stays inside the box the host gave it',
    defect: 'a scroll box no keyboard can reach',
    path: '/kilder', state: 'kilder-list', width: 1024,
    setup: css('.munin-explorer-kilder { min-width: 1600px; }'),
    apply: page => page.evaluate(() =>
      document.querySelector('.munin-explorer-kilder-scroll').removeAttribute('tabindex')),
  },
  {
    assertion: 'hidden means hidden',
    defect: 'a rule that un-hides without naming [hidden], the 2026-09-03 defect',
    path: '/kilder', state: 'kilder-list', width: 843,
    // The toggle goes with it, so the fold is inert and only the rule half of the check can be
    // what fails: this is the accidental un-hide, wearing the deliberate one's other condition.
    apply: css('.munin-explorer-filters__toggle { display: none !important; }\n' +
      '.munin-explorer-filters__facets { display: block !important; }'),
  },
  {
    assertion: 'hidden means hidden',
    defect: 'a deliberate un-hide whose fold is still on screen',
    path: '/kilder', state: 'kilder-list', width: 1440,
    // Stiler's own `[hidden]`-naming rule is in force here and the panel is meant to be open.
    // Putting the toggle back makes it a control the reader can press whose aria-expanded says
    // "false" over a panel they can see, and the exemption has to stop applying.
    apply: css('.munin-explorer-filters__toggle { display: inline-block !important; }'),
  },
  {
    assertion: 'nothing the reader can press is under the host header',
    defect: 'a control in the band the absolute header covers',
    path: '/kilder', state: 'kilder-list', width: 1440,
    apply: page => page.evaluate(() => document.querySelector('.munin-explorer')
      .insertAdjacentHTML('beforeend',
        '<button style="position:fixed;top:8px;left:8px">under the header</button>')),
  },
  {
    assertion: 'text a reader is meant to see has a box to see it in',
    defect: 'a row of names collapsed to zero height',
    path: '/kilder', state: 'kilder-list', width: 1440,
    apply: css('.munin-explorer-kilder__name { display: block; height: 0 !important; ' +
      'overflow: hidden; }'),
  },
  {
    assertion: 'the tablist clears the header',
    defect: 'the tablist back at document top, under the header',
    path: '/', state: 'explorer-tabs', width: 1440,
    apply: css('[role=tablist] { position: fixed; top: 0; left: 0; }'),
  },
  {
    assertion: 'exactly one tab panel has content',
    defect: 'both panels rendering at once',
    path: '/', state: 'explorer-tabs', width: 1440,
    apply: page => page.evaluate(() => {
      for (const panel of document.querySelectorAll('[role=tabpanel]')) {
        if ((panel.textContent ?? '').trim().length === 0) panel.append('the other panel');
      }
    }),
  },
  {
    assertion: 'no page shell class inside a tab panel',
    defect: 'a nested view wearing the page shell class',
    path: '/', state: 'explorer-tabs', width: 1440,
    apply: page => page.evaluate(() =>
      document.querySelector('[role=tabpanel] *').classList.add('munin-explorer')),
  },
];

// An assertion nobody broke on purpose is an assertion nobody has seen work. Checked before a
// browser starts, so adding one to geometry-assertions.mjs without a case here is a message.
const uncovered = assertions
  .map(a => a.name)
  .filter(name => !cases.some(c => c.assertion === name));
if (uncovered.length > 0) {
  console.error('no negative control for: ' + uncovered.join('; ') + ' - TOOLING failure.');
  console.error('add a case to scripts/geometry-negative-control.mjs that makes each one fail.');
  process.exit(2);
}

const unknown = cases.filter(c => !assertions.some(a => a.name === c.assertion));
if (unknown.length > 0) {
  console.error('cases name assertions that no longer exist: ' +
    unknown.map(c => c.assertion).join('; ') + ' - TOOLING failure.');
  process.exit(2);
}

let browser;
try {
  browser = await chromium.launch();
} catch (err) {
  console.error('could not start a browser - this is a TOOLING failure, not a finding.');
  console.error(String(err?.message ?? err));
  process.exit(2);
}

let quiet = 0;

try {
  for (const testCase of cases) {
    const { assertion, defect, path, state, width, setup, apply } = testCase;
    const body = assertions.find(a => a.name === assertion).body;
    const url = `${base}${path}`;
    console.log(`\n==> "${assertion}" against ${defect}`);
    console.log(`    ${url} [${state}] at ${width}px`);

    const context = await browser.newContext({ viewport: { width, height: 900 } });
    const page = await context.newPage();
    try {
      await page.goto(url, { waitUntil: 'networkidle', timeout: 60_000 });
      await page.waitForTimeout(settleMs);
      await states[state](page);
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.waitForTimeout(250);
      if (setup) {
        await setup(page);
        await page.waitForTimeout(250);
      }
    } catch (err) {
      console.error(`could not set the page up - TOOLING failure.`);
      console.error(String(err?.message ?? err));
      await browser.close();
      process.exit(2);
    }

    const before = await page.evaluate(body, selectors);
    if (before !== null) {
      // The assertion was already failing, so making it fail proves nothing about it.
      console.error(`    the unbroken page already fails this assertion - TOOLING failure.`);
      console.error(`    ${before}`);
      await browser.close();
      process.exit(2);
    }

    try {
      await apply(page);
      await page.waitForTimeout(250);
    } catch (err) {
      console.error('could not apply the defect - TOOLING failure.');
      console.error(String(err?.message ?? err));
      await browser.close();
      process.exit(2);
    }

    const after = await page.evaluate(body, selectors);
    if (after === null) {
      quiet += 1;
      console.log('    FAIL the assertion held against the broken page and reported nothing.');
    } else {
      console.log('    ok   held before, and after the break said:');
      console.log(`         ${after}`);
    }

    await page.close();
    await context.close();
  }
} catch (err) {
  console.error('the negative control did not complete - TOOLING failure, not a finding.');
  console.error(String(err?.stack ?? err));
  await browser.close();
  process.exit(2);
} finally {
  await browser.close().catch(() => {});
}

console.log('');
if (quiet > 0) {
  console.log(`${quiet} geometry assertion(s) did not fire against the defect they exist for.`);
  console.log('A green geometry run means nothing while that is true.');
} else {
  console.log(`all ${cases.length} defects were caught by the assertion written for them.`);
}

process.exit(quiet > 0 ? 1 : 0);
