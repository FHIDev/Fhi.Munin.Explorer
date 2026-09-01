// Scans the given targets with axe and exits 1 on a violation, 2 if it could not run.
// Playwright is used because it manages its own browser: the previous runner drove
// Chrome through selenium, and the runner's chromedriver drifted from its Chrome on a
// weekly cadence, so the gate failed for reasons that had nothing to do with the page.
//
// A target is a URL, or `URL::state` to drive the loaded page into a named state from
// `axe-states.mjs` first: what a reader reaches by pressing is invisible to the plain form.
import { chromium } from 'playwright';
import AxeBuilder from '@axe-core/playwright';
import { states } from './axe-states.mjs';

const targets = process.argv.slice(2);
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);
const tags = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

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

let browser;
try {
  browser = await chromium.launch();
} catch (err) {
  console.error('could not start a browser - this is a TOOLING failure, not a finding.');
  console.error(String(err?.message ?? err));
  process.exit(2);
}

let violationCount = 0;

try {
  for (const { url, state, label } of plan) {
    console.log(`\n==> axe ${label}`);
    const context = await browser.newContext();
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

    const results = await new AxeBuilder({ page }).withTags(tags).analyze();
    await page.close();
    await context.close();

    if (results.violations.length === 0) {
      console.log('    no violations');
      continue;
    }

    violationCount += results.violations.length;
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

process.exit(violationCount > 0 ? 1 : 0);
