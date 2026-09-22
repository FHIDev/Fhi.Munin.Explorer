// How the browser gates scroll, in one place because two of them do it and both are measuring the
// same thing: scripts/state-assertions.mjs asserts the sticky fact bar's predicate, and
// scripts/axe-states.mjs stages the one page state where the bar is on screen for axe to read.
//
// WHY A FRAME AT A TIME. An IntersectionObserver notifies on a CROSSING. A jump from below the hero
// row to above it leaves every frame non-intersecting, so nothing is delivered and the bar reads
// stale — the gate would then be measuring the browser's notification rule rather than the module's
// predicate (Fhi.Metadata-14j7i). The step and the overshoot below are chosen for that reason and
// are the numbers a change to one gate would otherwise leave the other one disagreeing about.

/** How far past an element's bottom to land, so the crossing is unambiguous rather than borderline. */
const OVERSHOOT = 200;

/** Long enough for an IntersectionObserver to have delivered, which is one frame. */
const OBSERVER_SETTLE_MS = 400;

/** Scrolls to `y` the way a reader does — a frame at a time, never in one jump. */
const scrollLikeAReader = (page, y) => page.evaluate(async to => {
  // Smaller than the viewport, so nothing can pass through unseen in a single step.
  const step = Math.max(40, Math.floor(window.innerHeight / 4));

  // Instant, because Stiler's `html { scroll-behavior: smooth }` would animate each step and land
  // late, a few pixels off, while the page is still growing.
  for (let at = window.scrollY; Math.abs(to - at) > step; at += to > at ? step : -step) {
    window.scrollTo({ top: at, left: 0, behavior: 'instant' });
    await new Promise(next => requestAnimationFrame(next));
  }

  window.scrollTo({ top: to, left: 0, behavior: 'instant' });
}, y);

/** The scroll position that puts `#id` clear of the top of the viewport, with room to spare. */
const pastElement = (page, id) => page.evaluate(({ at, over }) => {
  const one = document.getElementById(at);

  // Named rather than left to read as "cannot read properties of null": the caller passed an id it
  // read off the DOM, so nothing here is what went wrong and the message has to say so.
  if (one === null) {
    throw new Error(`nothing on the page carries the id '${at}', so there is nothing to scroll past`);
  }

  return one.getBoundingClientRect().top + window.scrollY + one.offsetHeight + over;
}, { at: id, over: OVERSHOOT });

/** Scrolls until `#id` has left the viewport upwards, and waits for the observer to answer. */
export async function scrollPast(page, id) {
  await scrollLikeAReader(page, await pastElement(page, id));
  await page.waitForTimeout(OBSERVER_SETTLE_MS);
}

/** Puts the reader back where a page load leaves them, and waits for the observer to answer. */
export async function scrollToTop(page) {
  await scrollLikeAReader(page, 0);
  await page.waitForTimeout(OBSERVER_SETTLE_MS);
  // Once more: after a deep hierarchy state the page settles 2-14px down, cause not yet found.
  await page.evaluate(() => window.scrollTo({ top: 0, left: 0, behavior: 'instant' }));
}
