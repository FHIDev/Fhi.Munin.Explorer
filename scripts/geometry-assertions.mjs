// What the hostile host is measured against, and the one file to read before quoting a green run.
//
// Each entry is a function that runs INSIDE the page and returns `null` when it holds or a string
// describing what it measured when it does not. They are handed to Playwright's page.evaluate, so
// they must be self-contained: no imports, no closures over anything in this module. page.evaluate
// takes exactly ONE argument, so every body takes the same `{ mount, header }` object — passing
// two selectors as two parameters fails at run time with "Too many arguments".
//
// WHY GEOMETRY AT ALL. Four layout defects reached a branch on 2026-09-03 having passed 1317 unit
// tests and eight axe states. Every one of them was found by a human with getBoundingClientRect —
// content past the viewport, children laid out side by side instead of stacked, an element
// carrying `hidden` that still had a box. axe was green through all four, and axe was right to be:
// none of them is an accessibility rule violation. They are shape.
//
// INVARIANT vs PIN, and why the distinction is written into the data rather than the prose.
// A suite written only against four known defects passes the moment those four are fixed and
// catches nothing afterwards — it is a changelog with an exit code. So each assertion declares
// which it is:
//
//   kind: 'invariant' — a property of any correct rendering, which nothing about the four defects
//                       is baked into. These are the ones that can catch a defect nobody has seen
//                       yet, and they are the reason this file is worth running.
//   kind: 'pin'       — a replay of a specific defect. It cannot catch anything new by
//                       construction. It is here because the composition that produced it is
//                       still the composition we ship, and because a pin fails with a much more
//                       useful message than the invariant that would also have caught it.
//
// Five of the eight below are invariants. If that ratio ever inverts, this file has become a
// changelog.
//
// A pin may also declare `states: [...]`, naming the states from axe-states.mjs whose page can
// contain the defect at all. Elsewhere geometry-scan.mjs prints it as inapplicable and says so,
// rather than running it: the three tab pins below all need a tablist, and on a page that has
// none — the kildeutforsker on /kilder — "nothing was measured" is a fact about the page and not
// a finding. An invariant never declares one; an invariant that does not hold everywhere is a pin
// that has not admitted to it. (Fhi.Metadata-fih3y)

/** The component's own root. Everything measured is inside it or is the host chrome around it. */
const MOUNT = '.munin-explorer';

/** helsedata's header: `position: absolute; top: 0`, with a 64px content row inside it. */
const HEADER = '.main-header';

/** The single argument every body receives — page.evaluate accepts exactly one. */
export const selectors = { mount: MOUNT, header: HEADER };

