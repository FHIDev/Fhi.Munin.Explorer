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
        if (r.right > box.right + tolerance || r.left < box.left - tolerance) {
          return `${describe(el)} spans ${Math.round(r.left)}..${Math.round(r.right)}, ` +
            `outside the mount's ${Math.round(box.left)}..${Math.round(box.right)}`;
        }
      }
      return null;

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
    body: ({ mount: mountSel }) => {
      const mount = document.querySelector(mountSel);
      if (!mount) return `no ${mountSel} on the page — nothing was measured`;
      for (const el of mount.querySelectorAll('[hidden]')) {
        const r = el.getBoundingClientRect();
        if (r.width * r.height !== 0) {
          return `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''} carries [hidden] and ` +
            `still has a ${Math.round(r.width)}x${Math.round(r.height)} box`;
        }
      }
      return null;
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
