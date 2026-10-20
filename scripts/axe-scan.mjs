// Scans the given targets with axe and exits 1 on a violation, 2 if it could not run.
// Playwright is used because it manages its own browser: the previous runner drove
// Chrome through selenium, and the runner's chromedriver drifted from its Chrome on a
// weekly cadence, so the gate failed for reasons that had nothing to do with the page.
//
// A target is a URL, or `URL::state` to drive the loaded page into a named state from
// `axe-states.mjs` first: what a reader reaches by pressing is invisible to the plain form.
import { readFileSync } from 'node:fs';
import { chromium } from 'playwright';

import { launchOptions } from './browser-launch.mjs';
import AxeBuilder from '@axe-core/playwright';
import { states } from './axe-states.mjs';
import { describeShard, ownsItem, parseShard } from './scan-shard.mjs';

const targets = process.argv.slice(2);
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);
// WCAG 2.2 AA, the level helsedata's accessibility statement is written against (Fhi.Metadata-mhqx0).
const tags = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

// Narrower widths each state is scanned at again once reached, the window resized in place: a phone
// draws other markup (cards, a folded panel) from the same state, and a reload per width would triple
// the job. Unset, one scan at the viewport above.
const extraWidths = (process.env.AXE_EXTRA_WIDTHS ?? '').split(',').map(Number).filter(w => w > 0);

// Findings a bead owns, from the file AXE_KNOWN_VIOLATIONS names; its header says how a line is used.
// Only the sample host's scan names one: its lines describe that host's stylesheet, not helsedata's.
const known = (process.env.AXE_KNOWN_VIOLATIONS ? readFileSync(process.env.AXE_KNOWN_VIOLATIONS, 'utf8') : '')
  .split('\n')
  .filter(line => line.trim() !== '' && !line.startsWith('#'))
  .map(line => {
    const [rule, state, width, selector, bead] = line.split('|').map(part => part.trim());
    return { line, rule, state, width: width === '' ? null : Number(width), selector, bead, seen: false };
  });
const scanned = new Set();

// Playwright's own default is 1280x720, which is what every caller got before this existed and is
// what a caller that sets nothing still gets. AXE_VIEWPORT_WIDTH is for the one caller that cannot
// use it: samples/HostileHost renders against helsedata's real stylesheet, whose
// `@media (max-width: 1280px)` collapses every result row to zero height (Fhi.Metadata-l9l2n.41),
// so at the default the rows are never visible and the scan stops as a TOOLING failure before it
// judges anything.
const viewport = process.env.AXE_VIEWPORT_WIDTH
  ? {
      width: Number(process.env.AXE_VIEWPORT_WIDTH),
      height: Number(process.env.AXE_VIEWPORT_HEIGHT ?? 900),
    }
  : null;

if (targets.length === 0) {
  console.error('usage: node axe-scan.mjs <url|url::state> [...]');
  console.error(`known states: ${Object.keys(states).join(', ')}`);
  process.exit(2);
}

// Parsed before a browser starts, so a typo in a state name is a message rather than a run that
// scans the default state and reports it under the name of one it never entered.
const plan = [];
for (const target of targets) {
  const separator = target.indexOf('::');
  const url = separator < 0 ? target : target.slice(0, separator);
  const state = separator < 0 ? null : target.slice(separator + 2);

  if (state !== null && !Object.hasOwn(states, state)) {
    console.error(`unknown state "${state}" - TOOLING failure.`);
    console.error(`known states: ${Object.keys(states).join(', ')}`);
    process.exit(2);
  }

  plan.push({ url, state, label: state === null ? url : `${url} [${state}]` });
}

// A CI shard scans every n-th page state; unset, all of them.
let shard;
try {
  shard = parseShard();
} catch (err) {
  console.error(`${err.message} - TOOLING failure.`);
  process.exit(2);
}
const owned = plan.filter((_, i) => ownsItem(i, shard));
if (owned.length === 0) {
  console.error('this shard has no page state to scan - TOOLING failure: fewer states than shards.');
  process.exit(2);
}
const shardNote = describeShard(owned.length, plan.length, shard);
if (shardNote) console.log(`${shardNote} page states.`);

let browser;
try {
  browser = await chromium.launch(launchOptions());
} catch (err) {
  console.error('could not start a browser - this is a TOOLING failure, not a finding.');
  console.error(String(err?.message ?? err));
  process.exit(2);
}

let violationCount = 0;

