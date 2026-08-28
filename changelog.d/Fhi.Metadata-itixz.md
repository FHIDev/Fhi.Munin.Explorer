category: Added

- **`VariableListView` shows the reader's saved variable lists** - which lists they have, what is in
  the one they are looking at, and the two things they can do to it: take a variable out, or make
  another list. A separate root component rather than a tab inside the explorer, because the host
  decides where it goes — helsedata's own stories put "mine variabellister" on its own page.
  (Fhi.Metadata-itixz)
- **It shares `VariableListState` with the explorer's save button**, so removing a variable here is
  reflected there without either surface refetching. What it does not share is paging: which page is
  being looked at belongs to the surface looking at it, not to a holder three surfaces read, which is
  why the holder deliberately never wrapped `GetMyListVariablesAsync`.
- **An entry whose variable has no row in the read model keeps its place**, labelled rather than
  filtered out. The API returns it on purpose so the paging totals stay honest — a view that dropped
  it would show one row fewer than the count above it claims, and the reader would never learn that
  something had gone.
- **The list is paged, and the pager is real.** A saved list is as long as the reader made it, and
  the endpoint answers a page at a time. Fetching the first page and calling it the list would show
  the first 25 and hide the rest without saying so.
- **Signed out there is nothing at all** — not an empty frame, and not a sign-in prompt this package
  has no business wording. The host knows how its readers sign in; the package does not.
- **No new class names.** The rows wear the same `munin-explorer-dataitem-*` names the search results
  wear, so the host needs no new rules. The class-name guard runs on a render with both a normal row
  and an unavailable one.
