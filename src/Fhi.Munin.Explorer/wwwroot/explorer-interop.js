// The package's one browser module, imported by ExplorerInterop and by nothing else.
//
// Importing it must stay free of side effects — no listeners, no DOM writes — because every
// caller carries on without it and a module that changed the page on import would not be optional.
//
// Every export is named, and named again in ExplorerInterop.cs, which ExplorerInteropTest holds it
// to — by name, so an `export default` or `export * from` is refused rather than read as exporting
// nothing.

/** The class Fhi.Helsedata.Stiler draws a shown bar with. */
const SHOWN = 'munin-explorer-page__stuckbar--on';

/** The shown bar's height on the page root, which Stiler's sticky contents list clears. */
const STUCK_HEIGHT = '--munin-explorer-stuckbar-height';

// Room left between the stuck bar and a control focus brought out from under it.
const FOCUS_CLEARANCE = 8;

// Native dates expose an empty value for both a cleared control and an incomplete manual entry.
export function readDateInput(id) {
  const input = document.getElementById(id);
  return input instanceof HTMLInputElement && input.type === 'date'
    ? { value: input.value, badInput: input.validity.badInput }
    : null;
}

// One observer per mounted detail page, keyed by its own bar's id. Two explorers on one host page
// each have an entry of their own, so neither can drive or disconnect the other's bar.
const observers = new Map();

/**
 * Shows the sticky fact bar `barId` while the hero fact row `factsId` is off the top of the
 * viewport, and hides it again the moment that row is back.
 *
 * Calling it again for the same bar replaces the observer rather than adding a second one.
 */
export function observeHeroFacts(barId, factsId) {
  const bar = document.getElementById(barId);
  const facts = document.getElementById(factsId);

  if (bar === null || facts === null) {
    return;
  }

  disconnectHeroFacts(barId);

  const page = bar.closest('.munin-explorer-page');
  const inner = bar.querySelector('.munin-explorer-page__stuckbar-inner');

  // Measured, not assumed: a long name wraps the bar from 77px to 126px at 1280, and a hidden bar
  // measures 0, so the observer below follows showing and hiding too (Fhi.Metadata-l9l2n.125).
  const publish = () => {
    page?.style.setProperty(STUCK_HEIGHT, `${Math.ceil(inner.getBoundingClientRect().height)}px`);
  };

  let above = false;
  const update = () => {
    // Keep the action reachable until focus leaves; hiding its ancestor would strand keyboard focus.
    const on = above || bar.contains(document.activeElement);
    bar.classList.toggle(SHOWN, on);
    bar.hidden = !on;
    bar.setAttribute('aria-hidden', String(!on));
  };
  const focusout = () => queueMicrotask(update);
  bar.addEventListener('focusout', focusout);

  // WCAG 2.4.11: what Tab lands on under the stuck bar is scrolled clear of it, after the browser's
  // own focus scroll, which knows nothing of the bar (Fhi.Metadata-mhqx0.4).
  const focusin = (event) => {
    const target = event.target;
    if (!(target instanceof Element) || bar.hidden || bar.contains(target) || !page?.contains(target)
      || !target.matches(':focus-visible') || ownsItsPlace(target)) {
      return;
    }

    requestAnimationFrame(() => {
      if (!bar.isConnected || bar.hidden) return;
      const bottom = inner?.getBoundingClientRect().bottom ?? 0;
      // A bar over half the window leaves no room to clear; scrolling would push the control below it.
      if (bottom > window.innerHeight / 2) return;
      // Outwards through the scrolling ancestors: an inner one already at its top cannot clear it alone.
      const root = document.scrollingElement ?? document.documentElement;
      for (let scroller = scrollerOf(target); scroller; scroller = scroller === root ? null : scrollerOf(scroller)) {
        const top = target.getBoundingClientRect().top;
        if (top >= bottom) break;
        scroller.scrollBy({ top: top - bottom - FOCUS_CLEARANCE, behavior: 'instant' });
      }
    });
  };
  document.addEventListener('focusin', focusin, true);

  // An IntersectionObserver notifies on a CROSSING, and a jump straight past the row — an in-page
  // anchor press, or one window.scrollTo — leaves both frames non-intersecting, so nothing is
  // delivered and the bar reads stale (Fhi.Metadata-14j7i). The box itself always answers.
  const reread = () => {
    const box = facts.getBoundingClientRect();
    const intersecting = box.bottom > 0 && box.top < window.innerHeight
      && box.right > 0 && box.left < window.innerWidth;

    // The observer's predicate, both halves, computed rather than waited for.
    above = !intersecting && box.top < 0;
    update();
  };

  window.addEventListener('hashchange', reread);

  // On the document and captured, so an inner scroller's `scrollend` — which does not bubble — is
  // caught on the way down, and the window's own in its target phase. Below the floor (Safari < 18)
  // only `hashchange` re-reads, so a pushState host keeps the stale bar: this bead's known limit.
  if ('onscrollend' in window) {
    document.addEventListener('scrollend', reread, { capture: true, passive: true });
  }

  const observer = new IntersectionObserver(([entry]) => {
    // BOTH halves. `!isIntersecting` is also true of a hero row still BELOW the fold, which is how
    // a short viewport starts before the reader has scrolled at all — so dropping `top < 0` shows
    // the bar on load rather than on the row leaving upwards.
    above = !entry.isIntersecting && entry.boundingClientRect.top < 0;
    update();
  });

  const resized = new ResizeObserver(publish);

  if (inner !== null) {
    resized.observe(inner);
  }

  observer.observe(facts);
  observers.set(barId, () => {
    observer.disconnect();
    resized.disconnect();
    page?.style.removeProperty(STUCK_HEIGHT);
    bar.removeEventListener('focusout', focusout);
    document.removeEventListener('focusin', focusin, true);
    window.removeEventListener('hashchange', reread);
    document.removeEventListener('scrollend', reread, { capture: true });
  });
}

