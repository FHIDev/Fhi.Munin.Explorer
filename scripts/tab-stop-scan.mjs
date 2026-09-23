// Walks each target with real Tab presses and fails on any stop a reader cannot see: one inside a
// [hidden] subtree, or one whose box has no width or no height. Exits 0 held, 1 a stop failed,
// 2 could not run (TOOLING), 3 the walk did not see the control it plants - read as unmeasured.
//
// Same shape as geometry-scan.mjs beside it: `url::state` targets, states from axe-states.mjs,
// the same Playwright. What differs is that it reads document.activeElement after a key press and
// never calls .focus() on a stop: a Tab order is the browser's answer, not the markup's.
//
// WHY ONLY ON HOSTILEHOST. The browser's own `[hidden] { display: none }` takes a hidden panel out
// of the Tab order by itself; Stiler's bare `div { display: block }` beats it and puts it back,
// 0px tall. So the defect this exists for cannot occur on a host without Stiler, and neither can
// the planted control fire there (Fhi.Metadata-w8sms).
import { states } from './axe-states.mjs';
import { scrollToTop } from './reader-scroll.mjs';

const launchOptions = () => {
  const channel = process.env.PLAYWRIGHT_BROWSER_CHANNEL;
  return channel ? { channel } : {};
};

const targets = process.argv.slice(2);
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);

// 1440 is the width the defect was found at, and the one axe on this host uses for the same
// reason: below Stiler's 1280px breakpoint the result rows are laid out differently.
const widths = (process.env.TAB_STOP_WIDTHS ?? '1440,843')
  .split(',')
  .map(w => Number(w.trim()))
  .filter(w => Number.isInteger(w) && w > 0);

// Far more stops than any state here has; reaching it means focus is cycling inside the page.
const maxPresses = 400;

if (targets.length === 0 || widths.length === 0) {
  console.error('usage: node tab-stop-scan.mjs <url|url::state> [...]');
  console.error(`known states: ${Object.keys(states).join(', ')}`);
  process.exit(2);
}

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

