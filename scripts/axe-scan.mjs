// Scans the given URLs with axe and exits 1 on a violation, 2 if it could not run.
// Playwright is used because it manages its own browser: the previous runner drove
// Chrome through selenium, and the runner's chromedriver drifted from its Chrome on a
// weekly cadence, so the gate failed for reasons that had nothing to do with the page.
import { chromium } from 'playwright';
import AxeBuilder from '@axe-core/playwright';

const urls = process.argv.slice(2);
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);
const tags = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

if (urls.length === 0) {
  console.error('usage: node axe-scan.mjs <url> [url...]');
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

let violationCount = 0;

try {
  for (const url of urls) {
    console.log(`\n==> axe ${url}`);
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
