// Measures the assertions in state-assertions.mjs against the given targets, and exits 1 on a
// failure or 2 if it could not run.
//
// Deliberately the same shape as geometry-scan.mjs beside it — same `url::state` target syntax,
// same states from axe-states.mjs, same Playwright, same 0/1/2 exit contract — so a reader who
// knows one knows the other. What differs is what it asks: geometry judges boxes, this judges
// whether a control's own state still agrees with what the component drew after a press it refused.
//
// Two things it does that geometry does not, both because these assertions PRESS things:
//
//   - a fresh page per assertion. A geometry assertion reads and can share one load; staging a
//     refusal hides six columns or ticks a facet value, so a second assertion on the same page
//     would be measuring the first one's leftovers.
//   - the negative control is inline rather than a run of its own. The page is already staged, and
//     the defect being replayed is one line of DOM: leave the browser's own flip standing, exactly
//     as a missing SetUpdatesAttributeName("checked") would. An assertion whose measure holds
//     against that is not passing, it is absent, and this reports it as a TOOLING failure.
//
// No widths: nothing here is about layout, so a second viewport would be a second copy of the same
// answer at twice the wall clock.
import { chromium } from 'playwright';

// PLAYWRIGHT_BROWSER_CHANNEL=msedge runs an installed browser instead of the bundled chromium, on
// the same terms as the sibling scans: `playwright install chromium` cannot complete on Node 26,
// the pinned fetcher calling fs.rmdir(recursive) which that version removed (Fhi.Metadata-2nfvm).
const launchOptions = () => {
  const channel = process.env.PLAYWRIGHT_BROWSER_CHANNEL;
  return channel ? { channel } : {};
};
import { states } from './axe-states.mjs';
import { assertions } from './state-assertions.mjs';

const targets = process.argv.slice(2);
const settleMs = Number(process.env.ACCESSIBILITY_SETTLE_MS ?? 4000);

// Where the stub API answers, so an assertion can hold a fetch open the way the browser cannot: the
// request the component makes is the HOST's, over the circuit, and page.route never sees it.
const stubBase = process.env.STATE_STUB_BASE;

if (targets.length === 0) {
  console.error('usage: node state-scan.mjs <url|url::state> [...]');
  console.error(`known states: ${Object.keys(states).join(', ')}`);
  process.exit(2);
}

if (stubBase === undefined) {
  console.error('STATE_STUB_BASE is unset - TOOLING failure.');
  console.error('it names the stub API this scan holds a fetch open at; see check-component-state.sh.');
  process.exit(2);
}

const stub = {
  async hold(path, ms) {
    const query = `path=${encodeURIComponent(path)}&ms=${ms}`;
    const response = await fetch(`${stubBase}/__stub/hold-next?${query}`, { method: 'POST' });
    if (!response.ok) {
      throw new Error(`the stub would not hold ${path}: ${response.status}`);
    }
  },
  async holding() {
    const response = await fetch(`${stubBase}/__stub/hold-next`);
    if (!response.ok) {
      throw new Error(`the stub would not say what it is holding: ${response.status}`);
    }

    return (await response.json()).held;
  },
};

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

// An assertion scoped to a state name that no longer exists would go quietly inapplicable
// everywhere and never be run again — the same false green as measuring nothing and passing.
for (const { name, states: appliesTo } of assertions) {
  for (const scoped of appliesTo ?? []) {
    if (Object.hasOwn(states, scoped)) continue;
    console.error(`assertion "${name}" is scoped to unknown state "${scoped}" - TOOLING failure.`);
    console.error(`known states: ${Object.keys(states).join(', ')}`);
    process.exit(2);
  }
}

