// The hidden toolbar must stay out of layout and the Tab order even under host CSS.
// Runs with and without the optional module; disabling all JavaScript would also disable Blazor.
// Exits 1 for a defect, 2 for tooling, 3 if a negative control cannot prove detection.
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import { launchOptions } from './browser-launch.mjs';
import { states } from './axe-states.mjs';
import { scrollPast, scrollToTop } from './reader-scroll.mjs';

const base = process.argv[2];
if (!base) {
  console.error('usage: node sticky-toolbar-scan.mjs <host-base-url>');
  process.exit(2);
}

const targets = [
  ['/kilder', 'kilde-datasamling'],
  ['/kilder', 'kilde-drilldown'],
  ['/', 'variable-whole'],
];
const selector = '.munin-explorer-page__stuckbar';
let status = 0;
let browser;

async function hiddenState(bar) {
  return bar.evaluate(element => ({
    hidden: element.hidden,
    ariaHidden: element.getAttribute('aria-hidden'),
    display: getComputedStyle(element).display,
    boxes: element.getClientRects().length,
  }));
}

async function assertHidden(bar) {
  assert.deepEqual(await hiddenState(bar), {
    hidden: true, ariaHidden: 'true', display: 'none', boxes: 0,
  }, 'a hidden toolbar must generate no boxes');
}

// Adjacent stops bound the walk: landing after the bar proves its action was skipped.
// The same walk must reach the action when shown, and when the hiding rule is broken.
async function plantStops(bar) {
  await bar.evaluate(element => {
    for (const position of ['before', 'after']) {
      const stop = document.createElement('button');
      stop.id = `toolbar-${position}`;
      stop.textContent = position;
      element[position](stop);
    }
  });
}

async function nextStop(page) {
  await page.locator('#toolbar-before').evaluate(element => element.focus({ preventScroll: true }));
  await page.keyboard.press('Tab');
  return page.evaluate(() => ({
    after: document.activeElement.id === 'toolbar-after',
    inBar: !!document.activeElement.closest('.munin-explorer-page__stuckbar'),
  }));
}

async function checkTransitions(page, bar) {
  const hero = page.locator('.munin-explorer-page__facts').first();
  await scrollPast(page, await hero.getAttribute('id'));
  await page.waitForFunction(selector => {
    const bar = document.querySelector(selector);
    return !bar.hidden && bar.getAttribute('aria-hidden') === 'false';
  }, selector);
  await bar.locator('.munin-explorer-page__stuckbar-inner').waitFor({ state: 'visible' });
  const action = bar.locator('a,button').first();
  assert.equal(await action.isVisible(), true, 'the shown action must be visible');
  assert.equal((await nextStop(page)).inBar, true, 'Tab must reach the shown toolbar action');
  await scrollToTop(page);
  assert.equal(await action.evaluate(element => document.activeElement === element), true,
    'returning to the hero must preserve action focus');
  assert.equal((await hiddenState(bar)).hidden, false, 'the focused bar must remain shown');
  await page.keyboard.press('Tab');
  await page.waitForFunction(selector => document.querySelector(selector).hidden, selector);
  await assertHidden(bar);
}

try {
  browser = await chromium.launch(launchOptions());
  for (const moduleAvailable of [true, false]) {
    for (const [path, state] of targets) {
      const context = await browser.newContext({
        viewport: { width: 1689, height: 700 }, reducedMotion: 'reduce',
      });
      const page = await context.newPage();
      page.setDefaultTimeout(15_000);
      let blocked = 0;
      let control = false;
      if (!moduleAvailable) {
        await page.route('**/explorer-interop.js*', route => {
          blocked += 1;
          return route.abort();
        });
      }
      try {
        await page.goto(new URL(path, base).href, { waitUntil: 'networkidle' });
        await states[state](page);
        const bar = page.locator(selector).first();
        await bar.waitFor({ state: 'attached' });
        await scrollToTop(page);
        if (!moduleAvailable) assert.ok(blocked > 0, 'the optional module request must be blocked');
        for (const width of [1689, 320]) {
          await page.setViewportSize({ width, height: 700 });
          await scrollToTop(page);
          await assertHidden(bar);
        }
        await page.setViewportSize({ width: 1689, height: 700 });
        await scrollToTop(page);
        if (state === 'kilde-datasamling') {
          assert.equal(await bar.locator('a').count(), 1, 'the fixture must have a compact action');
          await plantStops(bar);
          assert.deepEqual(await nextStop(page), { after: true, inBar: false },
            'Tab must skip the hidden compact action');
          if (moduleAvailable) await checkTransitions(page, bar);
          else {
            const hero = page.locator('.munin-explorer-page__facts').first();
            await scrollPast(page, await hero.getAttribute('id'));
            await assertHidden(bar);
            await scrollToTop(page);
          }
        }

        // Recreate the cascade defect; a check that accepts it has measured nothing.
        control = true;
        const brokenStyle = await page.addStyleTag({ content: `${selector}[hidden] { display: block !important; }` });
        const broken = await hiddenState(bar);
        assert.notEqual(broken.display, 'none', 'negative control must override the hidden rule');
        await assert.rejects(() => assertHidden(bar), { name: 'AssertionError' });
        if (state === 'kilde-datasamling') {
          assert.equal((await nextStop(page)).inBar, true,
            'the negative control must expose the hidden action to Tab');
          await page.locator('#toolbar-after').focus();
        }
        await brokenStyle.evaluate(element => element.remove());
        await scrollToTop(page);
        await assertHidden(bar);
        control = false;
        console.log(`PASS ${state}: 1689px and 320px, module ${moduleAvailable ? 'loaded' : 'blocked'}, negative control detected`);
      } catch (error) {
        status = Math.max(status, control ? 3 : error.code === 'ERR_ASSERTION' ? 1 : 2);
        console.error(`FAIL ${state}, module ${moduleAvailable ? 'loaded' : 'blocked'}: ${error.stack}`);
      } finally {
        await context.close();
      }
    }
  }
} catch (error) {
  console.error(`sticky toolbar scan could not run: ${error.stack}`);
  process.exitCode = 2;
} finally {
  await browser?.close();
}
process.exitCode ||= status;
