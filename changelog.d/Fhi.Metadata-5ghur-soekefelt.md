category: Fixed

- **Clearing the search box now takes effect, in both explorers.** The field was an
  `<input type="search">`, so the browser drew a ✕ inside it — and pressing that ✕ emptied the box
  without applying the change. Both explorers bind their search field on `onchange` rather than
  `oninput`, deliberately, because `oninput` costs a Blazor Server round trip per keystroke; the ✕
  fires the DOM `search` event instead, which is not one Blazor knows, and hooking it would mean
  shipping JavaScript this package does not ship. The result was a search box reading empty over a
  search still in force. In `KildeExplorer` that was worse than cosmetic: velg-alle, *Nullstill
  utvalg* and the handover itself all act on the rows currently matching, so they operated on a
  subset the reader believed they had cleared. In `VariableExplorer` the stale search had also
  reached the API and been reported to the host for its URL. The field is now
  `<input type="text" enterkeyhint="search">` — no ✕ to mislead, and a soft keyboard still offers a
  search key. Clearing works as it always did otherwise: empty the box and press Enter, or move
  focus out of it. (Fhi.Metadata-5ghur)
