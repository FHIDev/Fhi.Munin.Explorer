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
// Eight of the fourteen below are invariants. If that ratio ever inverts, this file has become a
// changelog.
//
// A pin may also declare `states: [...]` — the states from axe-states.mjs whose page can contain
// its defect at all; elsewhere geometry-scan.mjs prints it as inapplicable rather than failing on
// "nothing was measured". Invariants never declare one. (Fhi.Metadata-fih3y)
//
// And `gates: [...]`: states entered by pressing what the pin measures. When one cannot be entered,
// the pin is measured on the page it stopped on, and only a failure there makes it a finding.

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

      // Named, because the numbers alone leave the reader of a failed run to find the element
      // themselves - and on a runner with other fonts that is a page they cannot open.
      const past = [...document.querySelectorAll('*')]
        .map(el => [el, el.getBoundingClientRect()])
        .filter(([, box]) => box.width > 0 && box.right > d.clientWidth + 0.5)
        .sort((one, other) => other[1].right - one[1].right)
        .slice(0, 3)
        .map(([el, box]) => `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''}` +
          `${typeof el.className === 'string' && el.className ? '.' + el.className.trim().split(/\s+/).join('.') : ''}` +
          ` ${Math.round(box.left)}..${Math.round(box.right)}`);

      return `document scrollWidth ${d.scrollWidth} > clientWidth ${d.clientWidth}` +
        (past.length > 0 ? `; widest past the edge: ${past.join(' | ')}` : '');
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
    // A declared scroll box — role=region, tabindex=0, scrollable overflow-x — is measured AGAINST
    // rather than skipped: getBoundingClientRect reports a child of a scroll container where it
    // sits in the scrollable content, not where it is clipped, so a child of any correct one reads
    // as outside the mount. tabindex is the gate; the box itself still answers to the mount.
    // (Fhi.Metadata-fih3y)
    body: ({ mount: mountSel }) => {
      const mount = document.querySelector(mountSel);
      if (!mount) return `no ${mountSel} on the page — nothing was measured`;
      const box = mount.getBoundingClientRect();
      const tolerance = 1;
      for (const el of mount.querySelectorAll('*')) {
        const r = el.getBoundingClientRect();
        if (r.width === 0 && r.height === 0) continue;
        // Invisible ancestors retain descendant rectangles. The bounds check measures painted
        // content; document overflow is checked independently even when its cause is transparent.
        if (!el.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true })) continue;
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
    // A host may un-hide on purpose; scripts/hidden-on-purpose.mjs says how that is told, and
    // tab-stop-scan.mjs asks it the same way (Fhi.Metadata-fih3y, Fhi.Metadata-w8sms).
    body: ({ mount: mountSel }) => {
      const unhiddenOnPurpose = window.__muninUnhiddenOnPurpose;
      if (typeof unhiddenOnPurpose !== 'function') {
        throw new Error('hidden-on-purpose.mjs was not installed on this page');
      }
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

  {
    name: "the kilder table's expand control is big enough to hit",
    kind: 'pin',
    states: ['kilder-list'],
    // A pin, not an invariant: Fhi.Metadata-mpx2p measured 20 x 24 against WCAG 2.5.8's 24 x 24.
    // The invariant it resembles — every control at least 24 x 24 — does not pass this page today,
    // because the selection checkbox is a native 13 x 13 (Fhi.Metadata-ycv15, a different fix).
    body: () => {
      const toggles = [...document.querySelectorAll('.munin-explorer-kilder__expand-toggle')];
      if (toggles.length === 0) return 'no expand toggle on the page — nothing was measured';
      const minimum = 24;
      for (const toggle of toggles) {
        const r = toggle.getBoundingClientRect();
        if (r.width < minimum || r.height < minimum) {
          return `expand toggle measures ${r.width.toFixed(1)} x ${r.height.toFixed(1)}, ` +
            `under the ${minimum} x ${minimum} minimum target size`;
        }
      }
      return null;
    },
  },

  {
    name: "Runa's row chevron is big enough to hit",
    kind: 'pin',
    states: ['explorer-tabs', 'variable-detail'],
    // A pin, for the kilder pin's reason: the 24 x 24 invariant fails on the native checkbox. Under
    // Stiler 0.1.91 this chevron was 0 x 0 below 1280px (Fhi.Metadata-m586y), and `gates` makes
    // that a finding when variable-detail and variable-whole cannot press it (Fhi.Metadata-kqano).
    gates: ['variable-detail', 'variable-whole'],
    body: () => {
      const toggles = [...document.querySelectorAll('button.munin-explorer-dataitem__expand-toggle')]
        .filter(toggle => !toggle.closest('[hidden]'));
      if (toggles.length === 0) return 'no row chevron on the page — nothing was measured';
      const width = Math.round(window.innerWidth);
      const minimum = 24;
      for (const [index, toggle] of toggles.entries()) {
        const r = toggle.getBoundingClientRect();
        if (r.width < minimum || r.height < minimum) {
          return `at ${width}px row chevron ${index + 1} of ${toggles.length} measures ` +
            `${r.width.toFixed(1)} x ${r.height.toFixed(1)}, under the ${minimum} x ${minimum} ` +
            'minimum target size';
        }
      }
      return null;
    },
  },

  {
    name: "the kilder table's counts are right-aligned in their column",
    kind: 'pin',
    // Both kilder states, because the columns differ: `kilder-list` draws Datasamlinger and
    // Variabler, and Delkilder — hidden by default — is only drawn in `kilder-counts`, which is a
    // target of check-hostile-host.sh for that reason alone.
    states: ['kilder-list', 'kilder-counts'],
    // Nothing else in this repository can see this. The right alignment and the tabular figures
    // come entirely from Stiler's `.munin-explorer-kilder .munin-explorer-kilder__count`: a unit
    // test can only say the class is on the cell, and the sample stylesheets are a stand-in no
    // host restores. A release that dropped that rule — or rescoped it under an ancestor it can
    // never match, which is the munin-explorer-skiplink-pagination shape — would pass every other
    // check in both repositories.
    //
    // THE TRAP: measure the TEXT, not the cell. A cell's border box is identical whichever way its
    // contents are aligned, so an assertion written against the cell's own rect passes with the
    // alignment fully gone. A Range over the cell's contents is the thing that moves.
    //
    // Two measurements, because agreement down a column can be vacuous. With the rule taken away
    // the figures fall to the cell's left edge, and the fixture's Datasamlinger (9, 6, 1, 0, 11)
    // and Variabler (240, 630, 23, 0, 12728) then spread by the digits they differ in — while
    // Delkilder (0, 0, 0, 0, 4) still agrees exactly. Flush against the cell's own content edge is
    // what fails in that column, and it needs no second row to say so.
    body: () => {
      const tables = [...document.querySelectorAll('table.munin-explorer-kilder')];
      if (tables.length === 0) return 'no kilder table on the page — nothing was measured';

      // Subpixel, because a column that has lost its alignment is out by tens of pixels and never
      // by one. The flush check is allowed the 1px the "stays inside the box" invariant allows,
      // for the same rounding reason and on the same terms.
      const spreadTolerance = 0.5;
      const flushTolerance = 1;

      const width = Math.round(window.innerWidth);
      const columns = new Map();

      for (const table of tables) {
        for (const cell of table.querySelectorAll('tbody td.munin-explorer-kilder__count')) {
          const column = heading(table, cell);
          const figure = (cell.textContent ?? '').trim();

          const range = document.createRange();
          range.selectNodeContents(cell);
          const text = range.getBoundingClientRect();
          if (text.width === 0) {
            return `at ${width}px the ${column} column's "${figure}" has no text box — ` +
              'nothing was measured';
          }

          const style = getComputedStyle(cell);
          const contentRight = cell.getBoundingClientRect().right -
            parseFloat(style.borderRightWidth) - parseFloat(style.paddingRight);
          if (text.right < contentRight - flushTolerance) {
            return `at ${width}px the ${column} column's "${figure}" ends at ` +
              `${text.right.toFixed(2)} with its cell's content edge at ` +
              `${contentRight.toFixed(2)} — ${(contentRight - text.right).toFixed(2)}px of slack ` +
              'on the right, so the figure is not aligned to it';
          }

          const key = `${tables.indexOf(table)}:${cell.cellIndex}`;
          const edges = columns.get(key) ?? { column, measured: [] };
          edges.measured.push({ figure, right: text.right });
          columns.set(key, edges);
        }
      }

      if (columns.size === 0) {
        return 'the kilder table drew no count cells — nothing was measured';
      }

      for (const { column, measured } of columns.values()) {
        const rights = measured.map(m => m.right);
        const spread = Math.max(...rights) - Math.min(...rights);
        if (spread <= spreadTolerance) continue;
        return `at ${width}px the ${column} column's text right edges spread ` +
          `${spread.toFixed(2)}px, over the ${spreadTolerance}px tolerance: ` +
          measured.map(m => `"${m.figure}" at ${m.right.toFixed(2)}`).join(', ');
      }
      return null;

      // The column's own heading, so a failure names Delkilder, Datasamlinger or Variabler rather
      // than an index the reader has to count out along the row.
      function heading(table, cell) {
        const th = table.tHead?.rows[0]?.cells[cell.cellIndex];
        return (th?.textContent ?? '').trim() || `column ${cell.cellIndex}`;
      }
    },
  },

  {
    name: "the detail page's main column is the wider part of its body",
    kind: 'invariant',
    // Which of the chassis's two tracks the reading column landed in is Stiler's to decide, and
    // nothing here can see it: bUnit pins the body's children, never their boxes. A comparison
    // rather than the 250px the rule names, so a retuned track is not a failure and a swap is.
    body: () => {
      const width = Math.round(window.innerWidth);
      for (const body of document.querySelectorAll('.munin-explorer-page__body')) {
        const children = [...body.children];
        const toc = children.find(el => el.classList.contains('munin-explorer-page__toc'));
        // A body with no contents column is one track by design — the saved-list view never draws
        // one — so there is nothing to compare and nothing to report.
        if (toc === undefined) continue;
        const main = children.find(el => el.classList.contains('munin-explorer-page__main'));
        if (main === undefined) {
          return `at ${width}px a body with a contents column has no ` +
            '.munin-explorer-page__main beside it — nothing was measured';
        }

        const rail = toc.getBoundingClientRect();
        const column = main.getBoundingClientRect();
        // Neither has a layout box, so there are no columns to compare: `0 <= 0` below would
        // report the rail winning a comparison nobody made.
        if (rail.width === 0 && column.width === 0) continue;

        // Side by side, read off the boxes rather than off a copy of Stiler's breakpoint that
        // nothing in this repository can check: the body is two tracks when they do not overlap.
        const beside = rail.right <= column.left + 1 || column.right <= rail.left + 1;
        if (beside) {
          if (column.width > rail.width) continue;
          return `at ${width}px the main column is ${column.width.toFixed(1)}px wide beside a ` +
            `${rail.width.toFixed(1)}px contents rail — the reading column is in the rail's track`;
        }

        // One track: the narrow layout, and also a body whose `grid-template-columns` has gone —
        // identical boxes, so only the viewport separates them. 1200 is a floor no page stacks a
        // detail view at, clear of Stiler's 1025 so that a breakpoint that moved is not a red.
        if (width >= 1200) {
          const held = body.getBoundingClientRect().width.toFixed(1);
          return `at ${width}px the ${held}px body is one track: a ` +
            `${column.width.toFixed(1)}px main column stacked over a ` +
            `${rail.width.toFixed(1)}px contents rail — the second track is not declared`;
        }
        if (column.width < rail.width) {
          return `at ${width}px the stacked main column is ${column.width.toFixed(1)}px wide ` +
            `under a ${rail.width.toFixed(1)}px contents rail`;
        }
      }
      return null;
    },
  },

  {
    name: "the detail page's two columns share a row",
    kind: 'invariant',
    // Two tracks side by side are one row, and a rail dropped to a row of its own passes the
    // comparison above with both widths still right. At scroll offset 0, where this file measures:
    // the rail is sticky, so anywhere else it has left its row on purpose.
    body: () => {
      const width = Math.round(window.innerWidth);
      for (const body of document.querySelectorAll('.munin-explorer-page__body')) {
        const children = [...body.children];
        const toc = children.find(el => el.classList.contains('munin-explorer-page__toc'));
        const main = children.find(el => el.classList.contains('munin-explorer-page__main'));
        // One track by design, or the missing main column the assertion above reports.
        if (toc === undefined || main === undefined) continue;

        const rail = toc.getBoundingClientRect();
        const column = main.getBoundingClientRect();
        // Stacked is one track: the narrow layout, or above 1200px the collapsed grid the width
        // assertion reports. Either way there is no row of two tracks here to measure.
        const beside = rail.right <= column.left + 1 || column.right <= rail.left + 1;
        if (!beside) continue;

        if (Math.abs(column.top - rail.top) > 1) {
          return `at ${width}px the main column starts at ${column.top.toFixed(1)} and the ` +
            `contents rail at ${rail.top.toFixed(1)} — the two tracks are not one row`;
        }
      }
      return null;
    },
  },

  {
    name: "the detail page's fact list has as many tracks as its container fits",
    kind: 'invariant',
    // Stiler 0.1.107 asks the container, not the viewport: `repeat(auto-fill, minmax(min(440px,
    // 100%), 1fr))`, so a count per viewport width is wrong the moment the column around it moves.
    // The expected count is auto-fill's own arithmetic on the measured box. (Fhi.Metadata-2w7fx)
    body: () => {
      const minTrack = 440;
      const width = Math.round(window.innerWidth);
      for (const grid of document.querySelectorAll('.munin-explorer-page__fields')) {
        const style = getComputedStyle(grid);
        const box = grid.getBoundingClientRect();
        if (box.width === 0 || style.display !== 'grid') continue;

        const inner = box.width - ['paddingLeft', 'paddingRight', 'borderLeftWidth', 'borderRightWidth']
          .reduce((sum, side) => sum + (parseFloat(style[side]) || 0), 0);
        const gap = parseFloat(style.columnGap) || 0;
        const expected = Math.max(1, Math.floor((inner + gap + 0.01) / (Math.min(minTrack, inner) + gap)));
        const tracks = style.gridTemplateColumns === 'none'
          ? [inner]
          : style.gridTemplateColumns.split(' ').map(parseFloat);
        if (tracks.length === expected) continue;

        return `at ${width}px a ${inner.toFixed(1)}px .munin-explorer-page__fields draws ` +
          `${tracks.length} track(s) of ${tracks.map(t => `${t.toFixed(1)}px`).join(' + ')} with a ` +
          `${gap}px gap; ${expected} of at least ${minTrack}px fit its container`;
      }
      return null;
    },
  },
];
