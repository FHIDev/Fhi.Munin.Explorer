category: Notes for hosts

- **Three class names to style if you are not on Stiler.** The kilde list emits
  `munin-explorer-kilder` for its table, `munin-explorer-kilder__name` for the control that opens a
  row and `munin-explorer-kilder__count` for the three columns holding a number. A host that
  supplies no rule for any of them still gets a usable list — the shapes underneath are a `<table>`
  and a `<button>`, so the columns still line up and the name is still visibly a control — which is
  why they are handles rather than names that carry meaning nothing else carries. Both sample hosts'
  `host.css` carries rules for all three, right after the kilde view's own block.
  (Fhi.Metadata-2fomm.1)
- **`KildeExplorer` mounts the way `VariableExplorer` does**, and needs the same of the component
  that mounts it: the parent creating `SelectedKildeIdChanged` must itself be interactive, because
  an `EventCallback` serialises to an empty delegate across a static-SSR to interactive-island
  boundary. Making the mount point interactive is not enough — see the note under
  Fhi.Metadata-5ghur for what that costs and how the samples arrange it. Set `HeadingLevel` to
  whatever keeps the surrounding page's outline unbroken, and `Language` to the page's own.
  (Fhi.Metadata-2fomm.1)