export const assertions = [
  {
    name: 'no horizontal overflow',
    kind: 'invariant',
    // The plainest statement of "it fits", and the one that does not care WHY it did not. Defect
    // 3 was found this way (1758px of content in a 1675px viewport) but nothing about defect 3 is
    // encoded here: any future element that is too wide for the page fails this line, at whichever
    // width it stops fitting. Run at several widths because a grid that fits at 1689 can overflow
    // at 1024, which is where the 384px filter column starts costing real room.
    body: () => {
      const d = document.documentElement;
      if (d.scrollWidth <= d.clientWidth) return null;
      return `document scrollWidth ${d.scrollWidth} > clientWidth ${d.clientWidth}`;
    },
  },

  {
    name: 'the component stays inside the box the host gave it',
    kind: 'invariant',
    // Stronger than the document check above, and it catches what that one misses: an ancestor
    // with `overflow: hidden` or `overflow-y: auto` absorbs a too-wide child, so the document
    // stops scrolling sideways while the content is still clipped and unreachable. The filter
    // panel is exactly such an ancestor above 1024px. Nothing here names a component or a class:
    // it asks every visible element under the mount to be within the mount's own content box.
    //
    // The tolerance is 1px for subpixel rounding — a 1487.98px child of a 1488px box is not a
    // defect, and reporting it as one would make this file the boy who cried overflow.
    //
    // A declared scroll box — role=region, tabindex=0, scrollable overflow-x — is measured
    // AGAINST rather than skipped: getBoundingClientRect reports a child of a scroll container
    // where it sits in the scrollable content, not where it is clipped, so a child of any correct
    // one reads as outside the mount. Children of one are held to that box's scrollable extent
    // instead, which still catches content no amount of scrolling reaches. tabindex is the
    // load-bearing half — an unreachable clip is the defect worth keeping — and not overflow-x
    // alone, which overflow-y also computes to and which would exempt the filter panel above. The
    // box itself is still measured against the mount. (Fhi.Metadata-fih3y)
    body: ({ mount: mountSel }) => {
      const mount = document.querySelector(mountSel);
      if (!mount) return `no ${mountSel} on the page — nothing was measured`;
      const box = mount.getBoundingClientRect();
      const tolerance = 1;
      for (const el of mount.querySelectorAll('*')) {
        const r = el.getBoundingClientRect();
        if (r.width === 0 && r.height === 0) continue;
        const style = getComputedStyle(el);
        // Deliberately out of the flow, placed against the viewport rather than the mount.
        if (style.position === 'fixed') continue;
        // Entirely to the left of the viewport: the visually-hidden idiom, and Stiler's own
        // `.screenreader-only` is `position: absolute; left: -10000px`. Off-canvas on purpose is
        // not overflow, and there is no way to overflow a container by being at -9875.
        if (r.right <= 0) continue;
        // Inside a scroll region the reader can reach, the box to fit is that region's scrollable
        // extent — see the note above. The region itself is not exempt.
        const scroller = nearestReachableScroller(el);
        const bounds = scroller === null ? box : scrollableBounds(scroller);
        if (r.right > bounds.right + tolerance || r.left < bounds.left - tolerance) {
          const whose = scroller === null ? "the mount's" : `${describe(scroller)}'s scrollable`;
          return `${describe(el)} spans ${Math.round(r.left)}..${Math.round(r.right)}, ` +
            `outside ${whose} ${Math.round(bounds.left)}..${Math.round(bounds.right)}`;
        }
      }
      return null;

      // Strictly between the element and the mount, so the mount's own overflow — the host's
      // business, not the package's — exempts nothing.
      function nearestReachableScroller(el) {
        for (let p = el.parentElement; p && p !== mount; p = p.parentElement) {
          if (p.getAttribute('role') !== 'region') continue;
          if (p.getAttribute('tabindex') !== '0') continue;
          const overflowX = getComputedStyle(p).overflowX;
          if (overflowX === 'auto' || overflowX === 'scroll') return p;
        }
        return null;
      }

      // Where the scrollable content begins and ends in viewport coordinates, which is the frame
      // a child's own rect is already reported in: the padding edge, shifted by whatever is
      // scrolled out of view, and as wide as there is content to scroll.
      function scrollableBounds(p) {
        const rect = p.getBoundingClientRect();
        const left = rect.left + p.clientLeft - p.scrollLeft;
        return { left, right: left + p.scrollWidth };
      }

      function describe(el) {
        const cls = typeof el.className === 'string' && el.className
          ? '.' + el.className.trim().split(/\s+/).join('.')
          : '';
        return `${el.tagName.toLowerCase()}${cls}`;
      }
    },
  },

  {
    name: 'hidden means hidden',
    kind: 'invariant',
    // The general form of defect 2. The browser's `[hidden] { display: none }` is a USER-AGENT
    // rule and loses to any author rule of equal specificity — Stiler's normalise block carries a
    // bare `div { display: block }`, so every hidden <div> on helsedata.no is visible. That is not
    // a fact about tab panels: it applies to every element this package will ever mark hidden, and
    // this assertion is written against the attribute rather than against the panels.
    //
    // Measured rather than computed from the style: an element can be display:none through a
    // parent, and a box of zero area is the thing that actually matters to a reader.
    //
    // A HOST MAY UN-HIDE ON PURPOSE — Stiler shows the facet panel wherever there is room for a
    // sidebar — and the tell is the rule, not the element: a rule whose own selector names
    // [hidden] was written about this attribute, where `div { display: block }` was not. The fold
    // must also be inert, so a control still pointing here that has a box of its own fails
    // anyway: a reader who can press it sees aria-expanded disagree with the panel. Nothing here
    // names a class, so the invariant stays on for every other element. (Fhi.Metadata-fih3y)
    body: ({ mount: mountSel }) => {
      const mount = document.querySelector(mountSel);
      if (!mount) return `no ${mountSel} on the page — nothing was measured`;
      for (const el of mount.querySelectorAll('[hidden]')) {
        const r = el.getBoundingClientRect();
        if (r.width * r.height === 0) continue;
        if (unhiddenOnPurpose(el)) continue;
        return `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''} carries [hidden] and ` +
          `still has a ${Math.round(r.width)}x${Math.round(r.height)} box`;
      }
      return null;

      function unhiddenOnPurpose(el) {
        for (const control of document.querySelectorAll('[aria-controls]')) {
          const names = (control.getAttribute('aria-controls') ?? '').split(/\s+/);
          if (!el.id || !names.includes(el.id)) continue;
          const c = control.getBoundingClientRect();
          if (c.width * c.height !== 0) return false;
        }
        for (const rule of applicableRules()) {
          if (!rule.selectorText.includes('[hidden]')) continue;
          const display = rule.style.getPropertyValue('display');
          if (display === '' || display === 'none') continue;
          try {
            if (el.matches(rule.selectorText)) return true;
          } catch {
            // A selector this browser cannot parse tells us nothing either way.
          }
        }
        return false;
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
    },
  },

  {
    name: 'nothing the reader can press is under the host header',
    kind: 'invariant',
    // The general form of defect 1, which was the tablist rendering at document top 0 with 64px of
    // helsedata's header over it. The header is `position: absolute; top: 0`, so it takes no space
    // in flow and the page's first 64px are underneath it — at scroll offset 0 anything there is
    // covered.
    //
    // Written against every visible control rather than against the tablist: what moves to the top
    // of the component next is not knowable from here, and a rule naming the tablist would have to
    // be rewritten by whoever breaks it. Interactive elements only, because a heading half under
    // the header is ugly and a button half under it is broken.
    body: ({ mount: mountSel, header: headerSel }) => {
      const header = document.querySelector(headerSel);
      if (!header) return `no ${headerSel} on the page — the host chrome did not render`;
      const mount = document.querySelector(mountSel);
      if (!mount) return `no ${mountSel} on the page — nothing was measured`;

      const band = header.getBoundingClientRect();
      if (band.height === 0) return `${headerSel} has no height — the host chrome did not render`;

      const controls = mount.querySelectorAll('button, a[href], input, select, textarea, summary, [role=tab]');
      for (const el of controls) {
        const r = el.getBoundingClientRect();
        if (r.width === 0 && r.height === 0) continue;
        // Off-canvas on purpose — the visually-hidden idiom. A skip link parked at -10000px is
        // not under the header; it is nowhere, until it takes focus and the host's own rule
        // brings it back.
        if (r.right <= 0) continue;
        if (r.top < band.bottom && r.bottom > band.top) {
          return `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''} ` +
            `"${(el.textContent ?? '').trim().slice(0, 40)}" sits at ${Math.round(r.top)}..` +
            `${Math.round(r.bottom)}, inside the header's ${Math.round(band.top)}..${Math.round(band.bottom)}`;
        }
      }
      return null;
    },
  },

  {
    name: 'text a reader is meant to see has a box to see it in',
    kind: 'invariant',
    // The inverse of "hidden means hidden", and the assertion that earned its place on the first
    // run of this fixture: it found a defect nobody had reported. Below Stiler's own
    // `@media (max-width: 1280px)` the result row becomes `flex-direction: column` while its
    // per-column `flex: 210 1 0` weights stay in force, so every cell's flex BASIS becomes its
    // height and collapses to 0. The names, codes, kilder and dates are all in the DOM, all
    // `visibility: visible`, and the row shows nothing but the save button. axe is green — the
    // text is there, so it is in the accessibility tree — and every other assertion here holds.
    //
    // Own text nodes only. An ancestor whose children are all zero-height is a consequence, not a
    // cause, and reporting the whole chain buries the one element that actually collapsed.
    body: ({ mount: mountSel }) => {
      const mount = document.querySelector(mountSel);
      if (!mount) return `no ${mountSel} on the page — nothing was measured`;
      for (const el of mount.querySelectorAll('*')) {
        const own = [...el.childNodes]
          .filter(n => n.nodeType === Node.TEXT_NODE)
          .map(n => n.textContent.trim())
          .join('');
        if (own.length === 0) continue;
        // Deliberately not shown: hidden subtrees, and the off-canvas visually-hidden idiom.
        if (el.closest('[hidden]')) continue;
        const r = el.getBoundingClientRect();
        if (r.right <= 0) continue;
        const style = getComputedStyle(el);
        if (style.display === 'none' || style.visibility === 'hidden') continue;
        if (r.height > 0) continue;
        return `${el.tagName.toLowerCase()}${el.className && typeof el.className === 'string' ? '.' + el.className.trim().split(/\s+/)[0] : ''} ` +
          `renders "${own.slice(0, 40)}" into a ${Math.round(r.width)}x${Math.round(r.height)} box`;
      }
      return null;
    },
  },

  {
    name: 'the tablist clears the header',
    kind: 'pin',
    states: ['explorer-tabs', 'explorer-list-tab'],
    // A replay of defect 1, kept beside the invariant above for the same reason as the two pins
    // below it: when the tablist is what moved, this names the tablist and the two numbers, where
    // the invariant reports whichever of its buttons it reached first. Nothing else measures the
    // tablist as a block — the invariant walks controls, and a tablist is not one.
    body: ({ header: headerSel }) => {
      const header = document.querySelector(headerSel);
      if (!header) return `no ${headerSel} on the page — the host chrome did not render`;
      const tablist = document.querySelector('[role=tablist]');
      if (!tablist) return 'no tablist on the page — nothing was measured';
      const band = header.getBoundingClientRect();
      const list = tablist.getBoundingClientRect();
      if (list.top >= band.bottom) return null;
      return `tablist top ${Math.round(list.top)} is above the header bottom ${Math.round(band.bottom)}`;
    },
  },

  {
    name: 'exactly one tab panel has content',
    kind: 'pin',
    states: ['explorer-tabs', 'explorer-list-tab'],
    // A replay of defect 2, and it stays because it fails with a far more useful message than
    // "hidden means hidden" does when the two panels are the thing that broke: it names how many
    // panels had text and how much. The invariant above is what would catch a NEW instance of the
    // same class; this is what makes the known one legible.
    //
    // Text content rather than a box, because that is what a reader sees. A panel that is present,
    // empty and 0px tall is not two panels open at once.
    body: () => {
      const panels = [...document.querySelectorAll('[role=tabpanel]')];
      if (panels.length === 0) return 'no tab panels on the page — nothing was measured';
      const filled = panels.filter(p => (p.textContent ?? '').trim().length > 0);
      if (filled.length === 1) return null;
      const shape = filled.map(p =>
        `${p.id || '(no id)'} ${(p.textContent ?? '').trim().length} chars, ` +
        `${Math.round(p.getBoundingClientRect().height)}px tall`);
      return `${filled.length} of ${panels.length} tab panels have text: ${shape.join('; ')}`;
    },
  },

  {
    name: 'no page shell class inside a tab panel',
    kind: 'pin',
    states: ['explorer-tabs', 'explorer-list-tab'],
    // A replay of defect 3. `munin-explorer` is the component's OWN root class and Stiler dresses
    // it as the page's sidebar-and-results grid — `display: grid; grid-template-columns: 384px
    // minmax(0, 1fr)` above 1024px. A view nested inside a tab panel that also wore it got its
    // heading, name field, create button and status messages laid out as columns of a page grid.
    //
    // A pin rather than an invariant: it names one class, so it can only ever catch that class
    // coming back. The "stays inside the box" invariant above is what caught the consequence, and
    // is what will catch the next nested-layout mistake under a different name.
    body: ({ mount: mountSel }) => {
      const offenders = [...document.querySelectorAll(`[role=tabpanel] ${mountSel}`)];
      if (offenders.length === 0) return null;
      return `${offenders.length} element(s) inside a tab panel carry ${mountSel}: ` +
        offenders.map(el => el.tagName.toLowerCase()).join(', ');
    },
  },
];
