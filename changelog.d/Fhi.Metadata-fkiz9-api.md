category: Added
- **`KildeView`, `VariableView` and `DatasamlingView` take `NamedSections`, and the contents nav
  lists every one.** Each `DetailNamedSection` is an id, a heading and a body; the view draws it as a
  section under a heading at the level of its own blocks and adds the matching nav entry, both off
  the same value, so a listed section is always on the page. `Sections` is unchanged and still drawn
  last, but the nav does not list it, because a view cannot see an id or a heading inside a
  fragment: a host that wants its own section in the nav mounts the view and passes it through
  `NamedSections` instead. `KildeSearch.Sections` stays a fragment and is not listed either.
  (Fhi.Metadata-fkiz9)
