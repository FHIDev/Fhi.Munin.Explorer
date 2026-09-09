category: Notes for hosts

- **A host rule that gives `munin-explorer-selection__explore` a width floor has to let it yield —
  `min-width: min(21rem, 100%)` and not `min-width: 21rem` — or the kildeutforsker scrolls the whole
  page sideways at a 320px viewport.** That name is the handover button over the kilder table, and a
  floor is the obvious way to stop its label — one of three, of different lengths — resizing the row
  on the first tick. CSS resolves `max-width` before `min-width`, so a `max-width: 100%` written
  beside the floor to prevent exactly this can never win: measured in the sample host, the document
  is **407px wide against a 320px viewport**, 87px of horizontal page scrolling, which fails WCAG
  1.4.10 Reflow at Level AA. The band runs from 320px to about 407px and it fits again from 414px,
  so no desktop and no tablet ever shows it.
  **`white-space: normal`, `height: auto` and `min-height: 2.75rem` go beside the floor rather
  than after it.** The wrap is not decoration: `hd-button-square` is `white-space: nowrap` at a
  fixed `2.75rem`, so a button allowed to shrink below its label spills the label out of its own
  box instead — measured with the longest Norwegian label, a 231px text run inside a 178px button.
  The `min-height` is what keeps the wrap from costing anything at wide widths: `height: auto`
  alone drops the button to its 42px content height, 2px short of the reset button beside it in
  the same centred row, so the fixed height has to become a floor rather than go away.
  **This is not a defect on helsedata.no today, and the numbers above are the sample's own.**
  `Fhi.Helsedata.Stiler` 0.1.42 declares nothing for `munin-explorer-selection__explore`, or for
  `munin-explorer-selection` around it, in any context — the `21rem` floor was the sample stand-in's
  invention, and a host on Stiler gets `hd-button-square` and no floor to overflow. The Stiler rule
  is filed as Fhi.Metadata-tx75j, which adds one rather than corrects one, and it needs to arrive
  already yielding. Both sample stylesheets carry the corrected rule from this version. Nothing in
  the package itself changed — no class name is added, renamed or removed — and
  `munin-explorer-kilder-scroll` was never the culprit: measured at 320px it clips an 875px table
  to the page exactly as it should. (Fhi.Metadata-l9l2n.65)
