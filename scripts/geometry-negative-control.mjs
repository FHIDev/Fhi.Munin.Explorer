// Breaks the hostile host on purpose and insists the matching geometry assertion says so: one that
// has quietly stopped measuring anything reports success forever. Each case measures twice at one
// width - unbroken, where it must hold, and broken, where it must not. (Fhi.Metadata-fih3y)
//
// Usage:  node geometry-negative-control.mjs <base-url>
// Exits 1 when an assertion stayed quiet, 2 when it could not run. Called by
// check-hostile-host.sh, which owns starting the host.
import { chromium } from 'playwright';

// PLAYWRIGHT_BROWSER_CHANNEL=msedge runs an installed browser instead of the bundled chromium.
// Opt-in and unset in CI: a channel renders a different engine build, so a geometry number from
// one is not interchangeable with a number from the other. It exists because `playwright install
// chromium` cannot complete on Node 26 - the pinned fetcher calls fs.rmdir(recursive), removed in
// that version - which leaves the port unrunnable on a developer machine (Fhi.Metadata-wgwa0).
const launchOptions = () => {
  const channel = process.env.PLAYWRIGHT_BROWSER_CHANNEL;
  return channel ? { channel } : {};
};
import { states } from './axe-states.mjs';
import { assertions, selectors } from './geometry-assertions.mjs';
import { installUnhiddenOnPurpose } from './hidden-on-purpose.mjs';
import { scrollToTop } from './reader-scroll.mjs';

const base = process.argv[2];
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);

if (!base) {
  console.error('usage: node geometry-negative-control.mjs <base-url>');
  process.exit(2);
}

const css = content => page => page.addStyleTag({ content });