try {
  for (const { url, state, label } of owned) {
    console.log(`\n==> axe ${label}`);
    // Stiler fades the sticky fact bar in over 0.15s; scanned mid-fade its text measures as low
    // contrast. Reduced motion drops the fade, so the bar is either there or not (Fhi.Metadata-hiz1k).
    const context = await browser.newContext({ ...(viewport ? { viewport } : {}), reducedMotion: 'reduce' });
    const page = await context.newPage();

    try {
      await page.goto(url, { waitUntil: 'networkidle', timeout: 60_000 });
    } catch (err) {
      console.error(`could not load ${url} - TOOLING failure.`);
      console.error(String(err?.message ?? err));
      await context.close();
      await browser.close();
      process.exit(2);
    }

    // Blazor Server paints a shell first and fills it over the circuit, so scanning
    // immediately reads an empty page and passes for the wrong reason.
    await page.waitForTimeout(settleMs);

    if (state !== null) {
      // A state that cannot be entered is the scanner failing, not the page: scanning the default
      // state under this state's name is exactly the false green this whole form exists to end.
      try {
        await states[state](page);
      } catch (err) {
        console.error(`could not reach state "${state}" on ${url} - TOOLING failure.`);
        console.error(String(err?.message ?? err));
        await context.close();
        await browser.close();
        process.exit(2);
      }
    }

    for (const width of [null, ...extraWidths]) {
      const at = width === null ? label : `${label} @${width}px`;
      if (width !== null) {
        console.log(`\n==> axe ${at}`);
        await page.setViewportSize({ width, height: viewport?.height ?? 900 });
        await page.waitForTimeout(500);
      }

      const results = await new AxeBuilder({ page }).withTags(tags).analyze();
      scanned.add(`${state}|${width}`);

      // A node a known line names is reported and not counted; a violation left with no node goes.
      for (const v of results.violations) {
        v.nodes = v.nodes.filter(node => {
          const entry = known.find(k => k.rule === v.id && k.state === state && k.width === width
            && node.target.join(' ').includes(k.selector));
          if (!entry) return true;
          entry.seen = true;
          console.log(`    known  ${v.id} ${node.target.join(' ')} (${entry.bead})`);
          return false;
        });
      }
      results.violations = results.violations.filter(v => v.nodes.length > 0);

      // The host's <html lang> need not follow the reader — helsedata's /en/ pages say "no" — so
      // the explorer root states its own, and axe's language rules must have judged it (Fhi.Metadata-nh7z2).
      const roots = await page.evaluate(() =>
        [...document.querySelectorAll('[data-munin-explorer-version]')].map((root) => ({
          lang: root.getAttribute('lang'),
          norwegian: root.querySelectorAll('[lang="no"], [lang="nb"]').length,
        })));
      const judged = [...results.passes, ...results.violations].map((r) => r.id);
      const unjudged = ['html-has-lang', 'valid-lang'].filter((id) => !judged.includes(id));

      if (unjudged.length > 0) {
        console.error(`axe never evaluated ${unjudged.join(', ')} on ${at} - TOOLING failure.`);
        await browser.close();
        process.exit(2);
      }

      if (roots.length === 0) {
        console.error(`no explorer root on ${at} to check the lang of - TOOLING failure.`);
        await browser.close();
        process.exit(2);
      }

      let pageViolations = results.violations.length;
      for (const root of roots) {
        console.log(`    explorer root lang="${root.lang ?? ''}", ${root.norwegian} element(s) marked Norwegian inside it`);
        if (root.lang !== 'no' && root.lang !== 'en') {
          pageViolations += 1;
          console.log(`\n  [serious] explorer-root-lang — the explorer root carries lang="${root.lang ?? ''}", not "no" or "en"`);
        }
      }

      violationCount += pageViolations;
      if (pageViolations === 0) {
        console.log('    no violations');
        continue;
      }

      for (const v of results.violations) {
        console.log(`\n  [${v.impact ?? 'unknown'}] ${v.id} — ${v.help}`);
        console.log(`  ${v.helpUrl}`);
        for (const node of v.nodes.slice(0, 5)) {
          console.log(`    ${node.target.join(' ')}`);
          const detail = (node.failureSummary ?? '').split('\n').filter(Boolean)[0];
          if (detail) console.log(`      ${detail}`);
        }
        if (v.nodes.length > 5) {
          console.log(`    ...and ${v.nodes.length - 5} more`);
        }
      }
    }

    await page.close();
    await context.close();
  }
} catch (err) {
  // Anything reaching here is the scanner failing, not the page. Exit 1 would be read
  // as "violations found" by the caller and send someone hunting a defect that is not
  // there.
  console.error('the scan did not complete - this is a TOOLING failure, not a finding.');
  console.error(String(err?.stack ?? err));
  await browser.close();
  process.exit(2);
} finally {
  await browser.close().catch(() => {});
}

// A line for a state and width this run scanned that matched nothing describes a fixed finding.
const stale = known.filter(k => !k.seen && scanned.has(`${k.state}|${k.width}`));
for (const k of stale) {
  console.log(`\n  stale line in test/axe-known-violations.txt, delete it: ${k.line}`);
}

process.exit(violationCount > 0 || stale.length > 0 ? 1 : 0);