/** Whether `el` keeps its own place on screen: sticky or fixed, or a section the contents jump places. */
function ownsItsPlace(el) {
  if (el.matches('[data-nav-section]')) return true;
  for (let e = el; e && e !== document.body; e = e.parentElement) {
    const position = getComputedStyle(e).position;
    if (position === 'sticky' || position === 'fixed') return true;
  }
  return false;
}

/** The element that scrolls `el` into view: its nearest scrolling ancestor, or the document. */
function scrollerOf(el) {
  for (let e = el.parentElement; e && e !== document.body; e = e.parentElement) {
    const overflow = getComputedStyle(e).overflowY;
    if ((overflow === 'auto' || overflow === 'scroll') && e.scrollHeight > e.clientHeight) return e;
  }
  return document.scrollingElement ?? document.documentElement;
}

/**
 * Stops watching for `barId` and forgets the observer, leaving the bar as the markup renders it.
 *
 * Called when the component goes away: an observer outliving it holds the elements it watches.
 */
export function disconnectHeroFacts(barId) {
  observers.get(barId)?.();
  observers.delete(barId);
}

// One scroll-spy per mounted contents column, keyed by the column's id, each holding its own undo.
const spies = new Map();

/** Marks the contents nav entry in `columnId` whose section the reader is in with
 * `aria-current="location"`, and no other. Calling it again for the same column replaces the spy. */
