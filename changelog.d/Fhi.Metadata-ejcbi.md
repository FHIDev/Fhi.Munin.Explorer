category: Changed
- The pager is helsedata.no's own shape now: `Forrige`, numbered pages, `Neste`, and a page-size
  dropdown. It drew "Side 1 av 907" between two buttons before, which says where the reader is and
  gives them nowhere to go — the last page of a long result was reachable only by pressing `Neste`
  until it arrived. The run carries the first page, the last page and three around the one in
  force, as `1 2 3 … 907`; where a skip would stand for a single page, that page is drawn instead.
- The page-size control is a `<select>` where it was three buttons. It takes its accessible name
  from a `<label for>` rather than repeating the phrase on every button, and a size a host asks for
  that is not one of the three — `PageSize="30"` — is added to the list rather than left out, since
  a select with no option for the size in force falls back to showing the first one.
- `VariableListView`'s pager changed with it and draws the same run from the same renderer, so a
  reader's own saved list is no longer walkable one page at a time either.
