category: Added
- **`DetailNamedSection` and a `NamedSections` parameter on `KildeView`, `VariableView` and
  `DatasamlingView` put a section of your own in the contents nav.** Each `DetailNamedSection` is an
  id, a heading and a body; the view draws it as a section under a heading at the level of its own
  blocks and adds the matching nav entry, both off the same value, so a listed section is always on
  the page. `KildeView` and `DatasamlingView` draw them after their own blocks, `VariableView`
  between the metadata and the version history. Give each an id no other element on the page has,
  and none the views write (`metadata`, `criteria`, `source`, `statistics`, `datacollections`,
  `versions`, `dataperiod`, `datatype`, `variablegroups`), or the id is on the page twice and the
  nav's second link lands on the first. `Sections` is unchanged: still a
  fragment, drawn right after the named sections, and not listed in the nav, because a view cannot
  see an id or a heading inside a fragment. `KildeSearch.Sections` stays a fragment and is not
  listed either. (Fhi.Metadata-fkiz9)
