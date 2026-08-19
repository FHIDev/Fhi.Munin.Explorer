category: Added
- **A variable's full detail now opens inside its own result card** - "Vis detaljer" under any row
  discloses the description, the period, the kilde trail (kildetype › kilde › datasamling), every
  variabelgruppe the variable belongs to and the kodeverk its values are drawn from, fetched from
  `GetVariableAsync`. There is no navigation behind it and no `@page` — the panel is drawn in the
  row it belongs to, which is what lets a CMS host that owns its own routing offer variable detail
  at all. One row is open at a time; a fetch that fails or a variable that is not published says so
  inside the panel and leaves the rows alone. (Fhi.Metadata-l9l2n.14)
- **The open panel is part of the component's parameter surface** - `SelectedVariableId` and
  `SelectedVariableIdChanged` give a host `@bind-SelectedVariableId`, so a reader's place in the
  catalogue can be deep-linked the same way the search text and the filters already are. The
  selection is always a row on screen: an id the result does not hold is dropped rather than
  fetched, and a new search, filter, ordering or page that leaves the open row behind closes the
  panel and reports it, so a host's URL cannot come to name a variable the page is not showing.
  (Fhi.Metadata-l9l2n.14)
