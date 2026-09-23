// Whether an element carrying [hidden] is shown ON PURPOSE, in one place because two gates ask it:
// geometry-assertions.mjs's 'hidden means hidden' and tab-stop-scan.mjs. If they answered apart, a
// panel could be a deliberate un-hide to one and a defect to the other (Fhi.Metadata-w8sms).
//
// It has to run inside the page, and a page.evaluate body is serialised on its own, so it cannot
// import anything. Each runner installs it on its context instead, before the first page loads, and
// the callers read it off window. A caller that finds nothing there throws, which every runner
// reports as a TOOLING failure rather than as a finding.

// Serialised whole by addInitScript, so everything it uses is inside it.
function install() {
  // A host may un-hide on purpose, and the tell is the rule rather than the element: one whose
  // own selector names [hidden] was written about the attribute, where `div { display: block }`
  // was not. The fold must be inert too, so a control still pointing here that has a box of its
  // own fails anyway. No class is named. (Fhi.Metadata-fih3y)
  window.__muninUnhiddenOnPurpose = function unhiddenOnPurpose(el) {
    for (const control of document.querySelectorAll('[aria-controls]')) {
      const names = (control.getAttribute('aria-controls') ?? '').split(/\s+/);
      if (!el.id || !names.includes(el.id)) continue;
      const c = control.getBoundingClientRect();
      if (c.width * c.height !== 0) return false;
    }
    for (const rule of applicableRules()) {
      const display = rule.style.getPropertyValue('display');
      if (display === '' || display === 'none') continue;
      for (const branch of topLevelBranches(rule.selectorText)) {
        if (!branch.includes('[hidden]')) continue;
        try {
          if (el.matches(branch)) return true;
        } catch {
          // A selector this browser cannot parse tells us nothing either way.
        }
      }
    }
    return false;
  };

  // Per branch, not per rule: `.other, .panel[hidden]` names the attribute in one half and can
  // match through the other, which would smuggle an accidental override past the check. Split
  // at the top level only, so `:is(a, b)` and `[x="a,b"]` keep their own commas.
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

  // Style rules in force at this width. A stylesheet the page cannot read contributes
  // nothing, so an unreadable one leaves the check failing rather than exempting.
  function applicableRules() {
    const found = [];
    walk([...document.styleSheets].flatMap(sheet => {
      try { return [...sheet.cssRules]; } catch { return []; }
    }));
    return found;

    function walk(rules) {
      for (const rule of rules) {
        if (rule.selectorText && rule.style) found.push(rule);
        else if (rule.cssRules && inForce(rule)) walk([...rule.cssRules]);
      }
    }

    function inForce(rule) {
      if (rule.media) return matchMedia(rule.conditionText).matches;
      if (rule.conditionText) return CSS.supports(rule.conditionText);
      return true;
    }
  }
}

/** Makes the check available to every page the context opens from here on. */
export const installUnhiddenOnPurpose = context => context.addInitScript(install);