export function observeContents(columnId) {
  disconnectContents(columnId);

  const column = document.getElementById(columnId);

  if (column === null) {
    return;
  }

  const spy = { column, clicked: null, jump: null, settle: 0, frame: 0, scroller: null, lastRoot: null, lastTop: 0 };
  const schedule = () => {
    spy.frame ||= requestAnimationFrame(() => {
      spy.frame = 0;
      markCurrent(spy);
    });
  };
  const click = (event) => {
    // A modified or middle press opens another tab and leaves this one where it is.
    if (event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
      // The section's id rather than the link, which a re-render can replace.
      const link = event.target.closest?.('a[href*="#"]');
      spy.clicked = link?.getAttribute('href').split('#')[1] ?? null;
      if (link && link.origin === location.origin && link.pathname === location.pathname
        && link.search === location.search) {
        spy.jump = spy.clicked;
        settleJump();
      }
      schedule();
    }
  };
  const unpin = () => {
    spy.clicked = null;
    spy.jump = null;
    clearTimeout(spy.settle);
  };
  // The scroller is the element seen scrolling the page, never one guessed from its styles: an
  // `overflow-x: hidden` wrapper computes as `overflow-y: auto` and would freeze the mark.
  const page = column.closest('.munin-explorer-page') ?? column;
  const settleJump = () => {
    clearTimeout(spy.settle);
    // Native and router-owned fragment jumps both scroll; wait until their smooth motion stops.
    spy.settle = setTimeout(() => {
      const section = spy.jump ? page.querySelector(`#${CSS.escape(spy.jump)}`) : null;
      if (section === null) {
        spy.jump = null;
        return;
      }

      const bottom = stickyBottom(page);
      const top = section.getBoundingClientRect().top;
      const root = spy.scroller?.isConnected ? spy.scroller : document.scrollingElement;
      if (bottom > 0 && top < bottom) {
        spy.jump = null;
        root?.scrollBy({ top: top - bottom - 1, behavior: 'instant' });
        schedule();
      } else if (root) {
        const origin = root === document.scrollingElement ? 0 : root.getBoundingClientRect().top + root.clientTop;
        const line = origin + pixels(getComputedStyle(root).scrollPaddingTop, root.clientHeight)
          + (parseFloat(getComputedStyle(section).scrollMarginTop) || 0);
        // Focusing the link can scroll before a server-side router starts the actual jump.
        if (Math.abs(top - line) < 2 || scrolledToEnd(root)) spy.jump = null;
      }
    }, 150);
  };
  const scrolled = (event) => {
    if (event.target === document) {
      spy.scroller = null;
    } else if (event.target !== column && event.target.contains?.(page)) {
      spy.scroller = event.target;
    } else {
      return;
    }

    if (spy.jump) settleJump();
    schedule();
  };

  // Entries and sections are looked up afresh on every pass, so a re-render is followed. The main
  // column is watched rather than the page, whose size the bold current entry itself can change.
  const rendered = new MutationObserver(schedule);
  const resized = new ResizeObserver(schedule);

  rendered.observe(column, { childList: true, subtree: true, characterData: true });
  const main = column.closest('.munin-explorer-page')?.querySelector('.munin-explorer-page__main');

  if (main) {
    resized.observe(main);
  }
  column.addEventListener('click', click);
  document.addEventListener('scroll', scrolled, { capture: true, passive: true });
  window.addEventListener('resize', schedule, { passive: true });

  for (const gesture of ['wheel', 'touchstart', 'keydown']) {
    window.addEventListener(gesture, unpin, { capture: true, passive: true });
  }

  spies.set(columnId, () => {
    cancelAnimationFrame(spy.frame);
    clearTimeout(spy.settle);
    rendered.disconnect();
    resized.disconnect();
    column.removeEventListener('click', click);
    document.removeEventListener('scroll', scrolled, { capture: true });
    window.removeEventListener('resize', schedule);

    for (const gesture of ['wheel', 'touchstart', 'keydown']) {
      window.removeEventListener(gesture, unpin, { capture: true });
    }
  });

  markCurrent(spy);
}

/**
 * Stops the scroll-spy for `columnId`, leaving the nav's links as they are.
 */
export function disconnectContents(columnId) {
  spies.get(columnId)?.();
  spies.delete(columnId);
}

// The wrapper deliberately has zero height; its visible inner row is what covers a jump target.
function stickyBottom(page) {
  const bar = page.querySelector(`.${SHOWN}:not([hidden])`);
  return bar?.querySelector('.munin-explorer-page__stuckbar-inner')?.getBoundingClientRect().bottom ?? 0;
}

