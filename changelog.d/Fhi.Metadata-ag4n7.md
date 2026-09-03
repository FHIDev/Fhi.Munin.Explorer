category: Changed
- **The clear-search control is now an ✕ inside the search box, left of the search button, in
  both explorers.** It was a separate *Tøm søket* button standing under the field. It is drawn
  only while there is something to clear, replacing the always-present greyed state — an ✕ inside
  an empty box invites a press that would do nothing. It is still the package's own `<button>`
  and the field is still `<input type="text">`: the browser's own ✕ on a `type="search"` field
  fires an event Blazor does not bind, which is the defect that removed it from inside the box in
  the first place. Pressing it does exactly what the old button did — the variable explorer
  re-runs the search with no term so the API, the facet counts and `SearchChanged` all follow,
  the kildeutforsker restores its list without a request — and it now returns focus to the search
  field, because the control leaves the page as it acts.
