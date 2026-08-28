category: Added

- **`KildeExplorer`, the kildeutforsker, ships from this package beside `VariableExplorer`** - a
  second parameterised root component, under the same host rules as the first: no `@page`, no
  `@rendermode`, no router, no CSS. It renders a search field, a `{n} kilder` count and a
  six-column table of the catalogue's kilder, and opening one hands it to the `KildeView` the
  variable explorer already drills into, so the two cannot render one source two ways. Kelda's own
  sections reach that view through its `Sections` parameter and its own heading for the datasamling
  table through `DataCollectionsHeading`; nothing Kelda-specific was added to the view itself.
  (Fhi.Metadata-2fomm.1)
- **The kilde list is fetched once and searched in the browser** - `GET /api/explorer/kilder` is not
  paged and answers with the whole catalogue in one array, so the list is asked for exactly once,
  unfiltered, and the search field narrows what is already in hand by name, code or short name. It
  is therefore deliberately without a pager and without sortable headers: the API returns the rows
  ordered by name and there is nothing to page to. The field binds on `change` rather than `input`
  all the same — on a Blazor Server circuit `input` is one round-trip per keystroke whatever the
  handler does with it. (Fhi.Metadata-2fomm.1)
- **`SelectedKildeId` and `SelectedKildeIdChanged`**, so a host can put the open kilde in its own
  URL with `@bind-SelectedKildeId`. It is the only piece of this component's state worth sharing:
  the search text is component state and goes away on refresh, which is the parity decision the
  Kelda epic records rather than an omission. (Fhi.Metadata-2fomm.1)