/** Sets the one current entry: the last section whose top has reached its jump line. */
function markCurrent(spy) {
  const page = spy.column.closest('.munin-explorer-page') ?? document;
  const entries = [];
  const links = [...spy.column.querySelectorAll('a[href*="#"]')];

  for (const link of links) {
    // The id as DetailToc wrote it: `hash` percent-encodes, and one bad entry must not stop the rest.
    const id = link.getAttribute('href').split('#')[1];
    const section = id ? page.querySelector(`#${CSS.escape(id)}`) : null;

    if (section !== null) {
      entries.push({ id, link, section, top: section.getBoundingClientRect().top });
    }
  }

  const root = spy.scroller?.isConnected ? spy.scroller : document.scrollingElement ?? document.documentElement;

  // Any move up, a scrollbar drag included, ends the reader's press: the pin is for where it landed.
  if (root === spy.lastRoot && root.scrollTop < spy.lastTop - 1) {
    spy.clicked = null;
  }

  spy.lastRoot = root;
  spy.lastTop = root.scrollTop;

  // A wrapped bar can exceed the host's CSS clearance; follow the same line the jump clears.
  const origin = root === document.scrollingElement ? 0 : root.getBoundingClientRect().top + root.clientTop;
  const padding = pixels(getComputedStyle(root).scrollPaddingTop, root.clientHeight);
  const bottom = stickyBottom(page) - origin;
  let current = entries[0];

  for (const entry of entries) {
    entry.top -= origin;

    const jumpLine = Math.max(bottom, padding + (parseFloat(getComputedStyle(entry.section).scrollMarginTop) || 0));
    if (entry.top <= jumpLine + 1) {
      current = entry;
    }
  }

  // At the end of the scroll the sections below the line can never reach it, so the last one is
  // current, unless the reader pressed one of those and has not since wheeled, touched, typed or
  // scrolled up.
  if (current !== undefined && scrolledToEnd(root)) {
    current = entries.find((entry) => entry.id === spy.clicked && entry.top >= current.top)
      ?? entries[entries.length - 1];
  }

  // Every link, not only those with a section: one whose section has gone must lose its mark too.
  for (const link of links) {
    if (link === current?.link) {
      if (link.getAttribute('aria-current') !== 'location') {
        link.setAttribute('aria-current', 'location');
      }
    } else if (link.hasAttribute('aria-current')) {
      link.removeAttribute('aria-current');
    }
  }
}

/** A computed length in px; a percentage is of the scrollport's height, as scroll-padding's is. */
function pixels(value, height) {
  return (value.endsWith('%') ? parseFloat(value) * height / 100 : parseFloat(value)) || 0;
}

/** Whether `root` has scrolled at all, and cannot scroll any further down. */
function scrolledToEnd(root) {
  return root.scrollTop > 0 && root.scrollTop + root.clientHeight >= root.scrollHeight - 1;
}

/**
 * Half-ticks every checkbox under `rootId` that carries `data-mixed`, and clears the half-tick on
 * every other one. `indeterminate` is a DOM property with no attribute, so markup cannot carry it;
 * the component marks the box and this sets it after each render (Fhi.Metadata-cjezd).
 */
export function markMixed(rootId) {
  const root = document.getElementById(rootId);

  if (root === null) {
    return;
  }

  for (const box of root.querySelectorAll('input[type="checkbox"]')) {
    box.indeterminate = box.hasAttribute('data-mixed');
  }
}

// Per tab, keyed by the address, so Back finds the position its own entry was left at. Not the
// browser's own restoration: that runs before the circuit has drawn the rows (Fhi.Metadata-95j8b).
const scrollKey = () => `Fhi.Munin.Explorer:scroll:${location.pathname}${location.search}`;

// One undo per kilde list, keyed by its title's id.
const remembered = new Map();

/**
 * Writes the page's scroll position down whenever the reader leaves the list under `anchorId`'s
 * section: a link pressed inside it, which a host's router may intercept, or the page going away.
 */
