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

const targets = process.argv.slice(2);
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);

// Six widths, and the last three are the ones that earn this file. Above the 1488px
// page-container cap, inside it, one pixel above Stiler's own `@media (max-width: 1280px)`
// breakpoint - and then 1280, 1024 and 843, which is where the layout actually stops fitting.
//
// 1280 AND 1024 USED TO BE MISSING, and the note here said why: below the breakpoint Stiler was
// turning the result row into `flex-direction: column` while leaving its per-column
// `flex: 210 1 0` weights in force, so every cell's flex basis became its height and collapsed to
// 0 (Fhi.Metadata-l9l2n.41). Re-measured against Stiler 0.1.38 on 2026-09-07: every assertion
// holds at both widths on both states, so the reset has shipped and the exclusion has outlived it.
//
// What the exclusion cost meanwhile is the point. Fhi.Metadata-b3brc is a page-wide horizontal
// scrollbar whose whole band is 1024 to 1225, and not one width of it was in the old default -
// 202px of overflow at 1024, and none at 1023, because Stiler's grid drops away entirely there. A
// suite that samples only the widths where a layout is comfortable reports success without
// having looked.
//
// 843 is the narrowest of the three and the only one below Stiler's grid: 779px of table
// min-content plus 48px of page air is 827, so 843 of viewport is the last width that fits and
// everything under it needs the table's own scroll box.
//
// 320 is missing, and it is the width WCAG 1.4.10 Reflow names. It is absent because nobody has
// measured this page there against pinned Stiler yet; Fhi.Metadata-hxtir is where that happens.
const widths = (process.env.GEOMETRY_WIDTHS ?? '1689,1440,1281,1280,1024,843')
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

// A pin scoped to a state name that no longer exists would go quietly inapplicable everywhere and
// never be run again — the same false green as measuring nothing and reporting success.
for (const { name, states: appliesTo } of assertions) {
  for (const scoped of appliesTo ?? []) {
    if (Object.hasOwn(states, scoped)) continue;
    console.error(`assertion "${name}" is scoped to unknown state "${scoped}" - TOOLING failure.`);
    console.error(`known states: ${Object.keys(states).join(', ')}`);
    process.exit(2);
  }
}

let browser;
try {
  browser = await chromium.launch(launchOptions());
} catch (err) {
  console.error('could not start a browser - this is a TOOLING failure, not a finding.');
  console.error(String(err?.message ?? err));
  process.exit(2);
}

let failures = 0;
let inapplicable = 0;

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

      for (const { name, kind, states: appliesTo, body } of assertions) {
        // Printed, never skipped silently. A pin whose defect cannot occur here is not a pass,
        // and a run that reported it as one would be the thing this suite exists to prevent.
        if (appliesTo !== undefined && !appliesTo.includes(state)) {
          inapplicable += 1;
          console.log(`    n/a  [${kind}] ${name}`);
          console.log(`         only measured in ${appliesTo.join(', ')}; ` +
            `this is ${state === null ? 'no state' : state}`);
          continue;
        }

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
const notRun = inapplicable === 0 ? '' : `, and ${inapplicable} did not apply`;
if (failures > 0) {
  console.log(`${failures} geometry assertion(s) failed${notRun}.`);
} else {
  console.log(`every geometry assertion that applies held${notRun}.`);
}

process.exit(failures > 0 ? 1 : 0);
