category: Added
- **A detail page pins a condensed fact bar to the top of the viewport once its hero row scrolls
  away.** The entity's name and code and the first three facts of the hero row — a summary of the
  summary, with every word in it still on the page below. Text only: `DetailPage.Actions` is not
  repeated in it, so nothing a caller put in that fragment gains a second tab stop or a duplicate
  `id`. `DetailPage` draws the bar, so the kilde, datasamling and variable views get it at once;
  `VariableListView` names no hero facts and so draws no bar. (Fhi.Metadata-35w0p.28)
- **`DetailPage` takes `StickyName`, `StickyNameLang` and `StickyCode` for it.** The name as the
  page's own heading says it, with a `lang` for the half the catalogue holds only in Norwegian, and
  the identifiers beside it — left out where the heading has already fallen back to the code. Unset,
  or with no hero facts left after the blank ones are dropped, draws no bar at all. `DetailFacts`
  takes an `Id` on the same change, because the bar appears when that row leaves the viewport and
  the browser needs an element to watch. (Fhi.Metadata-35w0p.28)
- **The package's browser module gains its first two exports, `observeHeroFacts` and
  `disconnectHeroFacts`.** One `IntersectionObserver` per mounted detail page, keyed by that page's
  own bar, so two explorers on one host page cannot fight over one bar. It shows the bar only when
  the hero row has left the viewport **upwards** — a row still below the fold, which is how every
  page load starts, leaves the bar hidden. Nothing about the scroll reaches the server: on a legacy
  Blazor Server circuit a scroll-driven callback would be a round trip per frame. One limit comes
  with the observer and is worth knowing: it notifies on a crossing, so a reader who JUMPS past the
  hero row — an in-page anchor, the contents nav's own links included — gets no notification and so
  no bar until they scroll. Fhi.Metadata-14j7i is that, and this change does not fix it.
  (Fhi.Metadata-35w0p.28)
