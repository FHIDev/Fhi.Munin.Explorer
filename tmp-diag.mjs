import { chromium } from 'playwright';

const b = await chromium.launch({ ignoreHTTPSErrors: true });
const p = await b.newPage({ ignoreHTTPSErrors: true, viewport: { width: 1689, height: 1000 } });
await p.goto('https://localhost:5000/MuninRuna', { waitUntil: 'domcontentloaded' });
await p.waitForTimeout(11000);
await p.getByRole('tab', { name: 'Variabelliste', exact: true }).first().click();
await p.waitForTimeout(3000);

const out = await p.evaluate(() => {
  const el = document.querySelector('[role=tabpanel]:not([hidden]) .munin-explorer');
  const hits = [];
  const walk = (rules, media) => {
    for (const r of rules) {
      if (r.cssRules) { walk(r.cssRules, r.conditionText || r.media?.mediaText || media); continue; }
      if (!r.selectorText || !r.style) continue;
      try { if (!el.matches(r.selectorText)) continue; } catch { continue; }
      const decls = [...r.style].map(k => `${k}:${r.style.getPropertyValue(k)}`).join('; ');
      hits.push(`${media ? '@media ' + media + ' ' : ''}${r.selectorText} { ${decls} }`);
    }
  };
  for (const sheet of document.styleSheets) {
    let rules; try { rules = sheet.cssRules; } catch { continue; }
    walk(rules, null);
  }
  const cs = getComputedStyle(el);
  return {
    cls: el.className,
    computed: { display: cs.display, gridTemplateColumns: cs.gridTemplateColumns, gap: cs.gap },
    matchingRules: hits,
  };
});
console.log(JSON.stringify(out, null, 1));
await b.close();
