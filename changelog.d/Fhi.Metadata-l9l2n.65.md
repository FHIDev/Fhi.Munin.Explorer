category: Notes for hosts

- **A host's `munin-explorer-selection__explore` rule needs `min-width: min(21rem, 100%)`, or the
  kildeutforsker scrolls the whole page sideways at a 320px viewport.** That name is the handover
  button over the kilder table, and it carries a `min-width: 21rem` floor so that its label — one
  of three, of different lengths — cannot resize the row on the first tick. CSS resolves
  `max-width` before `min-width`, so the `max-width: 100%` written beside that floor to prevent
  exactly this can never win: measured at 320px the document is **407px wide against a 320px
  viewport**, 87px of horizontal page scrolling, which fails WCAG 1.4.10 Reflow at Level AA. The
  band runs from 320px to about 407px and it fits again from 414px, so no desktop and no tablet
  ever shows it. Those figures are the sample host's rather than a Stiler host's, read across on
  the one declaration that causes them: the samples and the pinned 0.1.42 agree on
  `min-width: 21rem`, so a host on Stiler measures the same 87px.
  **`white-space: normal`, `height: auto` and `min-height: 2.75rem` go beside the floor rather
  than after it.** The wrap is not decoration: `hd-button-square` is `white-space: nowrap` at a
  fixed `2.75rem`, so a button allowed to shrink below its label spills the label out of its own
  box instead — measured with the longest Norwegian label, a 231px text run inside a 178px button.
  The `min-height` is what keeps the wrap from costing anything at wide widths: `height: auto`
  alone drops the button to its 42px content height, 2px short of the reset button beside it in
  the same centred row, so the fixed height has to become a floor rather than go away.
  `Fhi.Helsedata.Stiler` 0.1.42 still carries the old rule and a host on that version or older
  has the defect; the fix is filed there as Fhi.Metadata-tx75j. Both sample stylesheets carry the
  corrected rule from this version, so they are four declarations ahead of the pin until it
  lands. Nothing in the package itself changed — no class name is added, renamed or removed — and
  `munin-explorer-kilder-scroll` was never the culprit: measured at 320px it clips an 875px table
  to the page exactly as it should. (Fhi.Metadata-l9l2n.65)