let chromium;
try {
  ({ chromium } = await import('playwright'));
} catch (err) {
  console.error('could not import playwright - this is a TOOLING failure, not a finding.');
  console.error(String(err?.message ?? err));
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

const START = 'munin-tab-stop-start';
const CONTROL = 'munin-tab-stop-control';

// The walk starts from a tabindex="-1" anchor at the top of <body>: the sequential focus starting
// point, never a stop itself. The control is the defect in one element - a hidden div that still
// carries tabindex="0" - last in the document, so a walk that does not report it saw nothing.
const plant = ({ start, control }) => {
  window.scrollTo(0, 0);
  const anchor = document.createElement('span');
  anchor.id = start;
  anchor.tabIndex = -1;
  document.body.prepend(anchor);

  const planted = document.createElement('div');
  planted.id = control;
  planted.hidden = true;
  planted.tabIndex = 0;
  document.body.append(planted);

  anchor.focus();
  window.scrollTo(0, 0);
};

const readStop = ({ start }) => {
  const el = document.activeElement;
  if (el === null || el === document.body || el === document.documentElement || el.id === start) {
    return null;
  }

  const again = el.hasAttribute('data-munin-tab-stop');
  el.setAttribute('data-munin-tab-stop', '');

  const box = el.getBoundingClientRect();
  const hiddenAncestors = [];
  for (let node = el.closest('[hidden]'); node !== null; node = node.parentElement?.closest('[hidden]') ?? null) {
    hiddenAncestors.push(node);
  }
  const hiddenAncestor = hiddenAncestors.find(node => !unhiddenOnPurpose(node)) ?? null;
  const onPurpose = hiddenAncestors.find(node => unhiddenOnPurpose(node)) ?? null;
  const describe = node => node.tagName.toLowerCase()
    + (node.id ? `#${node.id}` : '')
    + (node.getAttribute('role') ? `[role=${node.getAttribute('role')}]` : '')
    + (node.getAttribute('class') ? `.${node.getAttribute('class').trim().split(/\s+/).join('.')}` : '');
  const name = (el.getAttribute('aria-label') || el.innerText || el.value || '')
    .replace(/\s+/g, ' ').trim().slice(0, 60);

  return {
    again,
    id: el.id,
    what: `${describe(el)}${name ? ` "${name}"` : ''}`,
    box: `${Math.round(box.width)}x${Math.round(box.height)}`,
    empty: box.width === 0 || box.height === 0,
    hiddenBy: hiddenAncestor === null ? null : hiddenAncestor === el ? 'itself' : describe(hiddenAncestor),
    unhiddenBy: onPurpose === null ? null : describe(onPurpose),
  };

  // The same test as geometry-assertions.mjs's 'hidden means hidden', and it has to stay the same:
  // a host un-hides on purpose with a rule whose own selector names [hidden], and the control that
  // folds it then has no box. Stiler does this to the facet panel at 1024px and up. Change both.
  function unhiddenOnPurpose(node) {
    for (const control of document.querySelectorAll('[aria-controls]')) {
      if (!node.id || !(control.getAttribute('aria-controls') ?? '').split(/\s+/).includes(node.id)) continue;
      const c = control.getBoundingClientRect();
      if (c.width * c.height !== 0) return false;
    }
    for (const rule of applicableRules()) {
      const display = rule.style.getPropertyValue('display');
      if (display === '' || display === 'none') continue;
      for (const branch of topLevelBranches(rule.selectorText)) {
        if (!branch.includes('[hidden]')) continue;
        try {
          if (node.matches(branch)) return true;
        } catch {
          // A selector this browser cannot parse tells us nothing either way.
        }
      }
    }
    return false;
  }

  function topLevelBranches(selectorText) {
    const branches = [];
    let depth = 0;
    let quote = null;
    let start = 0;
    for (let i = 0; i < selectorText.length; i += 1) {
      const ch = selectorText[i];
      if (quote !== null) {
        if (ch === '\\') i += 1;
        else if (ch === quote) quote = null;
      } else if (ch === '"' || ch === "'") {
        quote = ch;
      } else if (ch === '(' || ch === '[') {
        depth += 1;
      } else if (ch === ')' || ch === ']') {
        depth -= 1;
      } else if (ch === ',' && depth === 0) {
        branches.push(selectorText.slice(start, i));
        start = i + 1;
      }
    }
    branches.push(selectorText.slice(start));
    return branches;
  }

  function applicableRules() {
    const found = [];
    const inForce = rule => rule.media ? matchMedia(rule.conditionText).matches
      : rule.conditionText ? CSS.supports(rule.conditionText) : true;
    const walk = rules => {
      for (const rule of rules) {
        if (rule.selectorText && rule.style) found.push(rule);
        else if (rule.cssRules && inForce(rule)) walk([...rule.cssRules]);
      }
    };
    walk([...document.styleSheets].flatMap(sheet => {
      try { return [...sheet.cssRules]; } catch { return []; }
    }));
    return found;
  }
};

let failures = 0;
let unmeasured = 0;

try {
  for (const { url, state, label } of plan) {
    for (const width of widths) {
      console.log(`\n==> tab stops ${label} at ${width}px`);
      const context = await browser.newContext({ viewport: { width, height: 900 } });
      const page = await context.newPage();

      try {
        await page.goto(url, { waitUntil: 'networkidle', timeout: 60_000 });
      } catch (err) {
        console.error(`could not load ${url} - TOOLING failure.`);
        console.error(String(err?.message ?? err));
        await browser.close();
        process.exit(2);
      }

      await page.waitForTimeout(settleMs);

      if (state !== null) {
        try {
          await states[state](page);
        } catch (err) {
          console.error(`could not reach state "${state}" on ${url} - TOOLING failure.`);
          console.error(String(err?.message ?? err));
          await browser.close();
          process.exit(2);
        }
        await scrollToTop(page);
      }

      await page.evaluate(plant, { start: START, control: CONTROL });

      const stops = [];
      let sawControl = false;
      let ended = false;
      for (let press = 0; press < maxPresses; press++) {
        await page.keyboard.press('Tab');
        const stop = await page.evaluate(readStop, { start: START });

        if (stop === null) {
          if (stops.length > 0) { ended = true; break; }
          continue;
        }
        if (stop.id === CONTROL) {
          sawControl = true;
          continue;
        }
        // A stop seen before: the order wrapped round without passing through the browser chrome.
        if (stop.again) { ended = true; break; }
        stops.push(stop);
      }

      await context.close();

      if (!ended) {
        console.error(`focus never left the page in ${maxPresses} presses - TOOLING failure.`);
        await browser.close();
        process.exit(2);
      }

      const bad = stops.filter(stop => stop.hiddenBy !== null || stop.empty);
      console.log(`    ${stops.length} stops`);
      for (const stop of bad) {
        failures += 1;
        const why = [
          stop.hiddenBy === null ? null
            : stop.hiddenBy === 'itself' ? 'carries [hidden]' : `inside hidden ${stop.hiddenBy}`,
          stop.empty ? `a ${stop.box} box` : null,
        ].filter(Boolean).join(', ');
        console.log(`    FAIL stop ${stops.indexOf(stop) + 1}: ${stop.what} - ${why}`);
      }
      if (bad.length === 0) {
        console.log('    ok   no stop is hidden or has an empty box');
      }
      // Said, not skipped silently: these stops are inside [hidden] and were let through.
      const exempt = new Map();
      for (const { unhiddenBy } of stops) {
        if (unhiddenBy !== null) exempt.set(unhiddenBy, (exempt.get(unhiddenBy) ?? 0) + 1);
      }
      for (const [by, count] of exempt) {
        console.log(`    note ${count} stop(s) inside ${by}, which a [hidden] rule un-hides on purpose`);
      }

      if (!sawControl) {
        unmeasured += 1;
        console.log('    UNMEASURED the planted hidden tabindex="0" div was never a stop, so a hidden');
        console.log('         panel would not have been either - is Stiler\'s div rule on this page?');
      }
    }
  }
} catch (err) {
  console.error('the walk did not complete - this is a TOOLING failure, not a finding.');
  console.error(String(err?.stack ?? err));
  await browser.close();
  process.exit(2);
} finally {
  await browser.close().catch(() => {});
}

console.log('');
if (unmeasured > 0) {
  console.log(`${unmeasured} walk(s) did not reach the planted control; read the result above as unmeasured.`);
  process.exit(3);
}
if (failures > 0) {
  console.log(`${failures} Tab stop(s) a reader cannot see.`);
  process.exit(1);
}
console.log('every Tab stop walked has a box, and none is inside a [hidden] subtree that nothing un-hides on purpose.');
process.exit(0);
