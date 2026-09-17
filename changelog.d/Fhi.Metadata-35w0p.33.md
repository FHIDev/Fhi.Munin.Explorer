category: Added
- **An expanded kilde row in the kildeutforsker now ends in a link to that source's variables.** A
  reader who has just opened a source's drawer, seen its datasamlinger and decided it is the one had
  no control that took them onward — they had to go to the variable explorer and filter by kilde
  themselves. The drawer now ends in "Vis alle variabler i" and the source's name, and it is drawn
  only where the host has said where a variable explorer is: `KildeExplorer` offers it wherever
  `VariableExplorerPath` is set, off the same path as the selection handover it already drove, and a
  host mounting `KildeSearch` itself wires the new `KildeVariablesHref` parameter —
  `Func<Guid?, string>?`, the shape `DatasamlingHref` has, answering an address for one kilde's id.
  Unset, no link is rendered: not a dead href, not an inert button. It is the single-source shortcut
  and does not replace "Utforsk variabler for utvalget", which acts on the rows the reader has
  ticked. (Fhi.Metadata-35w0p.33)
