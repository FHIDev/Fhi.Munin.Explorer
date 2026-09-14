category: Added
- **The three detail views open with page chrome: an eyebrow, a breadcrumb and an action row.**
  `DetailPage` draws all three above the name block, so the kilde, datasamling and variable views
  get them at once. The eyebrow names what kind of thing the page is about — Datakilde,
  Datasamling, Variabel — and is a `<p>`, never a heading, so the page's outline is the one it
  already had. The action row is a slot: it is emitted only when a caller fills it, and nothing in
  this package fills it yet. `VariableListView` is untouched; its chrome is its own work.
  (Fhi.Metadata-35w0p.49)
- **`KildeView`, `DatasamlingView` and `VariableView` take a `Trail`, and `DetailTrailStep` is what
  goes in it.** The steps above the page, outermost first; the view appends its own name as the
  last step, which is marked `aria-current="page"` and is never a link. **Every step's target is
  yours to supply** — this package has no router and knows no addresses — and a step whose `Href`
  is null renders as plain text rather than as a link that goes nowhere. Pass no trail at all and
  none is drawn: a trail whose only step is the page itself names nowhere to go. The markup is a
  `<nav>` with an accessible name around an `<ol>`, wearing helsedata's own `breadcrumbs` class
  names. (Fhi.Metadata-35w0p.49)
- **The kildeutforsker wires that trail for real: Kildeutforsker › kilde › datasamling.** Both
  targets come off the host's own address through `KildeExplorer`'s `UrlMirror`, the same source the
  drill-in link already used, and the step back to the list keeps the order the reader chose. The
  kilde's name on a datasamling page comes off that payload's own `parentKildeNavn`, so the trail
  costs no second request. The variable explorer's drill-ins get no trail: opening one there is
  component state and not an address, so there is no target to offer. (Fhi.Metadata-35w0p.49)
- **`KildeSearch` takes a `KilderHref`.** The address of the kilde list, for the trail's first
  step. Unset, `KildeSearch` draws no trail; `KildeExplorer` sets it for you. (Fhi.Metadata-35w0p.49)
