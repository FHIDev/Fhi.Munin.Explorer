// Measures the assertions in geometry-assertions.mjs against the given targets, at several
// viewport widths, and exits 1 on a failure or 2 if it could not run.
//
// Deliberately the same shape as axe-scan.mjs beside it — same target syntax, same states from
// axe-states.mjs, same Playwright, same 0/1/2 exit contract — so the two run from one script and a
// reader who knows one knows the other. What differs is what it asks: axe judges the accessibility
// tree, this judges boxes. Both were needed on 2026-09-03 and only one of them was there.
//
// A target is a URL, or `URL::state` to drive the loaded page into a named state from
// axe-states.mjs first. A width is only interesting because layout changes with it: Stiler's
// `.munin-explorer` grid switches on at 1024px, so a page that fits at 1689 can overflow at 1024.
import { chromium } from 'playwright';
import { states } from './axe-states.mjs';
import { assertions, selectors } from './geometry-assertions.mjs';

const targets = process.argv.slice(2);
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);

// Three widths across the band in which Stiler's desktop result table applies: above the 1488px
// page-container cap, inside it, and one pixel above Stiler's own `@media (max-width: 1280px)`
// breakpoint, which is the narrowest width the desktop layout is used at.
//
// 1280 AND BELOW ARE DELIBERATELY NOT IN THE DEFAULT, and that is a live defect rather than a
// choice about coverage. Below the breakpoint Stiler turns the result row into
// `flex-direction: column` while leaving its per-column `flex: 210 1 0` weights in force, so every
// cell's flex basis becomes its height and collapses to 0: names, codes and dates all in the DOM
// and none of them drawn. This fixture found that on its first run, the "text a reader is meant to
// see has a box" invariant caught it, and the fix is in Fhi.Helsedata.Stiler rather than here
// (Fhi.Metadata-l9l2n.41). Reproduce it with GEOMETRY_WIDTHS=1280, and put 1280 and 1024 back in
// this default the day Stiler ships the reset.
const widths = (process.env.GEOMETRY_WIDTHS ?? '1689,1440,1281')
  .split(',')
  .map(w => Number(w.trim()))
  .filter(w => Number.isInteger(w) && w > 0);

if (targets.length === 0 || widths.length === 0) {
  console.error('usage: node geometry-scan.mjs <url|url::state> [...]');
  console.error(`known states: ${Object.keys(states).join(', ')}`);
  process.exit(2);
}

// Parsed before a browser starts, so a typo in a state name is a message rather than a run that
// measures the default state and reports it under the name of one it never entered.
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

let browser;
try {
  browser = await chromium.launch();
} catch (err) {
  console.error('could not start a browser - this is a TOOLING failure, not a finding.');
  console.error(String(err?.message ?? err));
  process.exit(2);
}

let failures = 0;

try {
  for (const { url, state, label } of plan) {
    for (const width of widths) {
      console.log(`\n==> geometry ${label} at ${width}px`);
      const context = await browser.newContext({ viewport: { width, height: 900 } });
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

      // Blazor Server paints a shell first and fills it over the circuit, so measuring
      // immediately measures an empty page and passes for the wrong reason.
      await page.waitForTimeout(settleMs);

      if (state !== null) {
        // A state that cannot be entered is the scanner failing, not the page: measuring the
        // default state under this state's name is exactly the false green the form exists to end.
        try {
          await states[state](page);
        } catch (err) {
          console.error(`could not reach state "${state}" on ${url} - TOOLING failure.`);
          console.error(String(err?.message ?? err));
          await context.close();
          await browser.close();
          process.exit(2);
        }
        // Entering a state can click something that scrolls. The header is `position: absolute`
        // at top 0, so "is this control under the header" is a question about scroll offset 0 and
        // means nothing anywhere else.
        await page.evaluate(() => window.scrollTo(0, 0));
        await page.waitForTimeout(250);
      }

      for (const { name, kind, body } of assertions) {
        let finding;
        try {
          // One argument, always: page.evaluate takes exactly one, and every body destructures
          // the selectors it needs out of it.
          finding = await page.evaluate(body, selectors);
        } catch (err) {
          // An assertion that throws is this file being wrong, not the page. Reporting it as a
          // finding would send someone hunting a defect that is not there.
          console.error(`assertion "${name}" threw - TOOLING failure.`);
          console.error(String(err?.message ?? err));
          await context.close();
          await browser.close();
          process.exit(2);
        }

        if (finding === null) {
          console.log(`    ok   [${kind}] ${name}`);
          continue;
        }

        failures += 1;
        console.log(`    FAIL [${kind}] ${name}`);
        console.log(`         ${finding}`);
      }

      await page.close();
      await context.close();
    }
  }
} catch (err) {
  // Anything reaching here is the scanner failing, not the page. Exit 1 would be read as
  // "the page is wrong" by the caller.
  console.error('the scan did not complete - this is a TOOLING failure, not a finding.');
  console.error(String(err?.stack ?? err));
  await browser.close();
  process.exit(2);
} finally {
  await browser.close().catch(() => {});
}

console.log('');
if (failures > 0) {
  console.log(`${failures} geometry assertion(s) failed.`);
} else {
  console.log('every geometry assertion held.');
}

process.exit(failures > 0 ? 1 : 0);
