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
  // caught on the way down, and the window's own, fired at the document, in its target phase.
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

  observer.observe(facts);
  observers.set(barId, () => {
    observer.disconnect();
    bar.removeEventListener('focusout', focusout);
    window.removeEventListener('hashchange', reread);
    document.removeEventListener('scrollend', reread, { capture: true });
  });
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

  const spy = { column, clicked: null, frame: 0, scroller: null, lastRoot: null, lastTop: 0 };
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
      spy.clicked = event.target.closest?.('a[href*="#"]')?.getAttribute('href').split('#')[1] ?? null;
      schedule();
    }
  };
  const unpin = () => {
    spy.clicked = null;
  };
  // The scroller is the element seen scrolling the page, never one guessed from its styles: an
  // `overflow-x: hidden` wrapper computes as `overflow-y: auto` and would freeze the mark.
  const page = column.closest('.munin-explorer-page') ?? column;
  const scrolled = (event) => {
    if (event.target === document) {
      spy.scroller = null;
    } else if (event.target !== column && event.target.contains?.(page)) {
      spy.scroller = event.target;
    }

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

  // The jump line is where a fragment jump puts a section — its scroll-margin-top plus the
  // scroller's scroll-padding-top — so a click and a scroll to the same place mark the same entry.
  const origin = root === document.scrollingElement ? 0 : root.getBoundingClientRect().top + root.clientTop;
  const padding = pixels(getComputedStyle(root).scrollPaddingTop, root.clientHeight);
  let current = entries[0];

  for (const entry of entries) {
    entry.top -= origin;

    if (entry.top <= padding + (parseFloat(getComputedStyle(entry.section).scrollMarginTop) || 0) + 1) {
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