export function rememberScroll(anchorId) {
  // A list torn down without forgetScroll, a dropped circuit's, would otherwise hold its table.
  for (const [id, entry] of remembered) {
    if (!entry.root.isConnected) {
      entry.forget();
    }
  }

  const root = document.getElementById(anchorId)?.closest('section');

  if (!root || remembered.has(anchorId)) {
    return;
  }

  const forget = () => {
    window.removeEventListener('pagehide', save);
    window.removeEventListener('pageshow', shown);
    root.removeEventListener('click', click, true);
    remembered.delete(anchorId);
  };
  function save() {
    if (!root.isConnected) {
      forget();
      return;
    }
    try {
      sessionStorage.setItem(scrollKey(), String(Math.round(window.scrollY)));
    } catch {
      // Storage refused (private mode, quota): the position is simply not kept.
    }
  }
  // Back from the cache restores the page and its scroll whole, and nothing reads the position.
  function shown(event) {
    if (event.persisted) {
      try {
        sessionStorage.removeItem(scrollKey());
      } catch {
        // Storage refused: nothing was kept.
      }
    }
  }
  // Captured, so it runs before a router's own document-level handler moves the address.
  function click(event) {
    if (event.target.closest?.('a[href]')) {
      save();
    }
  }

  window.addEventListener('pagehide', save);
  window.addEventListener('pageshow', shown);
  root.addEventListener('click', click, true);
  remembered.set(anchorId, { root, forget });
}

/** Stops keeping the position for the list under `anchorId`, which is going away. */
export function forgetScroll(anchorId) {
  remembered.get(anchorId)?.forget();
}

/**
 * Scrolls back to the position `rememberScroll` wrote down for this address, once, and otherwise
 * brings the row whose open panel is `panelId` into view — the shared link nobody has scrolled.
 */
export function restoreScroll(panelId) {
  let saved = null;
  try {
    saved = sessionStorage.getItem(scrollKey());
    sessionStorage.removeItem(scrollKey());
  } catch {
    // Storage refused: fall through to the row.
  }

  const y = saved === null ? NaN : Number(saved);

  if (Number.isFinite(y)) {
    window.scrollTo(0, y);
    return;
  }

  const panel = panelId ? document.getElementById(panelId) : null;
  (panel?.closest('tr')?.previousElementSibling ?? panel)?.scrollIntoView({ block: 'center' });
}

