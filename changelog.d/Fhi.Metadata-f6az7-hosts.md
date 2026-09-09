category: Notes for hosts

- **The column picker needs eight names a host without Stiler must now style**, and one it no
  longer uses. New: `form-control` and `form-control__label` (the row a checkbox and its label
  share), and `icon`, `icon-layout`, `icon--right`, `icon-keyboard-arrow-down` and
  `icon-keyboard-arrow-up` on the trigger. Gone: `hd-button-reset`, with any rule drawing a tick
  for `aria-pressed`. A host must also supply `position: relative` on `.munin-explorer__dropdown`,
  which the component used to emit inline and now leaves to the stylesheet - without it the open
  list anchors to whatever is positioned further up the page. Both chevrons are always in the DOM;
  hide the one that contradicts `[open]`, since the component ships no script and cannot swap a
  class. Both sample stylesheets show the whole set. (Fhi.Metadata-f6az7)
