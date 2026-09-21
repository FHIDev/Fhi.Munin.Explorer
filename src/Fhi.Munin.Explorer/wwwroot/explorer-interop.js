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

  const observer = new IntersectionObserver(([entry]) => {
    // BOTH halves. `!isIntersecting` is also true of a hero row still BELOW the fold, which is how
    // a short viewport starts before the reader has scrolled at all — so dropping `top < 0` shows
    // the bar on load rather than on the row leaving upwards.
    const on = !entry.isIntersecting && entry.boundingClientRect.top < 0;

    bar.classList.toggle(SHOWN, on);
    bar.hidden = !on;
    bar.setAttribute('aria-hidden', String(!on));
  });

  observer.observe(facts);
  observers.set(barId, observer);
}

/**
 * Stops watching for `barId` and forgets the observer, leaving the bar as the markup renders it.
 *
 * Called when the component goes away: an observer outliving it holds the elements it watches.
 */
export function disconnectHeroFacts(barId) {
  observers.get(barId)?.disconnect();
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

  const spy = { column, clicked: null, frame: 0 };
  const schedule = () => {
    spy.frame ||= requestAnimationFrame(() => {
      spy.frame = 0;
      markCurrent(spy);
    });
  };
  const click = (event) => {
    // A modified or middle press opens another tab and leaves this one where it is.
    if (event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {
      spy.clicked = event.target.closest?.('a[href*="#"]') ?? null;
      schedule();
    }
  };
  const unpin = () => {
    spy.clicked = null;
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
  document.addEventListener('scroll', schedule, { capture: true, passive: true });
  window.addEventListener('resize', schedule, { passive: true });

  for (const gesture of ['wheel', 'touchstart', 'keydown']) {
    window.addEventListener(gesture, unpin, { capture: true, passive: true });
  }

  spies.set(columnId, () => {
    cancelAnimationFrame(spy.frame);
    rendered.disconnect();
    resized.disconnect();
    column.removeEventListener('click', click);
    document.removeEventListener('scroll', schedule, { capture: true });
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

  for (const link of spy.column.querySelectorAll('a[href*="#"]')) {
    // The id as DetailToc wrote it: `hash` percent-encodes, and one bad entry must not stop the rest.
    const id = link.getAttribute('href').split('#')[1];
    const section = id ? page.querySelector(`#${CSS.escape(id)}`) : null;

    if (section !== null) {
      entries.push({ link, section, top: section.getBoundingClientRect().top });
    }
  }

  if (entries.length === 0) {
    return;
  }

  // The jump line is where a fragment jump puts a section — its scroll-margin-top plus the
  // scroller's scroll-padding-top — so a click and a scroll to the same place mark the same entry.
  const root = document.scrollingElement ?? document.documentElement;
  const padding = pixels(getComputedStyle(root).scrollPaddingTop, root.clientHeight);
  let current = entries[0];

  for (const entry of entries) {
    if (entry.top <= padding + (parseFloat(getComputedStyle(entry.section).scrollMarginTop) || 0) + 1) {
      current = entry;
    }
  }

  // At the end of the scroll the sections below the line can never reach it, so the last one is
  // current, unless the reader pressed that one or a lower one and has not wheeled, touched or typed.
  if (scrolledToEnd(root)) {
    current = entries.find((entry) => entry.link === spy.clicked && entry.top >= current.top)
      ?? entries[entries.length - 1];
  }

  for (const { link } of entries) {
    if (link === current.link) {
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

/** Whether the document has scrolled at all, and cannot scroll any further down. */
function scrolledToEnd(root) {
  return root.scrollTop > 0 && root.scrollTop + root.clientHeight >= root.scrollHeight - 1;
}