// A press outside an open menu closes it, as helsedata's own menus do; Tab out does not,
// nor does theirs (Fhi.Metadata-ht2gj). It presses the menu's own toggle, so the component closes it the way it always does.
let foldsWatched = false;
export function watchFolds() {
  if (foldsWatched) return;
  foldsWatched = true;
  document.addEventListener('pointerdown', event => {
    // The page behind a sheet is inert, so a press on the dimmed backdrop lands on whatever ancestor is not.
    for (const [panel, { toggle }] of sheets) {
      if (panel.contains(event.target)) return;
      // Without this the press's mousedown moves focus to <body> after the sheet has given it back.
      event.preventDefault();
      if (toggle) closeSheetFrom(panel, toggle);
      swallowTheClickAfter(event);
      return;
    }

    for (const menu of document.querySelectorAll('.munin-explorer-list-menu')) {
      const toggle = menu.querySelector(':scope > button[aria-expanded="true"]');
      if (toggle && !menu.contains(event.target)) toggle.click();
    }
  }, true);

  // «Lukk» on a sheet, from a pointer or the keyboard alike: the click is what both send.
  document.addEventListener('click', event => {
    const close = event.target.closest?.('.munin-explorer-list-menu__close');
    if (!close) return;
    const toggle = close.closest('.munin-explorer-list-menu')?.querySelector(':scope > button[aria-expanded="true"]');
    if (toggle) closeSheetFrom(close, toggle);
  });

  // Arrows, Home and End between an open panel's controls, as in helsedata's own menus (Fhi.Metadata-mhqx0.8).
  // A disclosure, not role="menu": Tab still walks the same controls, and a text field keeps its own keys.
  document.addEventListener('keydown', event => {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key) || event.altKey || event.ctrlKey || event.metaKey) return;
    const panel = event.target.closest?.('.munin-explorer-list-menu > .munin-explorer-list-menu__panel');
    if (!panel || event.target.matches('input:not([type="checkbox"]):not([type="radio"]), textarea, select')) return;
    const items = [...panel.querySelectorAll('button, input, select, textarea, [href]')]
      .filter(item => !item.disabled && item.getClientRects().length > 0);
    if (items.length === 0) return;
    const at = items.indexOf(event.target);
    const next = {
      ArrowDown: items[(at + 1) % items.length],
      ArrowUp: items[(at - 1 + items.length) % items.length],
      Home: items[0],
      End: items[items.length - 1],
    }[event.key];
    event.preventDefault();
    next.focus();
  });

  // A window turned or resized across the phone width while a menu is open: it changes form, open, where it is.
  phoneWidth().addEventListener('change', () => {
    for (const panel of document.querySelectorAll('.munin-explorer-list-menu > .munin-explorer-list-menu__panel:not([hidden])')) {
      if (phoneWidth().matches) {
        openSheet(panel, undefined, { keepFocus: true });
      } else {
        closeSheet(panel, { keepFocus: true });
      }
    }
  });

  // A press on a sheet's text drops focus to <body>, where no menu's own key handler hears Escape.
  document.addEventListener('keydown', event => {
    if (event.key !== 'Escape' || event.target !== document.body) return;
    for (const [panel, { toggle }] of sheets) {
      if (toggle) closeSheetFrom(panel, toggle);
    }
  });

  // A menu opened near the bottom edge put its panel below the fold on a phone, where the reader saw
  // nothing happen (Fhi.Metadata-qlo45). Brought into view as it opens; 'nearest' leaves one already on screen be.
  new MutationObserver(records => {
    // A sheet whose panel left the page while open (a navigation, a menu no longer drawn) would leave the
    // page inert for good: nothing else closes it, since its hidden never changes again.
    if (sheets.size > 0) {
      for (const panel of [...sheets.keys()]) {
        if (!panel.isConnected) closeSheet(panel, { keepFocus: true });
      }
    }
    for (const { target, type } of records) {
      if (type !== 'attributes' || !target.matches?.('.munin-explorer-list-menu > .munin-explorer-list-menu__panel')) continue;
      if (target.hidden) {
        closeSheet(target);
      } else if (phoneWidth().matches) {
        openSheet(target);
      } else {
        requestAnimationFrame(() => target.scrollIntoView({ block: 'nearest' }));
      }
    }
  }).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden'] });
}

// A tap's click is aimed after the press: the sheet is gone by then, and the control under the finger would get it.
// Armed from the press until its click; a press that became a scroll sends pointercancel and no click.
function swallowTheClickAfter(press) {
  if (press.pointerType === 'mouse') return;
  let timer = 0;
  const swallow = event => {
    if (!event.isTrusted) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    done();
  };
  const lifted = event => {
    if (event.pointerId !== press.pointerId) return;
    window.removeEventListener('pointerup', lifted, true);
    timer = setTimeout(done, 300);
  };
  const cancelled = event => {
    if (event.pointerId === press.pointerId) done();
  };
  const done = () => {
    window.removeEventListener('click', swallow, true);
    window.removeEventListener('pointerup', lifted, true);
    window.removeEventListener('pointercancel', cancelled, true);
    clearTimeout(timer);
  };
  window.addEventListener('click', swallow, true);
  window.addEventListener('pointerup', lifted, true);
  window.addEventListener('pointercancel', cancelled, true);
}

// On a phone an open menu is a bottom sheet, not an overlay over the rows (WCAG 2.5.8, Fhi.Metadata-mhqx0.8):
// in the top layer clear of sticky and scrolling ancestors, a modal dialog, and the page behind it inert.
// Asked for on first use, not on import: importing the module must not touch the page.
let phoneQuery = null;
function phoneWidth() {
  return phoneQuery ??= window.matchMedia('(max-width: 479.98px)');
}
const sheets = new Map();

