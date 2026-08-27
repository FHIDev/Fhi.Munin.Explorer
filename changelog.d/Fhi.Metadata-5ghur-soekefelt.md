category: Fixed

- **Clearing the search box now takes effect, in both explorers, and there is a button that does
  it.** The field was an `<input type="search">`, so the browser drew a ✕ inside it — and pressing
  that ✕ emptied the box without applying the change. Both explorers bind their search field on
  `onchange` rather than `oninput`, deliberately, because `oninput` costs a Blazor Server round
  trip per keystroke; the ✕ fires the DOM `search` event instead, which is not one Blazor knows,
  and hooking it would mean shipping JavaScript this package does not ship. The result was a search
  box reading empty over a search still in force. In `KildeExplorer` that was worse than cosmetic:
  velg-alle, *Nullstill utvalg* and the handover all act on the rows currently matching, so they
  operated on a subset the reader believed they had cleared. In `VariableExplorer` the stale search
  had also reached the API and been reported to the host for its URL, so a shared link described
  results nobody was looking at. (Fhi.Metadata-5ghur)
- **The field is now `<input type="text" enterkeyhint="search">` with a *Tøm søket* / *Clear search*
  button beside it.** No user-agent ✕ to mislead, a soft keyboard still offers a search key, and one
  press restores the whole list — in the variable explorer that runs the search again with no term,
  so the API, the facet counts and `SearchChanged` all follow. The button is **always on screen**,
  greyed via `aria-disabled` while there is nothing to clear, rather than appearing and
  disappearing beside a field somebody is typing in. `aria-disabled` and not `disabled`, so that
  pressing it cannot throw focus to the document; the component refuses the click itself. Neither
  clear touches the facets or the filter: a reader who narrowed twice asked for both, and one
  control must not quietly undo the other. (Fhi.Metadata-5ghur)
- **Kelda's handover button says what it is about to carry.** One button, three payloads — so three
  wordings, read off the same two questions the payload is, which is what keeps the label and the
  ids from disagreeing. *Utforsk variabler for utvalget* with rows ticked, *Utforsk variabler for
  treffene* when a search or a facet is narrowing and nothing is ticked, and *Utforsk alle
  variabler* on an untouched list. Munin's Kelda writes the first in all three cases; the behaviour
  here is identical and only the sentence differs. (Fhi.Metadata-5ghur)
