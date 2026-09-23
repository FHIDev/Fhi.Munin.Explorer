// Whether a [hidden] element is shown on purpose: one answer for 'hidden means hidden' and the Tab
// walk (Fhi.Metadata-w8sms). A page.evaluate body cannot import, so each runner installs this on its
// context and callers read it off window, throwing (TOOLING) when it is missing.

// Serialised whole by addInitScript, so everything it uses is inside it.
function install() {
  // The tell is the rule, not the element: a selector naming [hidden] was written about the
  // attribute, where `div { display: block }` was not. The fold must be inert too, so a control
  // pointing here that has a box of its own fails anyway. No class is named. (Fhi.Metadata-fih3y)
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
