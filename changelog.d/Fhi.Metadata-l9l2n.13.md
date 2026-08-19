category: Added
- **`VariableExplorer` can now be filtered by facet, with counts** - a panel above the results
  offers kildetype, datakilde (each with its delkilde tree), variabelgruppe, saved catalogue
  filters, datatype, helsefaglig and administrativt kodeverk, instrument, "har kildekodeverk" and
  "vis historiske". Every value carries the number of variables it would leave, and those numbers
  are cross-filtered: choosing a datakilde moves the counts on every other facet, because the
  component asks `GetFiltersAsync` with the same selection it asked the search with. Choosing a
  value narrows the list and goes back to page one; choosing it again removes it. A selection whose
  fetch fails is rolled back, so the buttons never claim a filter the rows on screen did not come
  from, and a facet refresh that fails leaves the panel in place and says the counts may be stale
  rather than emptying it under the reader's hand. The whole kilde/delkilde tree is built from the
  facet payload alone — no second request. (Fhi.Metadata-l9l2n.13)
- **Filter state is part of the component's parameter surface** - `Filter` and `FilterChanged` give
  a host `@bind-Filter`, so a filtered search can be deep-linked. `VariableFilter.ToQueryString()`
  and `VariableFilter.Parse()` are the two halves of putting it in a URL, using the Explorer API's
  own parameter names; the callback always reports the filter actually in force, including after a
  rollback, so a host's URL cannot come to disagree with the page. (Fhi.Metadata-l9l2n.13)
