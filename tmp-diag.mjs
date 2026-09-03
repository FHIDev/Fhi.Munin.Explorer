import { chromium } from 'playwright';
const b = await chromium.launch({ ignoreHTTPSErrors: true });
const p = await b.newPage({ ignoreHTTPSErrors: true, viewport: { width: 1675, height: 1000 } });
await p.goto('https://localhost:5000/MuninRuna', { waitUntil: 'domcontentloaded' });
await p.waitForTimeout(11000);
await p.getByRole('tab', { name: 'Variabelliste', exact: true }).first().click();
await p.waitForTimeout(3500);

const info = await p.evaluate(() => {
  const el = document.querySelector('[role=tabpanel]:not([hidden]) .munin-explorer');
  if (!el) return { found: false };
  const cs = getComputedStyle(el);
  const rules = [];
  for (const sheet of document.styleSheets) {
    let list; try { list = sheet.cssRules; } catch { continue; }
    for (const r of list) {
      if (!r.selectorText || !r.style) continue;
      const d = r.style.display, fd = r.style.flexDirection, w = r.style.width;
      if (!d && !fd && !w) continue;
      try {
        if (el.matches(r.selectorText)) {
          rules.push(`${r.selectorText} { ${d ? 'display:' + d + ';' : ''}${fd ? 'flex-direction:' + fd + ';' : ''}${w ? 'width:' + w : ''} }`);
        }
      } catch {}
    }
  }
  const r = el.getBoundingClientRect();
  return {
    found: true,
    cls: el.className,
    display: cs.display,
    flexDirection: cs.flexDirection,
    left: Math.round(r.left), width: Math.round(r.width), right: Math.round(r.right),
    rules,
    kids: [...el.children].map(c => {
      const b = c.getBoundingClientRect();
      return `${c.tagName}.${(c.className || '').toString().slice(0, 32)} left=${Math.round(b.left)} w=${Math.round(b.width)}`;
    }),
    messages: [...el.querySelectorAll('[role=alert], p')].map(e => e.textContent.trim()).filter(Boolean),
  };
});
console.log(JSON.stringify(info, null, 1));
await b.close();
