category: Notes for hosts

- **The column picker needs seven names a host without Stiler must now style**, and one it no
  longer uses. New: `form-control` and `form-control__label` (the row a checkbox and its label
  share), and `icon`, `icon-layout`, `icon--right`, `icon-keyboard-arrow-down` and
  `icon-keyboard-arrow-up` on the trigger. Gone: `hd-button-reset`, with any rule drawing a tick
  for `aria-pressed`. A host must also supply `position: relative` on `.munin-explorer__dropdown`,
  which the component used to emit inline and now leaves to the stylesheet - without it the open
  list anchors to whatever is positioned further up the page. Both chevrons are always in the DOM;
  hide the one that contradicts `[open]`, since the component ships no script and cannot swap a
  class. Both sample stylesheets show the whole set. (Fhi.Metadata-f6az7)

- **A host on Stiler needs at least `Fhi.Helsedata.Stiler` 0.1.53.** The picker's positioning, its
  right-aligned trigger, the chevron that follows `[open]` and the cue on the column that refuses
  all ship in that release. Measured against an earlier Stiler: the dropdown computes
  `position: static` so the open panel anchors to whatever is positioned further up the page, the
  trigger falls back to the left of the results row, and both chevrons draw at once. helsedata.no
  pinned 0.1.42 on 2026-09-08, so this component must not reach them in a release that does not
  lift Stiler with it. (Fhi.Metadata-f6az7)
