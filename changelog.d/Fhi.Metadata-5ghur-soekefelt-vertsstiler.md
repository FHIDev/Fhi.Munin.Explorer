category: Notes for hosts

- **Two more class names to style if you are not on Stiler**, both from the selection bar. The
  search row's two names were here as well and have been superseded before release: the clear
  control moved inside the search field under `Fhi.Metadata-ag4n7`, `munin-explorer-search` no
  longer exists, and `munin-explorer-search__clear` is drawn only when there is something to
  clear, so it has no `[aria-disabled="true"]` state to grey. See that entry for what it needs
  now. `munin-explorer-selection` is the ribbon under the
  results — the handover button, then *Nullstill utvalg*, then the "{n} kilder valgt" count, in
  that order so that everything which comes and goes sits to the right of everything that does
  not. Make it a flex row. `munin-explorer-selection__explore` is the handover button, and it needs
  a **`min-width`**: its label is one of three and they are different lengths, so without a floor
  the button resizes on the first tick and drags the rest of the row with it. The samples use
  `21rem`, which clears the longest label at their own font size — measure your own rather than
  copying the number.
  Both sample hosts carry all of it, and tests here assert the load-bearing declarations rather
  than just the names. A host that supplies none of it still gets every control, stacked and at
  natural widths. (Fhi.Metadata-5ghur)
- **The `type="search"` → `type="text"` change is safe on Stiler, and this was checked rather
  than assumed.** Every selector in helsedata's compiled bundle that mentions the search field is
  a bare class selector — `.searchbox__freetext`, `.searchbox__freetext:focus`,
  `.searchbox__freetext::placeholder`, `.searchbox__freetext-container` — with nothing scoped to
  `input[type="search"]`. Read off `https://helsedata.no/dist/styles.<hash>.css` on 2026-08-27.
  The field keeps every rule it had. (Fhi.Metadata-5ghur)
