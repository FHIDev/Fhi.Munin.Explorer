category: Notes for hosts

- **Three more class names to style if you are not on Stiler**, all from the search row and the
  selection bar. `munin-explorer-search` wraps the search field and its clear button — make it a
  flex line and let `.searchbox__freetext-container` inside it shrink (`flex: 1 1 320px;
  min-width: 0`), or the field keeps the whole width and the button lands under it.
  `munin-explorer-search__clear` is the clear button itself, and it needs one thing besides
  placement: a **greyed appearance under `[aria-disabled="true"]`**, because it is always on screen
  and that attribute is the only thing saying whether it has anything to do. It is `aria-disabled`
  rather than `disabled` on purpose — a disabled button cannot hold focus, so pressing it would
  clear the search and throw the reader's focus to the document — which also means it stays
  hoverable and focusable, so style those states too. `munin-explorer-selection` is the ribbon under the
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
- **Check one selector in Stiler if your search field looks wrong after upgrading.** Both
  explorers' search inputs changed from `type="search"` to `type="text"` (see Fixed). The class
  name is unchanged — it is still `searchbox__freetext`, still Stiler's own — so a rule written as
  `.searchbox__freetext { … }` is unaffected, which is how both sample hosts write it. A rule
  written as `input[type="search"].searchbox__freetext { … }` would stop matching. Nothing in this
  repository can see Stiler's compiled `main.css`, so this one is stated rather than verified: if
  the field renders at browser defaults after upgrading, that selector is why, and the fix is a
  one-word change in Stiler rather than anything here. (Fhi.Metadata-5ghur)