function openSheet(panel, toggle = panel.parentElement.querySelector(':scope > button[aria-controls]'), { keepFocus = false } = {}) {
  if (sheets.has(panel) || typeof panel.showPopover !== 'function') return;
  const restore = { toggle, role: panel.getAttribute('role'), labelledby: panel.getAttribute('aria-labelledby'), inert: [] };
  sheets.set(panel, restore);

  panel.setAttribute('popover', 'manual');
  panel.setAttribute('role', 'dialog');
  panel.setAttribute('aria-modal', 'true');
  restore.label = panel.getAttribute('aria-label');
  if (!panel.hasAttribute('aria-label')) {
    if (toggle?.id) panel.setAttribute('aria-labelledby', toggle.id);
    else if (toggle) panel.setAttribute('aria-label', toggle.textContent.trim());
  }
  panel.classList.add('munin-explorer-list-menu__panel--sheet');
  for (const close of closeControlsOf(panel)) showCloseControl(close, true);
  panel.showPopover();

  // Everything beside the path from the sheet to <body>, so nothing behind it takes a press or focus.
  for (let node = panel; node && node !== document.body; node = node.parentElement) {
    for (const sibling of node.parentElement?.children ?? []) {
      if (sibling !== node && !sibling.inert && sibling.localName !== 'script') {
        sibling.inert = true;
        restore.inert.push(sibling);
      }
    }
  }

  // Into the sheet, as a dialog takes it; on a resize only from outside it, since the page behind is now inert.
  if (!keepFocus || !panel.contains(document.activeElement)) {
    panel.querySelector('button:not([aria-disabled="true"]), input:not([disabled]), [href]')?.focus();
  }
}

// The component may ask for focus on the toggle before this has run, while the toggle is still inert. A sheet
// is modal, so focus still inside it (or dropped to <body>) is focus nothing else claimed: back to the toggle.
function closeSheet(panel, { keepFocus = false } = {}) {
  const restore = sheets.get(panel);
  if (!restore) return;
  sheets.delete(panel);
  const focusIsOrphaned = !keepFocus && (panel.contains(document.activeElement) || document.activeElement === document.body);

  for (const node of restore.inert) node.inert = false;
  if (panel.matches(':popover-open')) panel.hidePopover();
  panel.removeAttribute('popover');
  panel.removeAttribute('aria-modal');
  panel.classList.remove('munin-explorer-list-menu__panel--sheet');
  for (const close of closeControlsOf(panel)) showCloseControl(close, false);
  if (restore.role === null) panel.removeAttribute('role'); else panel.setAttribute('role', restore.role);
  if (restore.labelledby === null) panel.removeAttribute('aria-labelledby'); else panel.setAttribute('aria-labelledby', restore.labelledby);
  if (restore.label === null) panel.removeAttribute('aria-label');
  if (focusIsOrphaned) restore.toggle?.focus();
}

function closeControlsOf(panel) {
  return panel.querySelectorAll(':scope > .munin-explorer-list-menu__close');
}

// Inline as the markup has it: a host's display rule on the rows beats [hidden] alone.
function showCloseControl(close, shown) {
  close.hidden = !shown;
  if (shown) close.style.removeProperty('display'); else close.style.display = 'none';
}


function closeSheetFrom(target, toggle) {
  const panel = target.closest('.munin-explorer-list-menu__panel');
  closeSheet(panel);
  toggle.click();
}

// A reader's own setting, kept in this browser only (Fhi.Metadata-amp8c). Storage can be off, full or
// private, so a failure reads as "no setting" and a write that fails is dropped.
export function readPreference(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function writePreference(key, value) {
  try {
    if (value === null) {
      localStorage.removeItem(key);
    } else {
      localStorage.setItem(key, value);
    }
  } catch {
    // Not kept, and nothing else depends on it.
  }
}
