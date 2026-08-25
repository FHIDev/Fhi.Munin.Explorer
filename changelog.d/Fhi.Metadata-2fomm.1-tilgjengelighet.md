category: Fixed

- **`KildeExplorer`'s open kilde no longer keeps a heading that says it is loading after the fetch
  has finished.** The heading is what the drilldown's `aria-labelledby` points at, and it fell back
  to "Henter datakilden …" whenever the list could not supply the kilde's name — which is every time
  a host passes a `SelectedKildeId` the catalogue does not publish, and any `SelectedKildeId` at all
  when the list itself failed to load. A screen reader entering the landmark was told the source was
  still loading indefinitely, while the status line underneath said the fetch had finished and found
  nothing. It now follows the load state and says the same sentence the status line does.
  (Fhi.Metadata-2fomm.1)
- **The kilde table's Dataansvarlig and Databehandler cells are no longer marked `lang="no"` when
  they hold the package's own "Not specified".** For a host rendering the explorer with
  `Language="en"`, an empty catalogue field produced `<td lang="no">Not specified</td>`, so a screen
  reader read an English string in a Norwegian voice (WCAG 3.1.2, Language of Parts). The cell is
  marked as the catalogue's language only when it really holds the catalogue's words.
  (Fhi.Metadata-2fomm.1)