const noBreak = css(`.munin-explorer-page__fields dd, .munin-explorer-meta__grid dd,
                     .munin-explorer-page__facts dt, .munin-explorer-whole__code
                     { overflow-wrap: normal !important; }`);

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
    assertion: 'no horizontal overflow',
    defect: 'a value with no break point at 320px, the 2026-09-15 defect',
    path: '/', state: 'variable-detail-about', width: 320,
    // The drawer opens on Data since #433, which draws none of these selectors; the stub's long
    // Kode is on Om variabelen (Fhi.Metadata-r0w7c).
    apply: noBreak,
  },
  {
    assertion: 'no horizontal overflow',
    defect: 'the same value with no break point on the whole-variable page at 320px',
    path: '/', state: 'variable-page', width: 320,
    apply: noBreak,
  },
  {
    assertion: 'the component stays inside the box the host gave it',
    defect: 'an overflowing descendant becoming visible with its transparent ancestor',
    path: '/kilder', state: 'kilder-list', width: 1440,
    setup: page => page.evaluate(() => document.querySelector('.munin-explorer')
      .insertAdjacentHTML('beforeend', '<div id="geometry-transparent" style="position:relative;width:1px;height:1px;opacity:0"><span style="position:absolute;left:4000px;width:20px;height:20px">test</span></div>')),
    apply: page => page.locator('#geometry-transparent').evaluate(el => { el.style.opacity = '1'; }),
  },
  {
    assertion: 'the component stays inside the box the host gave it',
    defect: 'an overflowing descendant becoming visible with its visibility-hidden ancestor',
    path: '/kilder', state: 'kilder-list', width: 1440,
    setup: page => page.evaluate(() => document.querySelector('.munin-explorer')
      .insertAdjacentHTML('beforeend', '<div id="geometry-invisible" style="position:relative;width:1px;height:1px;visibility:hidden"><span style="position:absolute;left:4000px;width:20px;height:20px">test</span></div>')),
    apply: page => page.locator('#geometry-invisible').evaluate(el => { el.style.visibility = 'visible'; }),
  },
  {
    assertion: 'no horizontal overflow',
    defect: 'transparent content that still widens the document',
    path: '/kilder', state: 'kilder-list', width: 1440,
    setup: page => page.evaluate(() => document.querySelector('.munin-explorer')
      .insertAdjacentHTML('beforeend', '<div id="geometry-transparent-layout" style="width:1px;height:1px;opacity:0"></div>')),
    apply: page => page.locator('#geometry-transparent-layout').evaluate(el => { el.style.width = '4000px'; }),
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
    defect: 'a comma-joined rule naming [hidden] only in the branch that does not match',
    path: '/kilder', state: 'kilder-list', width: 843,
    // The branch that matches the panel does not name the attribute; the one that does matches
    // nothing. Read as a whole selector string this rule looks deliberate, and it is not.
    apply: css('.munin-explorer-filters__toggle { display: none !important; }\n' +
      '.munin-explorer-filters__facets, .munin-explorer-decoy[hidden] ' +
      '{ display: block !important; }'),
  },
  {
    assertion: 'hidden means hidden',
    defect: 'a rule naming [hidden] only inside :not(), the false exemption',
    path: '/kilder', state: 'kilder-list', width: 843,
    // The selector names the attribute and matches the panel, but through its parent's not being
    // hidden: nothing here was written about un-hiding the panel (Fhi.Metadata-vy6ah).
    apply: css('.munin-explorer-filters__toggle { display: none !important; }\n' +
      ':not([hidden]) > .munin-explorer-filters__facets { display: block !important; }'),
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
    assertion: "the kilder table's expand control is big enough to hit",
    defect: 'a toggle whose glyph is the whole of its size, as the literal "+" was',
    path: '/kilder', state: 'kilder-list', width: 1440,
    // Both halves, because either alone leaves the control over the minimum on one axis and the
    // assertion is meant to fail on either.
    apply: css('.munin-explorer-kilder__expand-icon { height: 8px !important; ' +
      'width: 8px !important; }\n' +
      '.munin-explorer-kilder__expand-toggle { padding: 0 !important; }'),
  },
  {
    assertion: "Runa's row disclosure is big enough to hit",
    defect: 'a name button squeezed below the minimum target, its glyph hidden as under Stiler 0.1.91',
    path: '/', state: 'explorer-tabs', width: 1440,
    // Hides the glyph and caps the text's line box, so nothing is left to give the button height.
    apply: css('button.munin-explorer-dataitem-main__name .icon { display: none !important; }\n' +
      'button.munin-explorer-dataitem-main__name, button.munin-explorer-dataitem-main__name * { ' +
      'line-height: 10px !important; font-size: 8px !important; padding: 0 !important; ' +
      'height: auto !important; min-height: 0 !important; }'),
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
    assertion: "the kilder table's counts are right-aligned in their column",
    defect: 'the Stiler rule gone, so the figures fall back to the cell default',
    // kilder-counts rather than kilder-list, so the break is applied to all three columns that
    // carry the class: Delkilder is hidden by default and that state is what draws it.
    path: '/kilder', state: 'kilder-counts', width: 1440,
    apply: css('.munin-explorer-kilder .munin-explorer-kilder__count ' +
      '{ text-align: left !important; }'),
  },
  {
    assertion: "the kilder table's counts are right-aligned in their column",
    defect: 'the same rule gone at the narrowest width the scan drives',
    path: '/kilder', state: 'kilder-list', width: 843,
    // The wide case above cannot stand for this one: 843 is the only width in the scan below
    // Stiler's grid, where the table sits in its own scroll box and every cell is measured in that
    // box's scrollable coordinates rather than the page's.
    apply: css('.munin-explorer-kilder .munin-explorer-kilder__count ' +
      '{ text-align: left !important; }'),
  },
  {
    assertion: "the detail page's main column is the wider part of its body",
    defect: "the reading column placed in the contents rail's track",
    path: '/', state: 'variable-whole', width: 1440,
    // The two tracks swapped, which is the arrangement every other assertion in the file stays
    // green through: the body is still two tracks and still one row, and only which child sits in
    // which has moved.
    apply: css('.munin-explorer-page__body ' +
      '{ grid-template-columns: minmax(0, 1fr) 320px !important; }\n' +
      '.munin-explorer-page__main { grid-column: 2 !important; }'),
  },
  {
    assertion: "the detail page's main column is the wider part of its body",
    defect: 'the reading column narrower than the rail it is stacked under',
    path: '/', state: 'variable-whole', width: 843,
    // The wide case above cannot stand for this one: it breaks the side-by-side comparison, and
    // 843 is below Stiler's second track, where the two are stacked and the other one is made.
    apply: css('.munin-explorer-page__main { width: 200px !important; }'),
  },
  {
    assertion: "the detail page's main column is the wider part of its body",
    defect: "the second track never declared, which is Stiler's rule going missing",
    path: '/', state: 'variable-whole', width: 1440,
    // `display: grid` with no template is one full-width track, the layout 843 renders correctly,
    // so this is the case the boxes alone cannot tell from the narrow page. Neither of the two
    // above removes a track: they move a child between tracks that are still declared.
    apply: css('.munin-explorer-page__body { grid-template-columns: none !important; }'),
  },
  {
    assertion: "the detail page's two columns share a row",
    defect: 'the contents rail dropped to a row of its own',
    path: '/', state: 'variable-whole', width: 1440,
    // Both tracks and both widths left where they were, so the width assertion stays green and
    // only the row this one is about has moved — the one break the width comparison cannot see.
    apply: css('.munin-explorer-page__toc ' +
      '{ grid-row: 2 !important; grid-column: 1 !important; }\n' +
      '.munin-explorer-page__main { grid-row: 1 !important; grid-column: 2 !important; }'),
  },
  {
    assertion: "the detail page's fact list has as many tracks as its container fits",
    defect: 'one column at 1280, where two fit: the fact list keyed to the viewport again',
    path: '/', state: 'variable-page', width: 1280,
    // 0.1.105's `auto` below its 1280 breakpoint, which drew one 927px track where two of 455.5px
    // fit. Measured on the whole-variable page reached by its click path (Fhi.Metadata-2w7fx).
    apply: css('.munin-explorer-page__fields { grid-template-columns: minmax(0, 1fr) !important; }'),
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
  browser = await chromium.launch(launchOptions());
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
    await installUnhiddenOnPurpose(context);
    const page = await context.newPage();
    try {
      await page.goto(url, { waitUntil: 'networkidle', timeout: 60_000 });
      await page.waitForTimeout(settleMs);
      await states[state](page);
      await scrollToTop(page);
      await page.waitForFunction(() => window.scrollX === 0 && window.scrollY === 0);
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
