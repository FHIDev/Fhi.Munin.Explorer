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
        if (!namesHiddenOnSubject(branch)) continue;
        try {
          if (el.matches(branch)) return true;
        } catch {
          // A selector this browser cannot parse tells us nothing either way.
        }
      }
    }
    return false;
  };

  // [hidden] has to be on the element the rule styles, and positively: `:not([hidden]) > div`
  // matches a hidden div and was written about its parent's attribute, not about un-hiding it.
  // Inside :is()/:where() still counts; inside :not()/:has() never does. (Fhi.Metadata-vy6ah)
  function namesHiddenOnSubject(branch) {
    const compound = subjectCompound(branch.trim());
    for (let i = 0; i < compound.length; i += 1) {
      const ch = compound[i];
      if (ch === '\\') {
        i += 1;
      } else if (ch === '[') {
        const end = closing(compound, i);
        if (/^\[\s*hidden\s*(?:[~|^$*]?=|\])/i.test(compound.slice(i, end + 1))) return true;
        i = end;
      } else if (ch === ':') {
        const name = /^:+([\w-]+)\(/.exec(compound.slice(i));
        if (name === null) continue;
        const open = i + name[0].length - 1;
        const end = closing(compound, open);
        if (/^(is|where|matches|-webkit-any)$/i.test(name[1]) &&
            topLevelBranches(compound.slice(open + 1, end)).some(namesHiddenOnSubject)) {
          return true;
        }
        i = end;
      }
    }
    return false;
  }

  // The compound after the last top-level combinator: the part that matches the element itself.
  function subjectCompound(selector) {
    let depth = 0;
    let quote = null;
    let start = 0;
    for (let i = 0; i < selector.length; i += 1) {
      const ch = selector[i];
      if (quote !== null) {
        if (ch === '\\') i += 1;
        else if (ch === quote) quote = null;
      } else if (ch === '\\') {
        i += 1;
      } else if (ch === '"' || ch === "'") {
        quote = ch;
      } else if (ch === '(' || ch === '[') {
        depth += 1;
      } else if (ch === ')' || ch === ']') {
        depth -= 1;
      } else if (depth === 0 && /[\s>+~]/.test(ch)) {
        start = i + 1;
      }
    }
    return selector.slice(start);
  }

  // Index of the bracket that closes the one at `open`, or the end when it is never closed.
  function closing(text, open) {
    let depth = 0;
    let quote = null;
    for (let i = open; i < text.length; i += 1) {
      const ch = text[i];
      if (quote !== null) {
        if (ch === '\\') i += 1;
        else if (ch === quote) quote = null;
      } else if (ch === '\\') {
        i += 1;
      } else if (ch === '"' || ch === "'") {
        quote = ch;
      } else if (ch === '(' || ch === '[') {
        depth += 1;
      } else if (ch === ')' || ch === ']') {
        depth -= 1;
        if (depth === 0) return i;
      }
    }
    return text.length - 1;
  }

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