// Before a browser starts, and before anything is held: a stub that is not answering would
// otherwise surface as an assertion failing to stage, which reads as the page being wrong.
try {
  const pending = await stub.holding();
  if (pending.length > 0) {
    console.error(`the stub is already holding ${pending.map(o => o.path).join(', ')} - TOOLING failure.`);
    console.error('something else is driving it, and a hold meant for this run may be spent.');
    process.exit(2);
  }
} catch (err) {
  console.error(`could not reach the stub at ${stubBase} - TOOLING failure.`);
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

const tooling = (message, detail) => {
  console.error(message);
  if (detail !== undefined) console.error(String(detail?.stack ?? detail));
  return browser.close().catch(() => {}).then(() => process.exit(2));
};

let failures = 0;
let inapplicable = 0;

try {
  for (const { url, state, label } of plan) {
    for (const assertion of assertions) {
      const { name, kind, states: appliesTo } = assertion;

      // Printed, never skipped silently. An assertion whose press cannot be staged here is not a
      // pass, and a run that reported it as one is what this file exists to prevent.
      if (appliesTo !== undefined && !appliesTo.includes(state)) {
        inapplicable += 1;
        console.log(`\n==> state ${label}`);
        console.log(`    n/a  [${kind}] ${name}`);
        console.log(`         only measured in ${appliesTo.join(', ')}; ` +
          `this is ${state === null ? 'no state' : state}`);
        continue;
      }

      console.log(`\n==> state ${label}: ${name}`);

      // 1280 and 900, the width the sibling scans call ordinary. Nothing here is about layout, but
      // a control has to be on screen to be pressed, and both samples put the facet panel beside
      // the results rather than behind a fold at this width.
      const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
      const page = await context.newPage();

      try {
        await page.goto(url, { waitUntil: 'networkidle', timeout: 60_000 });
      } catch (err) {
        await context.close().catch(() => {});
        await tooling(`could not load ${url} - TOOLING failure.`, err);
      }

      // Blazor Server paints a shell first and fills it over the circuit, so acting immediately
      // acts on an empty page.
      await page.waitForTimeout(settleMs);

      if (state !== null) {
        try {
          await states[state](page);
        } catch (err) {
          await context.close();
          await tooling(`could not reach state "${state}" on ${url} - TOOLING failure.`, err);
        }
      }

      let staged;
      try {
        staged = await assertion.stage(page, stub);
      } catch (err) {
        // Staging is this file driving the page, so a failure here is the harness or the fixture,
        // not a finding. Reporting it as one would send someone hunting a defect that is not there.
        await context.close().catch(() => {});
        await tooling(`could not stage "${name}" on ${url} - TOOLING failure.`, err);
      }

      let finding;
      try {
        finding = await assertion.measure(page, staged);
      } catch (err) {
        await context.close().catch(() => {});
        await tooling(`assertion "${name}" threw - TOOLING failure.`, err);
      }

      if (finding !== null) {
        failures += 1;
        console.log(`    FAIL [${kind}] ${name}`);
        console.log(`         ${finding}`);
        await page.close();
        await context.close();
        continue;
      }

      // The control, on the page the assertion just held against: leave the browser's own flip
      // standing, which is the whole of what the missing line would have failed to undo.
      try {
        await assertion.control(page, staged);
      } catch (err) {
        await context.close().catch(() => {});
        await tooling(`the control for "${name}" would not run - TOOLING failure.`, err);
      }

      let controlled;
      try {
        controlled = await assertion.measure(page, staged);
      } catch (err) {
        await context.close().catch(() => {});
        await tooling(`assertion "${name}" threw under its control - TOOLING failure.`, err);
      }

      if (controlled === null) {
        await context.close().catch(() => {});
        await tooling(
          `assertion "${name}" held against the defect it exists for - TOOLING failure.`,
          new Error('it is not measuring the disagreement any more; read its pass above as absent'));
      }

      console.log(`    ok   [${kind}] ${name}`);
      console.log(`         and it still fires: ${controlled}`);

      await page.close();
      await context.close();
    }
  }
} catch (err) {
  // Anything reaching here is the scanner failing, not the page. Exit 1 would be read as
  // "the component is wrong" by the caller.
  console.error('the scan did not complete - this is a TOOLING failure, not a finding.');
  console.error(String(err?.stack ?? err));
  await browser.close().catch(() => {});
  process.exit(2);
} finally {
  await browser.close().catch(() => {});
}

// A hold left over means a staged wait was never spent, which makes the next run's pre-flight
// stop for a reason that belongs to this one.
const leftover = await stub.holding().catch(() => []);
if (leftover.length > 0) {
  console.error(`\nthe stub is still holding ${leftover.map(o => o.path).join(', ')} - TOOLING failure.`);
  process.exit(2);
}

console.log('');
const notRun = inapplicable === 0 ? '' : `, and ${inapplicable} did not apply`;
if (failures > 0) {
  console.log(`${failures} state assertion(s) failed${notRun}.`);
} else {
  console.log(`every state assertion that applies held${notRun}.`);
}

process.exit(failures > 0 ? 1 : 0);
