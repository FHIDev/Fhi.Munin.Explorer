# Changelog

Notable changes to the published package. This file is for **consumers** — what changed in
`Fhi.Munin.Explorer`, which carries the component, the client that feeds it and the types they
share, and what a host has to do about it. (`.Blazor`, `.Client` and `.Contracts` are namespaces
inside it, not separate packages.) Internal repository housekeeping belongs in commit messages,
not here.

Versions follow [semver](https://semver.org/). While on `0.x` the API surface may still move;
we stay below `1.0.0` until a consuming host is live and the surface has settled. Once at
`1.0.0`, a breaking change means a new major with a deprecation window — a package a partner
service embeds cannot move under them without warning.

**Unreleased changes are not in this file.** Each one lands on its branch as its own file in
[`changelog.d/`](changelog.d/README.md), and `scripts/assemble-changelog.ps1` folds them in under
a version heading — run by `.github/workflows/release.yml` when a `v*` tag is pushed, not by hand.
One file per change means two PRs in flight never conflict over this one. To see what is queued
for the next release, read `changelog.d/`.

The package is published to `Fhi.Helsedata.no`, helsedata's internal Azure Artifacts feed, and
not to nuget.org — restore it from there.

The eight `0.1.0-alpha.*` sections below were written in one go on 2026-09-04, because the
assembly step was documented and never run: eight versions shipped and 166 fragments piled up
behind them (`Fhi.Metadata-l9l2n.44`). They are backfilled per version rather than folded into
one, since which release a fragment shipped in is not a guess — it is the first tag whose history
contains the commit that added the fragment. A host bumping alpha.7 to alpha.8 needs the entry
under alpha.8, which is the whole reason this file exists.

<!-- assemble-changelog: new version sections are inserted directly below this line, newest first. -->

## 0.1.0-alpha.14 — 2026-09-23

### Added

- **A reader can mark individual datasamlinger across several kilder in `KildeExplorer` and
  explore the variables in exactly those.** Every expanded row's datasamling table gains a leading
  checkbox column, each marked row shows a count under its kilde's name, and the selection bar
  counts the marks beside the ticked kilder. The marks ride in the address as a repeated
  `?selectedDatasamling=<kildeId>:<datasamlingId>`, so they survive going back and forth between
  the two explorers and are gone with the link — nothing is kept in `sessionStorage` or
  `localStorage`. With any mark present the handover travels as `datasamlingIds` alone, each
  ticked kilde expanded to all of its own datasamlinger: Munin's API ANDs `kildeIds` with
  `datasamlingIds`, so sending both would drop every variable pinned into another kilde's
  datasamling. A ticked kilde holding no datasamling is named in a note beside the button, which
  also says that variables in no datasamling are out of reach of such a selection. A selection
  with no marks is unchanged. (Fhi.Metadata-75yov, sak #6098)
- **`DatasamlingDetail` reads `sistOppdatertKildesystem`, which the datasamling endpoint had been sending unread** - `SourceSystemLastUpdated` is the day the source system last changed the datasamling, a `DateOnly?` because the API sends a bare date such as `"2026-05-11"` with no time or offset, and a `DateTimeOffset` would invent a UTC midnight that reads as the day before west of UTC. It is null when the API omits it. `DatasamlingView` does not draw it yet: the contract carries it so a host reading `IMuninExplorerClient` directly gets it. (Fhi.Metadata-c9x90)
- **A variable names the instrument it was collected with, and the instrument gets a page of its
  own.** `VariableDetail.Instruments` carries the questionnaires and scales the catalogue links a
  variable to, and both the whole-variable view and the result row's panel list them, each name in
  the reader's language and each linking to `?instrumentId=<id>`. That address opens `InstrumentView`
  in place of the result list — code, name, description, validity and the catalogue's own properties
  — with a link on to the variables collected with the instrument. `IMuninExplorerClient` gains
  `GetInstrumentAsync`, which has a default body answering null, so a host implementing the
  interface itself keeps compiling. The instrument page needs Munin's own
  `GET api/explorer/instrument/{id}`, which ships in the Munin release that adds the instrument to a
  variable's detail; against an API older than that the page says the instrument was not found, the
  list on a variable is empty, and nothing else changes.
  (Fhi.Metadata-hkf58)
- **A saved variable list can be shared as a six-character code and opened from a code or a
  `?delekode=` link.** `VariableListView` gains a "Del liste" disclosure beside rename and delete:
  it posts every variable of the list on screen to `POST api/explorer/lists/share` and shows the
  code, a link when the view has an address to put it in, a mailto link and the 90-day validity.
  The private "Ønskede data" annotation is left out of what is posted, since anyone holding the
  code can read it. An "Åpne delt liste" field opens a code, and a shared list is shown read-only
  under its own name with the eyebrow "Delt liste": the same table and pager, no remove buttons,
  no desired-data fields, and two actions — "Lagre som min liste", which refuses a name the reader
  already uses (trimmed, case-insensitive) before any write, and "Lukk delt liste". Signed out, a
  shared list is shown read-only with a sentence asking the reader to sign in to save it, and no
  `my/lists` call is made. The snapshot format is Runa's, so a code made in either frontend opens
  in the other. On an empty list "Del liste" is `aria-disabled` with a visible reason.
  (Fhi.Metadata-ntpbd.1)
- **`IMuninExplorerClient` gains `ShareListAsync` and `GetSharedListAsync`, and
  `ExplorerUrlState` gains `ShareCode`.** `GetSharedListAsync` answers null for an unknown,
  expired or malformed code and sends no request for a code that is not six ASCII letters or
  digits; it returns a new `SharedList` record. Both members carry default bodies that throw
  `NotSupportedException`, so a host implementing the interface itself still builds.
  `VariableListView` gains `ShareCode`/`ShareCodeChanged` (bindable) and `SharedListHref`;
  `VariableSearch` gains `ShareCode`, which draws the Variabelliste tab while a code is present,
  signed out too. (Fhi.Metadata-ntpbd.1)
- **`KildeSearch` takes `@bind-Search`, `@bind-FacetChoices`, `@bind-TickedKildeIds` and
  `@bind-VisibleColumns`, beside `@bind-Order`.** Each is read once when the list opens and raised
  on every change, so a host that mounts `KildeSearch` itself can keep the list's state in its own
  address as `KildeExplorer` does. `KildeSearch.FacetKeys` and `KildeSearch.ColumnKeys` name the
  values. A host that binds none of them sees no change. (Fhi.Metadata-nvf2w)

### Changed

- **`KildeSearch` takes `@bind-MarkedDatasamlinger` and `ExploreDatasamlingerRequested`.**
  `ExploreVariablesRequested` keeps its signature and still carries kilde ids only, so a host
  composing `KildeSearch` itself is untouched. The datasamling column is drawn only where both
  callbacks are wired — a mark has nowhere to go without the second — and so is the whole of the
  mark half: without `ExploreDatasamlingerRequested` a `MarkedDatasamlinger` a host passes in seeds
  nothing, counts in no bar and opens no row, rather than building towards a handover that would be
  refused. At most twenty marked rows open themselves on the first render whatever the address
  holds, and only for kilder the list has and says hold a datasamling: each one is a catalogue
  request charged to the rate-limit window helsedata's cluster shares, and the query is untrusted.
  The marks past that bound are still held, still counted and still travel.
  `KildeExplorer.MarkedQueryKey` names the query key it mounts them from.
  (Fhi.Metadata-75yov)
- **The kilde table no longer prints each kilde's code under its name; Kode is an optional column,
  off by default.** The code under every name was noise in the list (sak #6076). It is now the
  first column in the column picker and starts unticked; turned on, it is a plain cell straight
  after Navn. Searching on a code still finds the kilde whether the column is on or off. No new
  class name, and the default table's column count is unchanged. The scroll box's column-count
  modifier now runs one higher: with every column on it reads sixteen with the selection column and
  fifteen without. The stylesheet's width thresholds stop at fifteen with and fourteen without, so
  that widest table has no threshold rule. It keeps the base box's own horizontal scroll at every
  width instead of opening out and pinning its header on a very wide container.
  (Fhi.Metadata-ffudq)
- **A dataperiode with an unknown start now reads "? – <slutt>" everywhere, and a missing date
  never reads as the year 1.** The result row, the panel's period bar, the saved-list cell and the
  variable page each composed the range themselves and disagreed: three wrote "?" for a missing
  start where the shared helper let the end stand alone, and three drew `default(DateTimeOffset)`
  as 1. jan. 0001 where the helper read it as no date. All four now call `CatalogueDate.Period`,
  which writes the "?" — an explicit question mark says the catalogue gave no start, where an end
  standing alone reads as a start and a bare dash reads as a value that failed to draw. The same
  reading now applies to every other field drawn through that helper: a kilde's and a datasamling's
  validity, a kilde's dataperiode, and a datasamling membership in the open panel. The panel's
  coverage bar follows the words: the track beneath them is drawn only where the start is known,
  since a full track beside "?" illustrates a coverage nobody measured. A period with neither end
  still says "Ikke oppgitt", and an open end still says "Pågående", whichever way the
  payload carries the absence. (Fhi.Metadata-msax9)
- **A kilde's detail page no longer repeats its kildetype in the name block.** The badge under the
  name is gone; the hero fact row's first fact, **Type datakilde**, carries it there, as in the
  mockup. The Source information section still lists it, unchanged. (Fhi.Metadata-pnn4w)

### Fixed

- **A kildekodeverk with no name lists its codes one per line instead of as one run-on line.**
  Where a kildekodeverk link has codes but no name, `VariableSearch` shows up to eight of them in
  the name's place; they were drawn as a single line joined by " · ", which read as one bold
  sentence (Munin sak #6132). They are now a plain `<ul>` with one `<li>` per code, still marked
  `lang="no"`. A `<ul>` cannot sit inside a `<p>`, so in this case only the element carrying
  `munin-explorer-kodeverk__name` is a `<div>` rather than a `<p>`; the class is unchanged, no
  class name is added, and a named kodeverk renders exactly as before. "Vis alle (N)" still opens
  the full list when there are more than eight codes. (Fhi.Metadata-0ajsy)
- **The sticky fact bar now follows an in-page jump, not only a scroll.** An `IntersectionObserver`
  reports a crossing, so a press in the contents nav — or any single `window.scrollTo` past the hero
  fact row — left the bar exactly as it was: no bar deep in the page, or a pinned copy of the page
  title over a page already showing its own. The module re-reads the row's position directly on
  `scrollend` and on `hashchange`, so hosts need change nothing. (Fhi.Metadata-14j7i)
- **Datasamling pages show inherited inclusion and exclusion criteria.** The page uses the
  API's resolved criteria, falling back to the collection's own text for older responses.
  Criteria inherited from a delkilde or kilde now appear instead of being silently omitted.
  The text appears under About the data collection, with a subordinate heading and no separate
  contents entry. Existing links to the criteria still reach the text.
- **Quality notes no longer repeat their section heading as a visible field label.** A lone authored field with the same label as its section keeps its accessible definition-list term while showing the heading only once. Sections with several fields, including appended source and statistics facts, or different labels retain their visible field labels.
- Keep the datasamling quality note inside About the data collection, alongside the criteria,
  without a separate contents entry. Preserve its catalogue heading, content and fragment target.
- **A link to a section of an open kilde or variable can be copied out of the address bar again.**
  Pressing an entry in a detail view's contents nav — or arriving on a link that names a section —
  left the fragment in the address for only as long as it took the explorer to mirror its state
  over it, so the reader scrolled to the right place and then held a link to the top of the page,
  and Back did not return to the section they came from. Both explorers now keep the incoming
  fragment for as long as the view it names is the one on screen, and drop it once the reader
  presses through to a different kilde, datasamling, search or sort. In the kildeutforsker Back
  returns to the section as well, because a navigation arrives with an address of its own whose
  fragment is honoured in its turn; the variabelutforsker reads the address once, at initialisation,
  so a fragment it has already dropped does not come back.
  Links the explorers build are unaffected and
  still carry no fragment: one naming a section of the view being left would name nothing in the
  view the link opens. (Fhi.Metadata-7np6k)
- **The Status column in variable search now reads in the reader's language.** The result row
  wrote the API's raw token, so a Norwegian reader who turned the column on — or who included
  historical variables, which puts it on screen by itself — read "Active" and "Historical" beside
  Norwegian headers. It now goes through the same `Texts.VersionStatusLabel` the whole-variable
  page uses, giving "Aktiv" and "Historisk" in Norwegian and "Active" and "Historical" in English,
  whatever case the token arrives in. A token the map has not seen is still shown as it arrived,
  and a variable with no status still reads "Ikke oppgitt". That map and the statistics-type map
  beside it now take either spelling of every word they know, so neither half of a pair can
  translate while the other passes through raw — though only the PascalCase English tokens have
  been observed coming back from the API. (Fhi.Metadata-hq0b6)
- **A sort on Kode or Status shows that column** - Both columns start off, so `?sort=Code` or `?sort=Status` used to open a list ordered by a column that was not on screen, with no header carrying `aria-sort`. `VariableSearch` now shows the column its `Sort` names, both on the first render and when a host changes `Sort` afterwards, and the column picker reads it as shown. A Status column shown this way is handed back to «Vis historiske» once that filter is in force. A `Sort` changed after the first render now also reorders the list, which it did not before; one that arrives while a fetch is in flight is followed once that fetch lands, and if its own fetch fails the old order stays and `SortChanged` and `DirectionChanged` report it back to the host. A `Direction` changed on its own is not followed. Hiding the column through the picker still wins: the order stays, the header goes, and a later `Sort` from the host does not bring the column back. (Fhi.Metadata-jqarq)
- **The whole-variable page no longer draws an empty bullet for a variabelgruppe the catalogue
  left unnamed.** Its Variabelgrupper list read the payload's own list while the Datasamlinger
  list below it asked a shared predicate, so an unnamed group drew a bullet with nothing beside
  it, and a variable whose groups were all unnamed got a heading and a contents entry over empty
  bullets. Both the list and its contents entry now come from one predicate, which drops the
  unnamed groups and falls back to the primary group's name when that leaves none — the reading
  the drill-in panel already had, now shared rather than copied. (Fhi.Metadata-jrgnt)
- **The list picker in the saved-list view no longer wears the facet panel's fold handle.** Its
  `<label>` carried `munin-explorer-filters__facets`, which is the name the two facet panels fold
  behind — the one a host keys its `[hidden]` and `> [role="group"]` rules on. Nothing about this
  label folds and it is never `hidden`, so neither of the two rules the sample stylesheets key on
  that name reaches it; whether a rule of yours does is worth a look, since a host's own
  stylesheet is the one thing this repository cannot read. The cost was that any change to how a
  host draws the handle would move or hide a control in an unrelated view, and that an audit of
  where the handle is used found a use that is not a fold. The label now carries no class of its
  own and still wraps the `<select>` it names; what draws it is the action row, whose
  `.munin-explorer-page__actions > label` rule is untouched and was doing the work all along. No
  `munin-explorer*` name is added, renamed or removed: `munin-explorer-filters__facets` is still
  the fold handle, now with two users instead of three. (Fhi.Metadata-l9l2n.119)
- **The chevron that opens a kilde row or a variable row now emits the icon class of the glyph it
  actually draws.** Collapsed is `icon-keyboard-arrow-down` and expanded is
  `icon-keyboard-arrow-up`, on both `KildeExplorer`'s kilde table and `VariableExplorer`'s result
  rows. On `Fhi.Helsedata.Stiler` 0.1.91 or later nothing moves on screen — the collapsed chevron
  pointed down and the expanded one up before this too, and the rules keyed on
  `[aria-expanded=false]` that 0.1.91 added are what keep it that way. What changes is that the
  markup says so: the collapsed state used to emit `icon-keyboard-arrow-right` and rely on Stiler
  mapping that name to the downward glyph, so a reader of the markup and a reader of the screen
  disagreed and neither could tell which was wrong without opening the other repository. The
  chevron keeps its `munin-explorer-kilder__expand-icon` /
  `munin-explorer-dataitem-main__expand-icon` class and its `aria-expanded`, and no
  `munin-explorer*` name is added or renamed. (Fhi.Metadata-l9l2n.84)
- Datasamling identification labels now agree between the summary, detail facts and complete
  record when an obsolete numeric code remains in the property bag. The catalogue's label for
  the effective value is used in both languages; explicitly defined or curated values are preserved.
- **`KildeExplorer` keeps the kilde list's search, facets, ticks and columns in the address, so a
  round trip away from the list comes back to it as it was left.** The "Kilder" crumb over an open
  kilde, "Tilbake til kilde" out of a datasamling and browser Back after "Utforsk variabler for
  utvalget" all used to land on an empty list. The list's state is now written with
  `history.replaceState` as `?search=`, one repeated key per facet (`?kildetype=`, `?kategori=`,
  `?tilgangsniva=`, `?databehandler=`), `?columns=` and `?selected=` — the names Munin's own Kelda
  uses — and every link `KildeExplorer` builds carries it, so a copied link opens the same view. A
  key at its default is not written, and a bare address still opens an untouched list. Nothing is
  kept in `sessionStorage` or `localStorage`. (Fhi.Metadata-nvf2w, sak #5689)
- **A section the catalogue places a column-backed identity value in is no longer drawn empty** - A kilde's name, code, short name and kildetype, a datasamling's name, code and short name, and a variable's name are now merged into the values the detail pages draw placed sections from, the same way the description and legal basis already were. Where the page already shows one - the title, the identifier line under it, or the kilde's Kildetype row - it is not repeated as a section row. (Fhi.Metadata-zg89n)

### Notes for hosts

- **`munin-explorer-kodeverk__name` can now be a `<div>` holding a `<ul>`, and needs no new rule.**
  Where a kildekodeverk has no name and its codes are shown instead, the element wearing the class
  is a `<div>` with a plain `<ul lang="no">` inside, one `<li>` per code; everywhere else it is the
  `<p>` it was. The existing class rule (`margin: 0; font-weight: 600` in Stiler) applies to both.
  A host stylesheet that selects it as `p.munin-explorer-kodeverk__name` misses the code list, and
  the `<ul>` takes the host's own list defaults. (Fhi.Metadata-0ajsy)
- **Nested criteria retain fragment navigation clearance.** Style `munin-explorer-page__anchor`
  with the same responsive `scroll-margin-top` as main detail sections (140px desktop,
  80px through 767px in Stiler). This target deliberately has no `data-nav-section`, so a
  criteria link clears sticky headers without adding a contents entry. Stiler support is
  supplied by Stiler 0.1.103 and later (PR 39519, `Fhi.Metadata-17k34`).
- **`KildeExplorer` now owns `?selectedDatasamling=` on the page it is mounted on, and two new
  class names need rules.** `munin-explorer-kilde__datasamling-select` is the drawer's checkbox
  cell and `munin-explorer-kilde__datasamlinger--selectable` the modifier its table wears; both
  ship in `Fhi.Helsedata.Stiler` 0.1.99 and later. Neither is drawn without
  `VariableExplorerPath`. A host on an older Stiler gets the column at browser defaults, and — the
  half worth knowing — a table whose other four columns are each sized for the one beside them,
  because the modifier is where those positional rules are re-anchored. A host that means
  something else by `?selectedDatasamling=` on that page mounts `KildeSearch` and owns the query
  string itself. (Fhi.Metadata-75yov)
- **Stiler does carry a breadcrumb rule, and the detail pages' trail wears it; the two search
  trails deliberately do not.** The documentation shipped with the package said Stiler had no
  breadcrumb rule that could be read back off its compiled stylesheet, and gave that as the reason
  the hierarchy trail over the results and the variable panel's kilde trail carry no class. Stiler
  defines `.breadcrumbs` — with `__list`, `__list-item`, `__homelink`, `__divider` and
  `__last-crumb` — as global unscoped classes, and `DetailTrail` wears the list, list-item, divider
  and last-crumb names. The real reason those two trails stay unclassed is that their steps do not
  navigate: the hierarchy trail's steps narrow the search, and the kilde trail's one button
  discloses the kilde in place, so the breadcrumb vocabulary does not describe either. No markup
  changed: both trails emit the same
  unclassed `<ol>` as before, and the chevrons between their steps are still a host's to draw.
  (Fhi.Metadata-7uqq8)
- **`VariableExplorer` now owns `?instrumentId=` on the page it is mounted on, and no new class
  name comes with it.** The key opens one instrument's page, and it is declinable like every other
  scalar in `ExplorerUrlState.ScalarQueryKeys` — a host that already means something else by it
  passes it in `DeclinedKeys`, or mounts `VariableSearch` and owns the query string itself.
  Declining it draws a variable's instruments as plain words rather than as links, because the
  whole of an instrument's address is that one key and there is nowhere else for it to go. The
  instrument page is built on the `munin-explorer-page` chassis alone, so every rule it needs is
  one `Fhi.Helsedata.Stiler` already carries. A host composing `VariableSearch` itself supplies the
  two addresses, because only it knows where the explorer is mounted: without `InstrumentHref` a
  variable's instruments render as plain words rather than as links that go nowhere, and without
  `InstrumentVariablesHref` the instrument page draws no Variables section. Both must be supplied
  inside the interactive boundary, not from a static SSR parent. (Fhi.Metadata-hkf58)
- **A host styling the row chevrons needs `Fhi.Helsedata.Stiler` 0.1.91 or later.** 0.1.91 is
  where the rules keyed on `[aria-expanded=false]` landed, and they are what tell a collapsed
  `icon-keyboard-arrow-down` from an expanded one while the older, inverted overrides for the
  previous names are still in the stylesheet. On an earlier Stiler the collapsed chevron is drawn
  by one of those overrides and points up. A host supplying its own rules for
  `munin-explorer-kilder__expand-icon` or `munin-explorer-dataitem-main__expand-icon` should key
  them on the class names above rather than on `icon-keyboard-arrow-right`, which the component no
  longer emits — and, if it draws them from images rather than from Stiler's icon set, owes the
  expanded chevron a resting rule for `icon-keyboard-arrow-up` as well. Stiler needs none, because
  its own `.icon-keyboard-arrow-up` draws that glyph and only the deviations from it are scoped to
  these names; both sample stylesheets, which carry no icon font, now show the extra rule.
  (Fhi.Metadata-l9l2n.84)
- **`VariableExplorer` now owns `?delekode=` on the page it is mounted on.** The key is in
  `ExplorerUrlState.QueryKeys` and `ScalarQueryKeys`, so a host that already means something by it
  can pass `DeclinedKeys: ["delekode"]`; the explorer then neither reads nor writes it and offers
  no link beside a share code. Whether a host's sign-in returns the reader to the same address,
  and so keeps the code, is the host's. No new class name: the share and shared-list controls
  wear names already styled. (Fhi.Metadata-ntpbd.1)
- **`KildeExplorer` now owns `?search=`, `?kildetype=`, `?kategori=`, `?tilgangsniva=`,
  `?databehandler=`, `?columns=` and `?selected=` on the page it is mounted on.** Until this
  version it carried `?search=` through untouched. A host that means something else by one of
  these keys on that page mounts `KildeSearch` and owns the query string itself.
  (Fhi.Metadata-nvf2w)
- **`munin-explorer-kilde__kildetype` is no longer emitted by any component.** A host with a rule
  for it can drop that rule — it now matches nothing, on this package's own pages and inside the
  variabelutforsker's drill-in panel alike. The kildetype is drawn by the hero fact row instead,
  which wears `munin-explorer-page__facts` and is already styled; nothing new needs a rule, and both
  sample stylesheets have had the badge rule removed. `Fhi.Helsedata.Stiler` still carries its copy,
  which is harmless and is removed separately. (Fhi.Metadata-pnn4w)
- **The hierarchy's node icons are not meant to be coloured per datakategori.** `ShowNodeIcons`'s
  documentation and the README said a host stylesheet was what made a category recognisable at a
  glance. That was never the delivered design: helsedata's Stiler gives no datakategori a colour of
  its own, since the shape and the spoken words already tell them apart, and mutes only the delkilde
  folder (`data-node-icon="kilde"`). The same docs said the glyph draws in front of the name: the
  package writes the slot there, and helsedata's stylesheet paints the hierarchy's after the name
  and count, the facet panel's before it. Nothing in the markup changed.

## 0.1.0-alpha.13 — 2026-09-22

### Added

- **The contents nav on a detail page now marks the section the reader is in.** As the reader
  scrolls, the package's browser module sets `aria-current="location"` on the one entry whose
  section has reached the line a jump to it lands on: its `scroll-margin-top`, plus the scroller's
  `scroll-padding-top`. It removes the attribute from every other entry. At the bottom of the page
  the last entry is marked, unless the reader just pressed one of the sections that cannot scroll up
  to that line. Nothing is sent to the server while the reader scrolls. A host that does not serve
  the module still gets the nav, with no entry marked. (Fhi.Metadata-35w0p.15)
- **Datasamling detail links to the collection's filtered variables.** The Variables section uses the host's existing `VariableExplorerPath` or the current variable search; standalone hosts can supply `VariablesHref` or an interactive `ShowVariables` callback. No variable table or extra detail fetch is required.
- **Kelda draws a proportion bar under each non-zero variable count.** In the result table's
  Variabler column only, a small bar under the digits shows the kilde's count as a share of the
  largest variable count among the rows currently drawn, so a reader can compare sources at a
  glance. A count of 0 gets no bar and still reads a dimmed "0"; the bar is `aria-hidden`, and the
  digits stay the cell's text. Delkilder and Datasamlinger are unchanged. (Fhi.Metadata-35w0p.30)
- **Both explorers take a `Lede`, a sentence the host writes to say what the page is for.** A
  string parameter on `KildeSearch` and `VariableSearch`, forwarded by `KildeExplorer` and
  `VariableExplorer`, drawn as plain text directly under the explorer's title. The package ships no
  default text in either language, so the host owns the wording and can change it without a new
  release. Left null or blank, nothing is drawn: no empty paragraph, no placeholder. The variable
  lists tab gets no lede of its own. (Fhi.Metadata-35w0p.32)
- **An expanded kilde row in the kildeutforsker now ends in a link to that source's variables.** A
  reader who has just opened a source's drawer, seen its datasamlinger and decided it is the one had
  no control that took them onward — they had to go to the variable explorer and filter by kilde
  themselves. The drawer now ends in "Vis alle variabler i" and the source's name, and it is drawn
  only where the host has said where a variable explorer is: `KildeExplorer` offers it wherever
  `VariableExplorerPath` is set, off the same path as the selection handover it already drove, and a
  host mounting `KildeSearch` itself wires the new `KildeVariablesHref` parameter —
  `Func<Guid, string>?`, the shape `DatasamlingHref` has over an id that is always there, answering
  an address for one kilde. Unset, no link is rendered: not a dead href, not an inert button. It is
  the single-source shortcut and does not replace "Utforsk variabler for utvalget", which acts on
  the rows the reader has ticked. (Fhi.Metadata-35w0p.33)
- **Every column of the variable table's header now sorts.** Kode, Datatype, Status and
  Dataperiode join Navn, Kilde, Datasamling and Variabelgruppe, so `SortField` gains `Code`,
  `DataType`, `Status` and `DataPeriod`. **Their four wire tokens need a Munin API carrying
  Fhi.Metadata-0ayti**; an API older than that does not recognise them and falls back to its own
  default order silently, leaving the header announcing an ordering the list is not in. Half a
  table of headers responding to a press with nothing to tell the two halves apart is worse than
  none of them responding, which is why the four were never an optional half of this.
  Dataperiode orders by the START of the period the data covers — not by the range as it is
  written, where "1999" would file after "2021 – Pågående", and not by the version's validity
  window, which is a different fact about the variable. A variable with no datatype or no period
  start comes last whichever direction is asked for. Datatype and Status order by the catalogue's
  own code and the API's own status rule rather than by the words on screen, so neither moves when
  the reader switches language. Ordering stays the API's throughout: nothing in this package
  compares two rows, so a Norwegian name sorts in the catalogue's collation rather than in
  whatever culture the host's thread carries. (Fhi.Metadata-35w0p.37)
- **Every kildeutforsker facet now says how many values it has.** A facet's summary reads
  "Databehandler 24 verdier" — heading, then the facet's size, then the ticked count when there is
  one ("Kildetype 2 verdier 2 valgt"), so a folded facet tells the reader whether opening it shows
  four values or forty. The size is the whole facet's, and ticking, the facet's own search and
  "Vis N til" do not change it. It is a separate `<span>` from the ticked count, each with its own
  unit word ("1 verdi"/"N verdier", "1 value"/"N values") and a space between them, so the two read
  as two facts to a screen reader and to the eye. It wears the existing
  `munin-explorer-filters__groupcount`, a direct child of the `<summary>` beside `__chosen`, whose
  `Fhi.Helsedata.Stiler` rule it shares — no new class name and no Stiler release needed. The
  text is a new `Texts.FacetSize` entry. The variabelutforsker's summaries are unchanged.
  (Fhi.Metadata-35w0p.53)

### Changed

- **`KildeView` draws its sections in the order the API places them, property sections and built-in
  ones in one pass.** The page used to emit every curated property group under one "Metadata"
  heading and then a fixed run of Datasamlinger, Kildeinformasjon and Statistikk, which could not
  put a built-in section between two property ones — the order the kilde mockup asks for. It now
  reads the new `sections` collection on `GET /api/explorer/kilder/{id}` and renders what it names,
  so reordering the page, renaming a property section or moving a property between sections is an
  edit a Munin curator makes rather than a release of this package. A built-in section keeps the
  word this package has for it — `groupTranslations` on a placement row is read by no view here, so
  renaming Datasamlinger is still a release. Each placed property group becomes a section of its
  own, anchored at its catalogue group key under a `section-` prefix and listed in the contents
  nav.
  Kildeinformasjon and Statistikk are named by no mockup and reserved by no seed yet: they keep
  their sections and every field in them, drawn after the placed sections, and each carries a key
  so the first placement that names it moves it with no release here. Against an API that sends no
  `sections` — every environment Munin has not migrated — the page draws exactly as it did before.
  (Fhi.Metadata-35w0p.22)
- **Datasamling pages offer working actions below their title and source trail.** The header and
  compact bar reuse the Variables section's collection-filtered destination. The source action
  opens the collection's parent in either explorer. Standalone mounts can supply `KildeHref` or
  the interactive `ShowKilde` callback; absent targets and zero-variable actions stay omitted.
- **The Kodeverk section opens with a sentence saying what kind of kodeverk the reader is looking at, and
  the statistics heading writes its kind in lower case.** The kodeverk block, on the whole-variable page and
  in the result row's Data tab, starts with one sentence per kind the variable carries, in the order the
  payload names them: "Kildekodeverket er verdiene slik de er registrert i kildesystemet.", "Administrative
  kodeverk er nasjonale kodeverk." and "Helsefaglige kodeverk er nasjonale kliniske kodeverk og
  terminologier." The statistics heading reads "Statistikk (årsbasert)" / "Statistikk (akkumulert)" where it
  read "Statistikk (Årsbasert)", on the variable and datasamling pages, in the Data tab and in the contents
  nav. (Fhi.Metadata-35w0p.24)
- **Runa's variable name now opens the whole variable, and a separate chevron opens the row panel.**
  This changes learned behaviour on a shipped surface: a reader who presses a variable's name used
  to get the inline panel under the row, and now gets the whole-variable view in place of the list,
  with "Tilbake til variabler" putting the list back with the same row open or shut as before. The
  panel moves to a chevron button, in a cell of its own first in each row, which carries `aria-expanded` and
  `aria-controls`; the name carries neither. Pressing the row strip still opens and closes the
  panel, as Kelda's row does. It reverses Fhi.Metadata-zqe14, which had put the chevron inside the
  name button, so that the two explorers split their gestures the same way. (Fhi.Metadata-35w0p.34)
- **`SortField`'s existing members are renumbered — rebuild against this version.** The four new
  members are inserted at their own columns rather than appended, because the enum is declared in
  the order a UI should offer the orders in and a host is free to build its control from
  `Enum.GetValues`. Source compatible, so a rebuild is the whole of it: `SortField.Kilde` still
  names the kilde order, and a link carrying `?sort=Kilde` still reads back as one, since the URL
  and the wire both carry a name rather than a number. A host that has stored the underlying
  `int` has to remap it. (Fhi.Metadata-35w0p.37)
- **The variable explorer's kilde filter says its search box also finds a datasamling.** The box
  has always matched any node below a kilde — delkilder, datasamlinger and variabelgrupper — and
  opened the branches down to the match, but its placeholder was the generic "Søk i verdiene",
  which left that the one route a reader who knows the datasamling and not its kilde could not
  guess at. It now reads "Søk etter kilde eller datasamling" / "Search for a source or data
  collection". The kildeutforsker's facet boxes are unchanged: they search their own facet's
  values, and keep the shared wording. (Fhi.Metadata-35w0p.38)
- **The result row's "Lagre i liste" button is quieter.** It wears
  `button-square--ghost-blue`, the variant the saved-list view's own buttons already use, rather
  than the filled `button-square--secondary`. It is drawn once per result row, so a default page
  of 20 results drew 20 primary-weight controls competing with the variable names being scanned.
  Ghost-blue draws no border either, which is what took the button off `button-square--ghost` in
  the first place (Fhi.Metadata-q7i5e) — but that variant's text is `--dark`, the row's own
  colour, so it read as bold prose, while ghost-blue's is `--primary`, the colour this package
  already relies on to mark a control the reader can press. Saved and unsaved stay told apart by
  their words and by `aria-pressed`, not by the colour. No new class name and no new rule: both
  variants are Stiler's. (Fhi.Metadata-35w0p.64)
- **A datasamling page places its inclusion and exclusion criteria where Munin's placement rows put
  them, instead of always after every other section.** The criteria block now answers to the
  built-in key `inklusjons-og-eksklusjonskriterier`, which Munin seeds on DatasamlingDetalj at band
  2000 (Fhi.Metadata-87tng), so an untouched page reads Om datasamlingen, the criteria,
  Kvalitetsnote, Variabler, Datakilde, Alle metadatafelt, and a curator's reorder in Sideoppsett
  reaches the page without a release here. The section's id stays `criteria`, so existing deep links
  still land. A payload with no placement row for the criteria, or no `sections` at all, draws them
  where it did before; empty criteria still draw no section and no contents link.
  (Fhi.Metadata-l9l2n.120)
- **A datasamling page now draws the sections Munin's placement rows declare, under their names and
  in their order, instead of one fixed "Metadata" block and three of the view's own.** Where the
  catalogue has placed a datasamling's properties, each of its sections is a section of the page —
  "Om datasamlingen", "Variabler", "Datakilde", "Alle metadatafelt" and whatever else the placement
  rows carry — so "Kildeinformasjon" is renamed to "Datakilde" and Kvalitetsnote gets a section of
  its own the day a row gives it one, without this package being changed again. The fact rows the
  placement did not take follow the fields it did into the same section rather than heading a second
  one about the same subject, so the source block's parent, kildetype and Munin timestamps sit under
  "Datakilde" and the variable count under "Variabler"; nothing is dropped. Ordering, ids and the
  fallback are the kilde page's, shared rather than written again: sections the rows name come
  first, in the order the API sent them, then the groups they name nowhere under the view's own
  "Metadata" heading, then the criteria, source and statistics blocks. A payload carrying no
  `sections` collection — an API predating the placement rows — renders exactly as before.
  (Fhi.Metadata-lr6yh)
- **Variabelgrupper are available in the collapsed kilde tree independently of the standalone facet.**
  Expand a kilde, delkilde or datasamling to reach its groups. Groups with `Filter="2"` remain
  selectable in the tree while being excluded as standalone facet options; an opted-out ancestor
  remains a container when an offered child needs it. Selection is shared across placements and
  produces one removable chip. Collapsing a branch preserves the selection.

### Fixed

- **The datatype and status cells in the result rows are no longer marked as Norwegian.** The
  datatype name arrives in the reader's language and the status is the API's token, so
  `lang="no"` had an English reader's screen reader pronounce them with a Norwegian voice. In the
  variable search both cells now inherit the host page's language; in the saved lists, which have
  no status column, the datatype cell does. The code, kilde, datasamling and variabelgruppe cells
  keep `lang="no"`, and the text shown is unchanged.
- **The datasamling summary now follows its mockup.** The hero shows source, source type, variables, validity, personal identification and the catalogue’s localized data category. Its compact bar repeats source, variables and validity. Controller and legal basis remain available in the body.
- **Category labels retain their fallback language.** Hero and sticky labels can carry a language marker independently of the value, so a Norwegian label on an English page is pronounced correctly.
- **A long filter facet no longer draws every value.** Both explorers' filter panels, and the
  saved-list panel beside them, now draw the first ten values of a facet and put the rest behind a
  "Vis N til" button — which is a real button with `aria-expanded`, reachable by Tab and operable
  by Enter and Space. Before this, a facet like Databehandler drew all 24 of its values and the
  filter panel grew longer than the results it filters. A value the reader has ticked stays on
  screen whatever its place in the list — including one ticked further down a kilde's own tree —
  and typing in a facet's own search box shows every match with the button withdrawn, so the two
  controls cannot hide a value between them. Utvid alle reaches past the cap and Skjul alle puts it
  back, so the control that offers to open everything still means it. A cap the reader lifted is
  lifted over the values they were looking at: a later search, or a switch to another saved list,
  that leaves the facet longer than it was puts the cap back rather than drawing the new, longer
  list in full. Ten is the threshold that already decided which facets get a search box, and all
  three panels now ask one predicate for it, so the package has one notion of a long facet rather
  than three. (Fhi.Metadata-35w0p.31)
- **A variable list is titled with its own name.** `VariableListView`'s heading now reads the name
  of the list on screen, and follows a switch or a rename, where it used to read "Mine
  variabellister" for every list; those words move to the eyebrow above it, as on the variable,
  kilde and datasamling pages. The heading keeps its level, id and class, and before the lists
  load or when there are none it still reads "Mine variabellister" with no eyebrow. The table and
  its scroll region are now named by the heading through `aria-labelledby` rather than an
  `aria-label` of their own. No new class name: the eyebrow is `munin-explorer-page__eyebrow`.
- **Every date the explorer shows a reader is now written the same way.** A kodeverk code's
  validity dates and the dataperiode chip in the filter panel wrote the culture's all-numeric
  short date — `01.01.2010` — while the same values elsewhere read `1. jan. 2010`, and the
  results list wrote a data period as month and year (`jan 1979 – des 2024`) over a variable page
  writing it as days (`1. jan. 1979 – 31. des. 2024`). All four now go through the shared date
  helper, so the day is spelled out with the ordinal dot in Norwegian and without it in English.
  The claim is about the day format and not about the whole range: a period with a missing start
  is still joined three different ways, which `Fhi.Metadata-msax9` settles rather than this. The
  ISO round-trip of the date-picker and of the API query string is unchanged. (Fhi.Metadata-ufmop)
- **A data period now shows the day the catalogue holds instead of rounding it to a month.**
  `dataFrom` and `dataTo` are day-precise at source — a `date` column computed from the ingest's
  `yyyyMMdd` `DataFra` string — and 212 of 1000 variables sampled off the live catalogue carry a
  start that is not the first of a month (`2016-06-17`, `2022-12-09`). `MMM yyyy` was discarding
  that day, and made `31. des. 2024` and `1. des. 2024` read alike. (Fhi.Metadata-ufmop)
- **Metadata values show their links and line breaks instead of printing `<br>` and `[label](url)` as
  text.** Twelve free-text keys the catalogue authors with markup (`Beskrivelse`, `BeskrivelseFlerspraklig`,
  `BeskrivelseEngelsk`, `Formaal`, `FormaalFlerspraklig`, `Kvalitetsnote`, `Innsamlingsmetode`,
  `InklusjonsOgEksklusjonskriterier`, `Forskrift`, `GeografiskAvgrensning`, `JuridiskNote`, `Kommentar`)
  now go through the same renderer as the page descriptions: links and line breaks only, everything else
  literal. It applies wherever a metadata section is drawn — kilde, datasamling and whole-variable pages,
  "Alle metadatafelt", and the variabelutforsker's row panel — so a variable's `Kommentar` also keeps the
  line breaks it was written with. The datasamling page's own Inklusjons- og eksklusjonskriterier section
  renders the same way. The renderer, descriptions included, now also draws a link inside a `- ` or `1. `
  list item (the marker stays literal; no list element is built) and a reference-style link (`[label]`
  with a `[label]: url` line; the definition line is drawn as text unless a link took its URL). A
  Lovverk written as one markdown link shows its words in the page's key facts and becomes a link in
  the source-information box. No new class names.
  (Fhi.Metadata-x0etk)

### Removed

- **A result row's variable name no longer carries a `title` tooltip.** It repeated the name
  word for word, because Stiler wraps the name onto as many lines as it needs. The data cells
  and column headers keep theirs, since those are still truncated. (Fhi.Metadata-vdnm4)

### Notes for hosts

- **The contents nav's mark needs Fhi.Helsedata.Stiler 0.1.89 or later.** Stiler draws the current
  entry from `.munin-explorer-page__toc li a[aria-current]`, first published in 0.1.89. On an older
  release the attribute is set and nothing looks different. The contents column also gains a per-instance
  id, `munin-explorer-contents-*`, which the module uses to find it. (Fhi.Metadata-35w0p.15)
- **Datasamling action placement changes while other detail pages keep their existing order.**
  The existing `munin-explorer-page__actions` row now sits between the datasamling identity block
  and hero facts. Its compact counterpart uses Stiler's existing desktop-only rule; no new class
  names are required. `DetailPage` offers `ActionsAfterHeader` and dedicated `StickyActionText`,
  `StickyActionHref` and `StickyAction` parameters, and never copies arbitrary `Actions` markup.
- **New class name `munin-explorer-lead` on the sentence that opens the kodeverk block, and
  Fhi.Helsedata.Stiler has no rule for it yet.** It is a `<p>`: directly after the Kodeverk heading on the
  whole-variable page, and the first element of the result row's Data tab. Both sample stylesheets give it
  `margin: 0 0 8px`, the declaration Stiler gives `munin-explorer-complete-record__lead` from 0.1.80; without
  a rule it takes the host's default paragraph margins. The Stiler rule is Fhi.Metadata-3z2s3.
  (Fhi.Metadata-35w0p.24)
- **New class names `munin-explorer-kilder__bar` and `munin-explorer-kilder__bar-fill` on Kelda's
  proportion bar, styled by Fhi.Helsedata.Stiler from the release that carries Fhi.Metadata-35w0p.68
  (commit 54a900f8).** The track `munin-explorer-kilder__bar` is an empty, `aria-hidden` span, the
  last child of a non-zero Variabler cell, directly after the digits; the fill
  `munin-explorer-kilder__bar-fill` is the one span inside it, with its width inline as
  `style="width:N%"`, a whole percent from 1 to 100. Stiler draws the track as a block under the
  digits so the column does not widen; set no width on the fill. On an older Stiler both spans are
  empty inline elements and draw nothing. (Fhi.Metadata-35w0p.30)
- **New class name `munin-explorer__lede` on the explorer's lede, styled by Fhi.Helsedata.Stiler from
  the release that carries Fhi.Metadata-35w0p.66.** It is a `<p>` and a direct child of
  `.munin-explorer`, immediately after the title `h2`, on Kelda and Runa alike, and only there when
  `Lede` holds text. Stiler gives it a top margin, a 65ch measure and, above 1024px, a full-width grid
  row of its own under the heading, opened by `:has(> .munin-explorer__lede)`. On an older Stiler the
  lede falls into the explorer grid's catch-all and lands in the results column beside the filters,
  so leave `Lede` unset until your Stiler carries the rule. A CMS host that picks parameters from a
  fixed list must add `Lede` to it, or the text is dropped before it is set. (Fhi.Metadata-35w0p.32)
- **New class names `munin-explorer-dataitem__expand-cell` and `munin-explorer-dataitem__expand-toggle`
  on Runa's row chevron, styled by Fhi.Helsedata.Stiler from the release that carries
  Fhi.Metadata-35w0p.72.** The chevron is a
  `<button class="hd-button-reset munin-explorer-dataitem__expand-toggle">` holding only the existing
  `munin-explorer-dataitem-main__expand-icon` glyph, inside a
  `<div role="cell" class="munin-explorer-dataitem__expand-cell">` that is the first direct child of
  `.munin-explorer-dataitem-main`. The cell is there because a table row may own only cells. Stiler
  sizes the button to 40x32, keeps it visible below 1280px where the row's other icons are hidden,
  draws its focus surface, and moves the header indent to 52px while it is present. On a Stiler
  without that release the glyph is hidden below 1280px and the button is an empty focusable box, so
  upgrade Stiler with this version. (Fhi.Metadata-35w0p.34)
- **Set `ApiBaseUrl` to the explorer host: `https://explorer.munin.skytest.fhi.no` in test,
  `https://explorer.munin.sky.fhi.no` in production.** It serves only `/api/explorer/*`, which is
  everything the package calls. `runa` and `kelda` still answer the same API today, but they are
  the two UIs' hostnames and may stop being reachable from outside FHI, so a host that points at
  them should move before that. The README, `docs/running-locally.md`, the samples' development
  fallback and the startup error for a missing `ApiBaseUrl` now name the explorer host.
  (Fhi.Metadata-chiyk)
- **Deep links into a datasamling page's metadata section change where the catalogue has placed its
  properties: `#metadata` becomes one `#section-<section key>` per placed section.** The keys are
  Munin's, not this package's — `#section-om-datasamlingen`, `#section-datakilde`,
  `#section-alle-metadatafelt` and whichever others the placement rows declare — so the set is open
  and a host that writes ids of its own should not start one `section-`. This is the same family a
  kilde page has written since `Fhi.Metadata-35w0p.22`, not a second one. Bare `#metadata` has not
  gone: it anchors whatever the catalogue titled but placed nowhere, so a payload that places some
  of its groups and not others writes that id and the new ones on the same page, and a payload
  predating the placement rows writes it alone. The three other ids a datasamling page writes are
  unchanged: `criteria`, and `source` and `statistics` for as long as the catalogue has placed
  nothing those blocks draw. No class name is added or renamed, so no `Fhi.Helsedata.Stiler` rule is
  needed for this. (Fhi.Metadata-lr6yh)
- **The Dataperiode column now holds a longer string, and the width it is given was measured for
  the shorter one.** A two-ended period reads `1. jan. 1979 – 31. des. 2024` where it read
  `jan 1979 – des 2024`, roughly a third wider. `Fhi.Helsedata.Stiler` gives
  `.munin-explorer-dataitem-main__period` `flex: 150 1 0`, taken from what `jan. 2001 – des. 2025`
  needed without wrapping, and the cell's text is `white-space: normal` — so the string wraps
  rather than overflowing and every result row and saved-list row with two ends grows a line
  taller. Nothing is unreadable and nothing is cut off. The Stiler width is filed as its own work
  item (`Fhi.Metadata-byvcr`); a host outside helsedata's estate that copied that number wants
  roughly 1.3× it, or 1.45× to keep the widest case (`31. mars … – 31. mars …`) on one line.
  (Fhi.Metadata-ufmop)
- **Run Fhi.Helsedata.Stiler 0.1.88 or later, or long variable names cannot be read in full.**
  0.1.88 is the first release that wraps the name in a result row. Before it the name is cut to
  one line with an ellipsis, and without the tooltip the rest of it is not shown anywhere in the
  row. (Fhi.Metadata-vdnm4)

## 0.1.0-alpha.12 — 2026-09-17

### Added

- **`PropertyMetadataEntry` carries `GroupKey`, and `KildeSummary` carries `KodeverkShare` and `StatisticsShare`.**
  The Explorer API now sends all three. `GroupKey` is the stable id of a property's section (for
  example `om-registeret`) and is null where the group has no key. The two shares are percentages
  of a kilde's visible variables with a kodeverk link or a statistikk entry, and are null when the
  kilde has no visible variables. Nothing in the components reads them yet, and all three are
  nullable, so a host on an older API is unaffected.
- **The kilde, datasamling and variable pages get a contents nav.** A list of links down the left
  of the page, one per section the page actually drew, so a reader can reach the metadata, the
  datasamlinger, the version history or the statistics without scrolling for them. It fills the
  contents column `DetailPage` has had since the chassis landed, and a page that drew no section
  draws no column at all rather than an empty rail. The links are plain `#fragment` anchors — the
  browser does the scrolling and the nav itself runs **no JavaScript**; the highlight that
  follows the reader down the page is a separate change and needs a script, so it is not here.
  Every entry is built from the same condition its section renders under, so a nav never offers a
  link to a block the page suppressed, and the `href` values are the fixed English section ids in
  both languages while the words translate — a link one reader sends another lands in the same
  place whichever language either is reading. (Fhi.Metadata-35w0p.12)
- **`DetailPage` takes any other attribute you write on it and puts it on the root element.**
  A new `AdditionalAttributes` parameter, captured the ordinary Blazor way. It exists so a view
  mounted on its own can mark its root — `data-munin-explorer-version` on the saved-list view is the whole of its use here.
  A `class` written through it is dropped rather than honoured: the splat is written before the
  element's own `class`, so `munin-explorer-page` cannot be taken off the root the layout rules key
  on. Pass a view's own root name through `ViewRoot`. (Fhi.Metadata-35w0p.13)
- **The package now ships one JavaScript module as a static web asset.**
  `_content/Fhi.Munin.Explorer/explorer-interop.js`, loaded by dynamic import after the first
  render and by nothing else — the same shape `Fhi.Helsedata.Soknader` already ships on this host.
  Nothing rendered depends on it: a host that does not serve it draws exactly the page it drew
  before. It ships no CSS, and that has not changed. (Fhi.Metadata-35w0p.14)
- **`PropertyMetadataEntry` carries `GroupSortOrder`.** The Explorer API sends the order of the
  section itself on the surface the payload was fetched for, so a section's position no longer has
  to be inferred from whichever of its properties happen to hold a value. Nullable, like `GroupKey`
  beside it: null means no placement names the section there, or the API predates the field.
  (Fhi.Metadata-35w0p.19)
- **A detail page's catch-all section now lists the complete record rather than the fields the other
  sections did not want.** The section Munin keys `alle-metadatafelt` is drawn with a lead paragraph
  and a collapsed `<details>` over every property the payload holds a value for — those the named
  sections above already drew included — plus the counts and dates no property definition carries
  (total variables, data collections, data period, last updated in Munin). The repetition is the
  point: the sentence over it says nothing is left out, and on the Tromsø payload that is 34 rows
  where the section drew 8. A source's name, code, short name and kildetype are in it too, which no
  section drew before. The section is recognised by its `groupKey` and never by its heading, so a
  curator renaming it in Munin changes what a reader sees and nothing else; a payload that carries
  no `groupKey` has no such section and is drawn exactly as before. (Fhi.Metadata-35w0p.21)
- **The three detail views open with a row of six facts under the name.** `DetailPage` draws it
  between the name block and the body, so the kilde, datasamling and variable views get it at once.
  Each fact is a label, a value and an optional second line carrying the qualifier that makes the
  value honest — `630 variabler`, then `i 6 datasamlinger`. The second line is left out rather than
  drawn empty when there is nothing to qualify, and a fact the catalogue has not filled in is
  dropped rather than drawn blank, so the row is a summary of what the record actually has.
  (Fhi.Metadata-35w0p.26)
- **`DetailPage` takes a `Facts`, and `DetailFact` is what goes in it.** A label, a value and an
  optional note — itself a label and a value — each value with its own `lang` for the half that is
  the catalogue's Norwegian: a value and its note need not be in one language, so they are marked
  apart. A datasamling's variable count is the case that needs both — the count is in no language
  and the telleenhet under it is catalogue free text held only in Norwegian, so an English reader
  gets `Counting unit: <span lang="no">Pasient</span>`, the unit marked and the label this package
  translated left in the language it was translated into. Empty or unset draws no row at all.
  (Fhi.Metadata-35w0p.26)
- **A source and a datasamling lead with the same six facts, and a variable with six of its own.**
  Kildetype, Dataansvarlig, Grad av personidentifikasjon, a period, a count and Lovverk on the two
  entity pages — Dataperiode and the total variable count on a source, Gyldighet and the
  collection's own count on a datasamling. A variable leads with Kodeverk, Statistikk, Opprinnelse,
  Identifiseringsgrad, Databasereferanse and Dataperiode, and with neither Kilde nor Datasamling:
  the breadcrumb directly above already names both. **Every one of them is still drawn in its
  section below** — the row summarises the page rather than moving anything out of it, and each
  value is resolved through the member that section reads, so the two cannot come out in different
  words. (Fhi.Metadata-35w0p.26)
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
- **The whole-variable view says where the variable comes from.** `VariableView` draws the kilde
  trail — kildetype, then kilde, then datasamling — under a **Plassering** heading, with an entry
  in the contents nav. The trail itself is not new: it has always been drawn inside the panel an
  open row shows, and this is the same implementation with a second caller rather than a copy, so
  the rule that leaves a level the catalogue holds nothing for out of the trail — rather than
  writing "Ikke oppgitt" in it — holds on both surfaces. A variable placed nowhere at all draws no
  section. No new class name: the steps are a bare `<ol>` in a `munin-explorer-breadcrumb` wrapper,
  the name that already dresses the trail over the search results. The three facts it names are
  drawn again under **Kildeinformasjon** further down, and deliberately: a path and a fact list are
  different registers, and both spell the kildetype out of the same table so the page cannot name
  one kildetype two ways. (Fhi.Metadata-35w0p.47)
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
  step. Unset, an open kilde draws no trail at all, while an open datasamling still draws one —
  rooted at the parent kilde alone, which `DatasamlingHref` is enough to reach. `KildeExplorer`
  sets it for you. (Fhi.Metadata-35w0p.49)
- **The kilde, datasamling and variable views draw each block as a `<section>` with a stable id.**
  Every block heading below the page title — the catalogue metadata, the datasamlinger, the
  inclusion criteria, the version history, the statistics and each fact box — now sits inside
  `<section id="…" data-nav-section class="munin-explorer-page__section">`, which is what a
  contents nav or a scroll-spy needs to anchor on; neither ships yet. The ids are fixed English
  words (`metadata`, `criteria`, `source`, `statistics`, `datacollections`, `versions`,
  `dataperiod`, `datatype`, `variablegroups`) rather than a slug of the heading, so a link into a
  section is the same link for a Norwegian and an English reader and survives a label being
  reworded. Nothing moved on the page, which is the whole acceptance: the wrapper carries no
  spacing, and every block that drew something before draws the same thing now — where a block can
  come out empty the section is emitted inside that check, never around it, so a datasamling that
  counts nothing gets no empty box rather than a heading in a box. (Fhi.Metadata-35w0p.5)
- **The datasamling page draws Opprettet i Munin and Statistikktype, and its parent kilde can be
  a link.** All three facts were already on the payload and rendered nowhere. "Opprettet i Munin"
  sits beside "Sist oppdatert i Munin" in Kildeinformasjon, worded that way because the bare
  "Opprettet" is Kelda's column for the founding year the import file states. Statistikktype is a
  row in the statistics block, resolved through the same vocabulary that names the heading over it
  and yielding to a section the catalogue has placed the key in, as Frekvens and Telleenhet
  already do. `DatasamlingView` gains a `KildeHref` parameter — `Func<Guid, string>?`, given the
  owning kilde's id and answering an address — which makes the Kilde row the way back up to the
  source; left unset, as it is everywhere but the kildeutforsker, the name is plain text rather
  than a link that goes nowhere. No new class name. (Fhi.Metadata-35w0p.50)
- **`FilterOptions` carries the whole variabelgruppe hierarchy and each group's visibility.**
  `GET /api/explorer/filters` answers with a second variabelgruppe collection,
  `hierarkiVariabelgrupper`, read as `FilterOptions.HierarchyVariabelgrupper`: every group the
  current selection reaches, with the kilde, delkilde and datasamling it hangs under in
  `VariabelgruppeFacet.Owners`. A host can draw a folder tree from the answer the facets already
  come in, instead of one `kilder/{id}/hierarchy` call per source a reader expands.
  `VariabelgruppeFacet` also gains the stored `Filter` value and the admin-curated `Global` flag,
  so the two surfaces can be told apart: the tree shows every group, and the standalone facet
  withholds the ones marked `"2"` — which arrive nonetheless, because the tree needs them, and
  because an opted-out group still appears in `Variabelgrupper` as the trunk an offered descendant
  nests under. `IsStandaloneFacetOption` is that test; which collection a row came from is not.
  `Filter` is a `string?` rather than a bool, so a group whose source file left it unset stays
  distinguishable from one that opted out. `HierarchyVariabelgrupper` is empty against an API
  predating the change, where `Variabelgrupper` keeps reading as before — with `Filter` null,
  `Global` false and `Owners` empty, since that API sends none of the three.
- **Kilde and delkilde rows in the variable explorer's Kilde facet now lead with a folder glyph,
  and a biobank kilde says so in a badge.** The folder is the same glyph at both levels, as in
  Kelda's own hierarchy tree and in Runa: what tells a kilde from a delkilde is where its row sits,
  so a second picture would invite a reader to look for a difference the tree does not draw. It
  sits between the checkbox and the name, exactly where a datasamling's datakategori glyphs already
  do, and the checkbox keeps its indentation whatever leads the name beside it. The glyph is
  `aria-hidden` and is named nowhere: unlike a datakategori it repeats the nesting the list already
  carries, so there is nothing for it to add in words.
  The badge is read off the `kildeType` the `GET /api/explorer/filters` answer already carries for
  each kilde, so nothing new is fetched to draw it, and it is real text inside the `<label>` rather
  than a rule or a picture — it is part of the checkbox's accessible name, and it is a member of its
  own separate from the glyphs, so turning decoration off cannot take it off the row. Biobank is the
  whole of the vocabulary marked out: the API's kildetype list holds eight values and Prøvesamling
  is not among them — that word is an EHDS datakategori, on a field the filters answer does not
  carry for a kilde, so marking one is `Fhi.Metadata-wxn6g` rather than part of this. Any other
  kildetype, and a kilde carrying none at all, wear no badge; an empty capsule would say they were
  one of the kinds the badge names. The word is this package's own bilingual copy rather than the
  API's resolved kildetype label, unlike every other kildetype word in this panel, because the badge
  marks membership and is drawn under a group heading already carrying the API's word for the same
  value. A chip for a badged kilde still carries the name alone.
  (Fhi.Metadata-aw203)
- **`DatasamlingFacet` carries the datasamling's data categories.** `GET /api/explorer/filters` sends
  `categories` on every datasamling now, so a host drawing data-category glyphs beside a filter row
  reads them off the facet it already has instead of one hierarchy request per kilde. The tokens are
  the ones `HierarchyDatasamling.Categories` carries — EHDS CURIEs such as `ehds-cat:biobanks` and
  bare codes such as `RPDG` both occur, so match whole tokens rather than a prefix — and the list is
  empty rather than null against an API that predates the field. (Fhi.Metadata-bajdj)
- **A datasamling in the Kilde facet now shows one glyph per datakategori it carries.** The same
  EHDS glyphs and the same shared render order the kildeutforsker's hierarchy tree already draws,
  read off the `categories` field of the `GET /api/explorer/filters` answer the row itself is built
  from — so no kilde hierarchy is fetched to draw them and opening a branch still costs no request.
  A datasamling hanging straight off its kilde is drawn exactly as one under a delkilde; `DelkildeId`
  decides where the row hangs and nothing about the glyphs. The glyphs appear between the checkbox
  and the name, as in Runa; the checkbox keeps the same indentation regardless of icon count.
  A token the icon table does not know falls back to the catch-all glyph, as it
  does in the hierarchy tree, and a datasamling with no categories at all draws nothing. The slot is
  `aria-hidden` and the categories are named in `screenreader-only` words after the label instead,
  the way the hierarchy tree names them, so the checkbox says which datakategorier its datasamling
  carries rather than leaving the pairing to the glyphs alone. Those words are this package's prose
  in the reader's language, so the `lang` marking a facet row puts on a catalogue name now sits on a
  `<span>` around the name itself rather than on the `<label>` around the whole row, which would
  have had a screen reader pronounce "Data category" as Norwegian (WCAG 3.1.2).
  (Fhi.Metadata-evoil)
- **`DetailNamedSection` and a `NamedSections` parameter on `KildeView`, `VariableView` and
  `DatasamlingView` put a section of your own in the contents nav.** Each `DetailNamedSection` is an
  id, a heading and a body; the view draws it as a section under a heading at the level of its own
  blocks and adds the matching nav entry, both off the same value, so a listed section is always on
  the page. `KildeView` and `DatasamlingView` draw them after their own blocks, `VariableView`
  between the metadata and the version history. Give each an id no other element on the page has,
  and none the views write (`metadata`, `criteria`, `source`, `statistics`, `datacollections`,
  `versions`, `dataperiod`, `datatype`, `variablegroups`), or the id is on the page twice and the
  nav's second link lands on the first. `Sections` is unchanged: still a
  fragment, drawn right after the named sections, and not listed in the nav, because a view cannot
  see an id or a heading inside a fragment. `KildeSearch.Sections` stays a fragment and is not
  listed either. (Fhi.Metadata-fkiz9)
- **The variable explorer's filter panel gains an Ikoner switch beside Nivålinjer, and
  `VariableSearch` a two-way `ShowNodeIcons` to go with it.** It turns off the node icons the
  panel's kilde tree draws in front of each name — the folder on a kilde or a delkilde, one glyph
  per datakategori on a datasamling — and it reaches the kilde a reader drills into as well, so one
  press decides both surfaces rather than leaving the pictures waiting one click away. The same
  parameter name and the same on-by-default meaning `KildeView` and `KildeHierarchyView` already
  carry, so a host setting it on more than one of them sets one thing.
  Off costs every reader the same thing. The glyphs are `aria-hidden` and the `screenreader-only`
  words beside them are those same categories said aloud, so the switch takes both or neither: a
  datasamling stops naming its datakategorier on the row whether the reader is looking at the tree
  or listening to it. Keeping the words behind an `Ikoner` switch that is off would leave a control
  that does nothing at all for the reader pressing it, which is why `KildeHierarchyView`'s own
  parameter has always taken both. Nothing else on the row moves: the filter it ticks, the counts
  beside it, the level lines and the kildetype badge on a kilde are drawn exactly as before, and
  the badge in particular is real text outside the icon slot, so it stays in the checkbox's
  accessible name whichever way the switch is set.
  It is a native `<button role="switch">` carrying `aria-checked`, the same control Nivålinjer is,
  so it announces as on and off and answers Enter and Space with no key handler of this package's
  own.
  `ShowNodeIconsChanged` is what a host stores. Like `LevelLines` beside it the parameter is read
  once at mount and owned by the component afterwards — the package makes no JS interop call and so
  reaches no `localStorage`, and what is remembered about a reader is the host's own policy to set
  — so a host that wants the choice to survive a visit stores what the callback raises and supplies
  it at the next mount. A host that stores nothing gets the icons at every visit. No new class name
  is added: the switch wears the pair Nivålinjer already wears.
  (Fhi.Metadata-kd9ts)
- **The variable explorer's filter panel folds behind a "Vis filtre" button on narrow screens.**
  `VariableSearch`, standalone or inside `VariableExplorer`, now draws the same toggle the
  kildeutforsker has, and the panel starts folded. Folding keeps every chosen filter, the open
  facets and the results, and fetches nothing; the active-filter chips over the results stay on
  screen and still remove a filter. On `Fhi.Helsedata.Stiler`, a host with room for a sidebar
  shows the panel as before; see the note for hosts about any other stylesheet.
  (Fhi.Metadata-l9l2n.102)
- **A datasamling in the kildeutforsker's hierarchy can be opened on its own page.** Each
  datasamling in `KildeView`'s tree now carries a link of its own, and `KildeExplorer` reads and
  writes `?datasamling=` beside `?kilde=` so the address names the whole path the reader walked.
  It is a plain `<a href>`: middle-click and Ctrl+click open a tab, the address pastes into a fresh
  one, and Back returns to the kilde rather than to the list. The link is written in the node's
  `<li>` after the `<details>` and never inside the `<summary>`, so the disclosure keeps its one
  job and still expands and collapses on Enter and Space. Back lands on the kilde with its
  hierarchy collapsed again, not expanded to where the reader was. `DatasamlingView`, which the
  variable explorer already reached, is rendered unchanged — the route was what was missing, not
  the view. `KildeSearch` gains `SelectedDatasamlingId` and `DatasamlingHref` for a host that owns
  its own query string. A host's own query parameters survive a navigation the way they survive a
  render: the explorer re-reads them off the address that arrived, so a navigation changing only
  the host's own keys neither loses them nor puts back the ones it dropped — and it redraws its
  own links from that address itself, rather than waiting for a render the host may never make.
  (Fhi.Metadata-l9l2n.107)
- **`KildeHierarchy` carries how many of the kilde's variables have expired.**
  `GET /api/explorer/kilder/{id}/hierarchy` sends `historicalVariableCount` beside
  `totalVariableCount`, and the contract now reads it as `HistoricalVariableCount`. The two are
  disjoint: `TotalVariableCount` is what a reader can open today, `HistoricalVariableCount` the
  variables whose every published version has expired. It reads as 0 against an API that predates
  the field. (Fhi.Metadata-l9l2n.113)
- **The contents nav is titled.** It carried only an `aria-label`, which names the landmark for a
  screen reader and renders nothing at all, so a sighted reader met an untitled list of links above
  the page's content. The nav now opens with a visible heading reading **Innhold** — or
  **Contents** — matching what helsedata's own contents nav draws above the same list. The word is
  the one the nav was already announcing itself by rather than a second string beside it, so the
  heading and the accessible name cannot drift apart. (Fhi.Metadata-l9l2n.116)
- **`KildeSearch.InitialDirection(KildeSortOrder)` is a new public method, and a supported entry
  point rather than an accident of the explorer's URL code.** It answers which way a column runs
  before anybody presses it: `Ascending` for `Name` and for `Standard`, `Descending` for
  `Variables`, `SourceUpdated` and `Established`. A host needs it because `KildeSearch.Direction`
  is a `SortDirection`, a struct with no unset state — it is `Ascending` whatever the order is, so
  a host setting `Order` from a `?sort=` link and nothing else silently opens a count or date
  column at its smallest value rather than at the end a reader of that link saw. Passing
  `InitialDirection(order)` alongside `Order` is what reproduces a heading's first press, and is
  what `KildeExplorer` does. Being public is a commitment: these per-column defaults are now part
  of the package's surface, and changing one is a breaking change for hosts, not only for the
  headings here. (Fhi.Metadata-l9l2n.88)
- **The result list's save column has a header, and the Kolonner picker can hide it.** For a
  signed-in reader the column of "Lagre i liste" buttons is headed "Variabelliste" ("Variable list"
  in English), is listed first in Kolonner and starts on. Turning it off removes the header and
  every row's button. It is not counted as the last column left, so the picker still refuses to
  hide the last data column. Signed out, neither the column nor its picker entry appears.
  (Fhi.Metadata-q7i5e)
- **The filter panel has a builder for the kilde → delkilde → datasamling → variabelgruppe tree,
  and nothing draws it yet.** It derives the whole tree from a single `GET /api/explorer/filters`
  answer: every variabelgruppe the answer carries whatever the API's own facet opt-out says, each
  one placed under the owning ids the payload names rather than under the group it nests in, and
  every count left exactly as the cross-filtered answer sent it. The standalone Variabelgruppe
  facet's own list is derived beside it, and still leaves out the groups the API opts out of that
  facet whether or not one of them is selected. Both are internal and have no caller on any render
  path, so this version compiles and configures exactly as the last one did and renders the same
  panel bar the Fixed entry below; the panel is rewired onto them in a later change. `KildeLevels`
  and `OnePerId`, which the panel's kilde facet has always used, moved here out of the panel and
  are still called from it, which is why this internal type has four entry points where the tree
  itself needs two. (Fhi.Metadata-raspm)
- **The filter panel explains its datakategori glyphs in words, under an `Ikonforklaring`
  legend.** The kilde tree draws one glyph per datakategori on a datasamling and a folder on the
  levels above, and until now the only place those pictures were named was the
  `screenreader-only` words on the row itself — so a sighted reader had nothing to read them by.
  The legend lists the whole vocabulary rather than what the narrowing has left on screen, built
  from `DataCategoryIcons.Order`, so it cannot pair a picture with the wrong word on a row that
  draws several; the grouping folder is deliberately absent, because it says only what the nesting
  already says. Every entry is localised in both `nb` and `en`. The glyphs stay `aria-hidden` and
  the meaning is ordinary text, not a `title` and not an `alt` — the same words for every reader —
  and they keep `currentColor`, so nothing here is told apart by hue alone. It is a `<details>`
  resting shut, whose `<summary>` carries the control's accessible name and expanded state, and it
  folds with the facets under `Utvid alle` and `Skjul alle`. It is drawn only while the `Ikoner`
  switch is on: with the pictures gone there is nothing left for it to explain. (Fhi.Metadata-zllxt)

### Changed

- **The detail pages' fact lists wear the chassis's own class instead of the drill-in panel's.**
  Every `<dl>` the kilde, datasamling and variable views draw is now
  `munin-explorer-page__fields` rather than `munin-explorer-meta__grid`, and the language name over
  a value the catalogue holds in more than one is `munin-explorer-page__language` rather than
  `munin-explorer-meta__language`. Nothing else about the markup changes — the same rows, the same
  `<div>` around each `dt`/`dd` pair, the same count of pairs on every page — and `Fhi.Helsedata.Stiler`
  0.1.75 gives the fact list the panel's numbers unchanged, so on a host that has it the grid and the
  type move nothing. A host carrying its own rule for the panel grid moves by whatever that rule
  says instead; the sample stylesheets here are one such host, and their fact lists gain a 40px row
  gap and a 24px bottom margin. The language marker is the exception on both: Stiler declares
  `margin: 0` for it and none of the panel marker's uppercase, letter-spacing or grey, so the
  language name draws at body size until `Fhi.Metadata-4ozhj` adds them.
  The result row's drill-in panel keeps both of its own names and is untouched: `DetailBlocks` draws
  both surfaces, so the one piece they share takes its class from the caller rather than deciding
  for itself. Until now a detail page borrowed the panel's typography and grid, which is why
  Stiler carried an override putting the panel's two lanes back to one there; neither surface could
  be restyled without the other following it. (Fhi.Metadata-35w0p.11)
- **`VariableListView` is drawn on the detail pages' chassis, and its heading is a real heading.**
  The saved-list view was the one surface with no chassis at all: its root element carried no class,
  so every layout rule the kilde, datasamling and variable views answer to missed it, and it sat at
  whatever width and rhythm the host happened to give a bare `<div>`. It now renders through
  `DetailPage` like the other three — `munin-explorer-page` on the root, the name block above the
  fold, and the list, its controls and its pager in `munin-explorer-page__body` /
  `munin-explorer-page__main`. The version marker `data-munin-explorer-version` has not moved: it is
  still on the root element, which is now the chassis's. The table, the scroll container around it
  and the pager are untouched, and paging stays flat.
  The heading used to be assembled as a string and injected as raw markup, so it carried neither a
  class nor an id — it missed `headline headline-s`, and no anchor on the page could reach it. It
  comes from the same heading helper the other three views use now, at the level the host asked for,
  with an id of its own. That id is minted per mount, as every id this view writes is, so it is an
  anchor within one page load rather than a stable deep link. (Fhi.Metadata-35w0p.13)
- **This view deliberately has no contents nav, so its body is one column and never a rail.**
  The Kilde filter beside the list already does the grouping a contents nav would do, so
  `DetailPage.Contents` is left null and `munin-explorer-page__toc` is not emitted. Both sample
  stylesheets now take the second track back rather than merely not giving it — the rule that gives
  a body two tracks is gated on the contents column, but the pinned Stiler's own copy is ungated and
  was winning for a body with one child, which is a 250px empty rail down the left of a 1440px page.
  (Fhi.Metadata-35w0p.13)
- **A detail page's sections are identified and ordered by the catalogue rather than by its own
  data.** Two properties are in the same section when they carry the same `groupKey`, so renaming a
  section's title in one language no longer splits it in two; and the sections are ordered by
  `groupSortOrder`, so filling in a previously-empty property no longer moves a section up the page.
  Both are fallbacks rather than requirements — a payload carrying neither is grouped by its
  headings and ordered by its members exactly as before, so upgrading this package never requires an
  API of a particular age. A property the payload files under no section is still drawn nowhere,
  which is unchanged: those are the column-backed keys each detail view already draws itself.
  (Fhi.Metadata-35w0p.19)
- **A detail fact the catalogue holds nothing for now reads "Ingen", muted, instead of being left out
  or reading "Ikke oppgitt".** One rule for the fact boxes on all three detail pages — Kildeinformasjon
  and Statistikk on the kilde and datasamling pages, Kildeinformasjon on the variable page — and for a
  version's details: a row with no value is drawn with "Ingen" / "None", and a box where no row has a
  value is not drawn at all, heading and contents-nav entry included. Kildetype and identification level
  used to read "Ikke oppgitt", which kept their box drawn whatever the catalogue held; a source with
  nothing filled in now has no Kildeinformasjon box. A fact a curated section already draws is still
  left out of the box rather than reading "Ingen", and a validity with a start and no end still reads
  "Pågående". The hero row above follows its own rule and drops a fact with no value, so a source or
  datasamling with no kildetype or identification level no longer leads with "Ikke oppgitt" for them.
  (Fhi.Metadata-35w0p.24)
- **The result row's Data tab no longer says "Ingen kodeverk registrert" when it has statistics.**
  The kodeverk section draws nothing when a variable has no kodeverk, as the whole-variable view already
  did, so the two views agree. A tab with neither kodeverk nor statistics says so once:
  "Ingen kodeverk eller statistikk registrert". (Fhi.Metadata-35w0p.24)
- **The saved-list view fills the chassis action row, and its summary says how many kilder the
  list draws from.** The list picker and the three download controls used to sit loose in the
  flow — this was the one detail surface that passed the chassis no `Actions` at all. Both now
  render in `munin-explorer-page__actions`, the row the other three detail views already fill,
  with the download behind a `<details>` so the two formats and the kodeverk tick do not stand
  across the row. The picker still appears only for a reader with more than one list, and the row
  itself is drawn only when one of the two is in it — a reader whose single list is empty gets no
  empty row. The line under the heading gains "fra N datakilder" / "from N sources", the distinct
  kilder in the whole list, written once the membership read has finished, left out until then,
  and dropped again when a variable is added or removed rather than left saying what it said
  before the write. (Fhi.Metadata-35w0p.52)
- **The kilde, datasamling and variable detail views read downwards in one column.** The sidebar
  each of them kept beside the main column is gone, and every block that lived in it now sits in
  the main flow: Kildeinformasjon and Statistikk on a kilde and a datasamling, and
  Kildeinformasjon, Dataperiode, Datatype, Variabelgrupper and Datasamlinger on a variable. Nothing
  was dropped, and the nine section ids a deep link uses are unchanged and still in the order they
  were. Two things about the order are worth knowing. The moved blocks keep the position they were
  already read in relative to the view's own blocks, so on a page with nothing passed into the
  `Sections` slot the reading order is exactly what it was. And on a kilde and a datasamling, where
  an explorer or a host does pass sections — Kelda's Variabler, Kriterier for tilgang til data and
  Priser — those now come *after* the moved blocks rather than before them, because on those two
  views the slot closes the column: a host's own sections are additions to the page the component
  is. A variable is the exception and nothing there moves. Its slot stays between Metadata and
  Versions, which is where the kodeverk block the variable explorer passes has always been drawn,
  so the five moved blocks join the page after it rather than before it. That reordering applies at
  every width. The layout change is desktop-only, since below 1024px the two columns already
  stacked.
  (Fhi.Metadata-35w0p.6)
- **The kilde, datasamling and variable views now draw one shared page chassis.** A new
  `DetailPage` component emits the root, the name block, the body grid, the contents column and the
  main column once, and the three views supply content into it rather than each writing the same
  three wrappers under a prefix of its own. The chassis wears `munin-explorer-page`,
  `munin-explorer-page__body`, `munin-explorer-page__main` and `munin-explorer-page__toc`. Each
  view keeps the prefixed names it already had on the root and the main column — `munin-explorer-kilde`
  and `munin-explorer-kilde__main` sit beside the chassis names there, and so do the `-datasamling`
  and `-whole` equivalents — because one of them, `munin-explorer-kilde__datasamlinger`, is styled
  by an expanded row in the kildeutforsker as well as by the detail page. The body is the one
  element that does not keep its old name, which is a break and is written up under Removed. The
  markup below the chassis is untouched, down to the count of `dt`/`dd` pairs.
  (Fhi.Metadata-35w0p.9)
- **Every facet that nests values is now a tree the reader opens branch by branch, and the Kilde
  facet's datasamlinger are in it.** Any value with something under it — a kildetype group, a kilde,
  a delkilde, and equally a variabelgruppe nested under another or a saved filter under another —
  now carries a disclosure control of its own beside the checkbox, and every one of them starts
  shut. Opening a branch narrows nothing, ticks nothing and asks the API for nothing: the whole tree
  comes from the `GET /api/explorer/filters` answer the panel already had, so a reader can look
  inside a kilde without filtering on it. The datasamling level is drawn from
  `FilterOptions.Datasamlinger`, each row placed under the delkilde its `DelkildeId` names or
  directly under its kilde where that is null — which is the majority of them — and the counts are
  the cross-filtered ones the same answer carries. The values under a shut branch are not rendered
  at all rather than hidden, so nothing inside one can be tabbed into; a value ticked inside a
  branch stays ticked while it is shut, keeps its chip over the results and keeps its place in the
  facet's own count. Utvid alle and Skjul alle reach the branches as well as the facets, and a term
  typed into the Kilde facet's own search opens the branches down to what it matched — that search
  matches the names of delkilder and datasamlinger too, and a match nobody can see would leave a
  kilde on screen for no visible reason. (Fhi.Metadata-adog5)
- **A property Munin keeps in a column of its own is drawn in the section the catalogue places it
  in.** Beskrivelse, Lovverk, Dataansvarlig, Databehandler, GradAvPersonidentifikasjon, GyldigFra
  and GyldigTil — and, on a datasamling, StatistikkType, TelleEnhet and Frekvens — arrive as typed
  fields rather than in the `additionalProperties` bag, so the metadata sections had no value to
  draw and a section holding only such keys came out empty. The kilde, datasamling and variable
  views now merge those columns into the values they resolve properties from, and each fact box row
  that would repeat one yields to the section that draws it, so a fact still appears exactly once.
  A page reading an API whose placements have not been seeded is unchanged: the fact box keeps
  drawing every one of them. Gyldighet is the one row the catalogue splits in two, so it yields per
  end: with GyldigFra placed and GyldigTil not, the box drops the period and keeps the unplaced end
  under its own label, leaving neither date undrawn. (Fhi.Metadata-bct95)
- **The Kilde facet's tree now reaches the variabelgrupper, and every branch it gains starts
  shut.** A datasamling holding variabelgrupper, a delkilde whose groups are in none of its
  datasamlinger, and a group with groups nested under it are all branches now, opened by the same
  control every other branch of that tree already had: a real button beside the row, named after
  what it opens, carrying `aria-expanded`, and operable from the keyboard. A leaf gets no
  disclosure at all, and what hangs under a shut branch is not rendered rather than hidden, so
  nothing inside one can be tabbed into. Opening a branch narrows nothing and asks the API for
  nothing — the whole tree, the groups included, is read off the `GET /api/explorer/filters` answer
  the panel already had, so a kilde's groups still cost no request of their own and every count it
  draws is the cross-filtered one that answer sent. The Kilde facet's own search now matches group
  names too, and opens the branches down to what it matched. A group hangs under every datasamling its
  variables are in, so one group can be drawn at more than one place in the tree, and each of those
  places opens and shuts on its own. The groups are containers for now, so a group row is its name
  alone — no count and no tick, the two things a checkbox carries: ticking one is wired to the
  standalone Variabelgruppe facet's selection in a later change, and that facet itself is unchanged
  here. No new class name — the branch row and its disclosure wear the two names
  Fhi.Metadata-adog5 introduced. (Fhi.Metadata-g51gg)
- **A variabelgruppe in the Kilde tree is a checkbox now, and it is the same selection as the
  standalone Variabelgruppe facet's.** Both surfaces write and read the one
  `VariableFilter.VariabelgruppeIds`, so ticking a group on either shows it ticked on the other,
  and one tick produces exactly one chip over the results however many places the tree draws that
  group at — a group hangs under every datasamling its variables are in, and every placement of it
  carries the same state. Each row now carries its cross-filtered count beside the name, as the
  levels above it already did. A group the API opts out of the standalone facet gets no checkbox
  there whether or not it is selected — where that facet names the group at all it is to nest the
  offered ones under it, so the row stays a container. The kilde tree is the one surface that
  offers such a group, and its chip is the way off it from anywhere else on the page. One id the two
  collections name differently is named by the facet's own copy wherever that facet reaches: its own
  checkbox, the chip and the trail step agree, while the tree names its rows from the collection it
  draws them out of. Shared links keep working — a `variabelgruppeIds` value restores as a tick in
  the tree, and its name reaches the chip and the hierarchy trail out of the tree's own collection
  rather than reading "Variabelgruppe". Opening
  and shutting a branch still narrows nothing and asks the API for nothing, so a selection and its
  chip survive folding the branch it was made in. No new class name. (Fhi.Metadata-km3zb)
- **The variabelutforsker's kildetype group counts wear `munin-explorer-filters__groupcount`
  instead of `munin-explorer-filters__chosen`.** The number beside a kildetype group in the Kilde
  facet is that group's size, drawn whether or not anything in it is ticked, while `__chosen` means
  how many values the reader chose — which is what the kildeutforsker's facet summaries use it for,
  and they keep it. One class had come to carry both meanings, so the markup told anyone reading it
  something false about half its uses. Both sample stylesheets add the new name to `__chosen`'s own
  selector list rather than giving it a block of its own, so the two cannot drift apart, and the
  merged `Fhi.Helsedata.Stiler` rule is written the same way — but it is in no published Stiler yet,
  so a host draws these counts at browser defaults until that release is cut. The Notes-for-hosts
  entry has the detail. (Fhi.Metadata-l9l2n.104)
- **The kildeutforsker sorts by pressing a column heading, and the "Sorter etter" select is gone.**
  Navn, Variabler, Sist endret and Opprettet each hold a `<button>` now; pressing one sorts on that
  column and pressing it again reverses it, the two-state toggle the variabelutforsker has always
  had. Where the two deliberately differ is the *first* press: there every column starts ascending,
  while here a count or a date column starts at its largest value and only Navn starts at A. That
  is what each column's old select label said it meant — "Flest variabler", "Sist endret (nyest
  først)", "Opprettet (nyest først)" — so a `?sort=Variables` link made against 0.1.0-alpha.11
  still opens the end of the list it opened then, rather than quietly the other one. The select
  over the table has been removed rather than kept in step — two
  controls for one piece of state is what this change exists to avoid — so the catalogue's own
  order is what a first load and a clean URL give and is no longer re-selectable in the page. That
  is intended: it is the absence of a sort rather than one of the five, and it was already the one
  order never written to a host's URL. Hosts setting `KildeSearch.Order` should know that a member
  now names a column rather than a direction, and that the new `Direction` parameter is `Ascending`
  by default whatever the order is — a struct has no unset state to read a per-column default out
  of, so a host that wants what a heading's first press gives passes the new public
  `KildeSearch.InitialDirection(order)` alongside it (see Added). `KildeExplorer` carries the new
  half in `?sortDir=`, omitted wherever the column is running the way that method says it runs, and
  `Sist endret` is reachable only once its column is turned on in the picker. (Fhi.Metadata-l9l2n.88)
- **"Lagre i liste" is a filled button.** It wore `button-square--ghost`, which draws no border
  and no fill until hovered, so on every row it read as bold text. It now wears
  `button-square--secondary` in both of its states. (Fhi.Metadata-q7i5e)
- **The sample hosts follow Stiler 0.1.75, including the new branch disclosures.** HostileHost
  pins the published package; ModernHost and LegacyHost carry its intervening rule changes and
  the four arrow assets those rules use. Three obsolete stylesheet divergence entries are removed.

### Fixed

- **A variable name or column header cut off in the result list now shows its full text on
  hover.** Stiler clips both to one line, and every non-empty data value already carried its full
  text as a `title`, but the name and the headers did not. Measured on real data, 17 to 19 names
  per page were clipped at every desktop width, and the Variabelgruppe header at 1300px. The name
  span and a new unclassed `<span>` around each header's label now carry a `title`. An empty name
  gets none. The title sits on the span and not on the `columnheader` or its sort button, where
  Edge exposes it on the sorted column as a description repeating the name. Accessible names are
  unchanged, and no class name is added. (Fhi.Metadata-1vm16)
- **A variable's description renders its links and line breaks instead of printing them as
  source.** The variable page's ingress, the description in Runa's row panel and each version's
  description in the version history printed the catalogue's `<br>` tags and `[label](url)` links
  literally, while the kilde, delkilde and datasamling descriptions already rendered them. All
  three now go through the same renderer: links and line breaks only, no raw HTML, so a
  description holding an HTML tag still shows the tag as text. The ingress keeps its `lang` and
  class, and no class name is added. (Fhi.Metadata-35w0p.46)
- **The whole-variable page no longer lists a datasamling the catalogue left unnamed.** Its
  Datasamlinger section drew an empty bullet for one, and — now that the Plassering trail counts
  them on the same page — a count of two could stand over a list of three. Both the section and
  the trail's last step are the one predicate again, including its fallback to the primary
  datasamling's name, so a variable the catalogue names no datasamling for at all draws neither
  the section nor the contents entry rather than a heading over blank bullets.
  (Fhi.Metadata-35w0p.47)
- **The variabelgruppe filter panel draws an opted-out group as a container, not a checkbox.**
  `GET /api/explorer/filters` returns a group marked `filter: "2"` in `variabelgrupper`
  nonetheless, when an offered descendant has to nest under it. The panel ticked such a row like
  any other and submitted it, offering a filter the API withholds; it is now drawn as the plain
  label its children hang from, the way `VariabelgruppeFacet.IsStandaloneFacetOption` says to.
  That label is marked with the catalogue's own language like the checkbox labels around it, so an
  English reader hears a Norwegian group name in a Norwegian voice there as well as in its chip.
  A reader who arrives with one already selected — through a shared link, or a host mounting with
  `Filter` set — still has the chip over the results to take it off.
- **Local testing documentation correctly names the variables that drive geometry assertions.**
  The `running-locally.md` guide previously stated hand-written counts for the widths and states measured by `check-hostile-host.sh`. These counts had gone stale as the scripts evolved. The text now directs readers to `GEOMETRY_WIDTHS` and `TARGETS` as the sources of truth, ensuring the documentation stays accurate without manual updates. (Fhi.Metadata-d951y)
- **The contents nav now lists the sections the explorers add to a detail page.** In the
  kildeutforsker the nav on an open kilde stopped at Statistikk, although Variabler — and with
  `ShowAccessAndPrices`, Kriterier for tilgang til data and Priser — were drawn below it, and the
  whole-variable page in the variabelutforsker left out its Kodeverk section the same way. Each is
  now a section of its own with a fixed English id (`variables`, `accesscriteria`, `prices`,
  `codelists`), listed in page order, and Kodeverk is listed only for a variable that has one.
  Runa's kilde page, which adds no sections, lists only the view's own. (Fhi.Metadata-fkiz9)
- **The variable panel's kildesti no longer ends on one datasamling when the variable sits in
  several.** The trail read the primary `datasamlingName` alone, so a variable in nineteen
  datasamlinger was written up under one — not visibly incomplete, but confidently singular. The
  last step now counts them ("19 datasamlinger"), and a "Datasamlinger" list beside the trail names
  every one with its own validity period, which is what the whole-variable view one press further in
  has always shown. A variable in exactly one still reads as that name, and a payload carrying no
  list still falls back to the primary name. (Fhi.Metadata-l9l2n.108)
- **A detail view's contents nav scrolls to its section instead of leaving the page.**
  Every entry carried a bare `#id`, which a browser resolves against the document's
  `<base href>` rather than against the page being read. helsedata's Optimizely host sets that to
  `/`, so each press navigated to the site root and dropped both the route and the open
  `?kilde=` — the reader lost their place entirely. The hrefs now carry this page's own path and
  query in front of the fragment, taken from the address the explorer last wrote rather than from
  `NavigationManager.Uri`, which a `history.replaceState` mirror leaves behind. Each target
  section also carries `tabindex="-1"`, so on a host that does not intercept the press the
  browser's own fragment jump moves keyboard focus into the section rather than only the
  viewport. No JavaScript: fragment navigation is plain HTML. (Fhi.Metadata-l9l2n.114)
- **The filter panel draws a datasamling once when the answer lists its id twice under different
  delkilder.** The kilde facet split the copies across its two levels before settling repeated ids,
  so such a datasamling was drawn both straight under its kilde and under a delkilde: one press
  ticked both rows and the results carried two chips for one filter. The first copy the answer
  lists is now the only one drawn, wherever it hangs. (Fhi.Metadata-raspm)
- **The same for a delkilde whose two copies name different kilder.** Each kilde was handed its own
  bucket of delkilder before repeated ids were settled, so the delkilde was drawn under both — the
  second offering a filter none of its variables are behind. Repeated ids are now settled across
  every kilde at once. (Fhi.Metadata-raspm)
- **A chosen datasamling gets one chip when the answer lists its id twice.** The chips over the
  results read the payload's datasamlinger straight while the panel drew the first copy alone, so
  one press left two ways off one filter. (Fhi.Metadata-raspm)

### Removed

- **BREAKING for hosts: `munin-explorer-kilde__body` and its two siblings are no longer emitted.**
  `munin-explorer-datasamling__body` and `munin-explorer-whole__body` go with it. The element all
  three named — the grid the detail pages lay their columns out in — now wears
  `munin-explorer-page__body` alone, so a rule of yours keyed on any of the three stops matching and
  has to move to the chassis name. They are the only names this change drops. The element could not
  keep both, which is the whole reason: every published `Fhi.Helsedata.Stiler` lays those three out
  as a grid of their own, so an element wearing an old name and the chassis name would carry a
  `grid-template-columns` from each block, and which one drew would be settled by the order a host
  happened to load the two stylesheets in rather than by either of them meaning it.
  (Fhi.Metadata-35w0p.9)

### Notes for hosts

- **Move any rule you keyed on `munin-explorer-meta__grid` for a detail page to
  `munin-explorer-page__fields`.** The kilde, datasamling and variable views no longer put the
  panel's class on their fact lists, so a rule of yours reaching one through that name stops
  matching — quietly, because the element is still there and still a definition list. The same
  move applies to `munin-explorer-meta__language`, which becomes `munin-explorer-page__language`
  on those pages. Both old names stay exactly where they were on the result row's drill-in panel,
  which still emits them, so a rule scoped to `.munin-explorer-meta` needs no change at all — and
  the kodeverk and frequency tables, which key off that ancestor rather than off the `dl`, are
  untouched on every surface. `Fhi.Helsedata.Stiler` 0.1.75 has rules for both new names already, in
  the same `components/munin-explorer/_page.scss` the chassis landed in. The fact list's are the
  panel's declaration for declaration — `1fr 1fr` with a 40px row gap and a 24px column gap, `font:
  normal 1rem/160%`, `margin-bottom: 24px`, and below 1280px one column, a 16px gap,
  `margin-bottom: 16px` and full width — but the language marker's is `margin: 0` alone, without the
  uppercase, letter-spacing and grey the panel's marker carries, so **a host that keyed nothing on
  either name still sees its language names change size and colour** on the three detail pages.
  `Fhi.Metadata-4ozhj` is the Stiler bead that puts them back; a host that wants them sooner can
  declare them itself. Both are handles otherwise: define neither and you get a definition list at
  browser defaults, with each language still on its own line because the name is a `<p>`. Both
  sample stylesheets stand in at Stiler's numbers, the bare language marker included.
  (Fhi.Metadata-35w0p.11)
- **`munin-explorer-page__toc` is now drawn on every detail page, so its rule finally matters.**
  It was emitted by nothing until this version — the note under 0.1.x's chassis entry said so — and
  the contents nav fills it now. The rules are `Fhi.Helsedata.Stiler`'s, in
  `components/munin-explorer/_page.scss`: a `250px minmax(0, 1fr)` body above 1025px and a sticky
  contents column. A host that defines nothing for the name still loses no words — the column
  becomes a block in ordinary flow above the main one, which is a contents list above the content
  it lists. Both sample stylesheets carry the stand-in, scoped to a body that has a contents column
  (`Fhi.Metadata-ex5wb` is the bead that puts that gate into Stiler's own copy), because the column
  is still drawn only when something fills it and a page with no sections fills nothing.
  (Fhi.Metadata-35w0p.12)
- **The nav itself borrows helsedata's `form-menu__list` and `form-menu__list__item`, and Stiler
  declares them, so you write no rule for either.** They are global, unscoped classes in Stiler's
  `pages/_healthregisterpage.scss` and they reach its compiled stylesheet, which is where the nav
  gets its link colour and padding from. Both sample stylesheets carry a stand-in for hosts with no
  Stiler, and that stand-in now declares what Stiler 0.1.42 declares — `margin` on the list,
  `background-color` and `transition` on the link — after
  `scripts/assert-sample-css-matches-stiler.sh` found it three declarations short of the real rule.
  `form-menu__list__item` has no rule of its own anywhere, and nothing is emitted for the
  active-item modifiers `form-menu__list__item--active` and `--active-child` either: with no scroll
  tracking in this package an active item would be permanently wrong on every section but one.
  (Fhi.Metadata-35w0p.12)
- **One detail view per document.** The section ids the nav links to — `metadata`, `source`,
  `statistics` and the rest — are fixed English literals with nothing per-instance in them, so that
  a deep link one reader sends another lands in the same place whichever language either is
  reading. The price is that two detail views in one document write each id twice, and a browser
  resolves a fragment to the first match: the second view's contents nav would scroll the reader
  into the first view's sections. Mount one. `VariableSearch` and `KildeSearch` already do — each
  picks between the arms of one `if`/`else`. (Fhi.Metadata-35w0p.12)
- **`DetailToc` and `DetailTocEntry` are new public types, and neither is one to mount.**
  `DetailToc` is the nav the three detail views put in their own contents column, public only
  because a Razor component has to be — the same reason `DetailPage` and `DetailSection` are — and
  `DetailTocEntry` is the id-and-label pair it takes. Mounting one yourself gets a `<nav>` of links
  to whatever ids you hand it. (Fhi.Metadata-35w0p.12)
- **`munin-explorer-page__header` is a new class name, and no published `Fhi.Helsedata.Stiler`
  carries a rule for it.** It is the name block of the saved-list view, which is the fourth surface
  on the detail chassis and the only one with no prefix of its own — the other three wear
  `munin-explorer-kilde__header`, `munin-explorer-datasamling__header` and
  `munin-explorer-whole__header`, and Stiler's rules are keyed on those three names. A handle:
  undefined, the heading is still a heading and still wears Stiler's own `headline headline-s`, so
  what is lost is the separator and the space under the name block that the other three detail pages
  have. Give it what those three declare — 16px of padding below, 16px of margin below, a 1px grey
  rule along the bottom — and both sample stylesheets here stand in at exactly that.
  `Fhi.Metadata-urbj0` is the bead that writes the rule in Stiler. (Fhi.Metadata-35w0p.13)
- **`munin-explorer-page`, `munin-explorer-page__body` and `munin-explorer-page__main` are now worn
  by a fourth view, and this one never draws `munin-explorer-page__toc`.** The saved-list view has no
  contents nav by decision — the Kilde filter beside it does the same grouping job — so its body
  always has exactly one child. **If you write the two-track body rule yourself, gate it on the
  contents column**, as the note under the chassis's own entry said and as both sample stylesheets
  do: `.munin-explorer-page__body:has(> .munin-explorer-page__toc)`. Ungated, a fixed 250px first
  track puts this view's whole list, scroll container and pager in the rail. Stiler 0.1.75 publishes
  that rule ungated, so a host on it draws exactly that until `Fhi.Metadata-ex5wb` lands; the sample
  stylesheets take the track back explicitly for the same reason. (Fhi.Metadata-35w0p.13)
- **A host with a strict Content-Security-Policy has one new path to allow.**
  `_content/Fhi.Munin.Explorer/explorer-interop.js` is fetched as an ES module after the first
  render. A host already calling `UseStaticFiles()` or `MapStaticAssets()` serves it with no
  change; a host that blocks it loses nothing, because the explorer is fully usable without it
  and the refusal is caught rather than left to break the circuit. (Fhi.Metadata-35w0p.14)
- **Three new class names, and none of them is in Fhi.Helsedata.Stiler yet.**
  `munin-explorer-complete-record` on the disclosure, `munin-explorer-complete-record__lead` on the
  paragraph above it and `munin-explorer-complete-record__fields` on the list inside it. Both sample
  stylesheets carry a rule for each; a host without one gets a native `<details>`, a paragraph and a
  single-column definition list, which is legible but not the dense two-column grid the mockup
  draws. Fhi.Helsedata.Stiler now carries a rule for all
  three: they landed on 2026-09-16 as 9ae64786 (_detail.scss, _hierarchy.scss) and Stiler publishes
  on merge to main, so a host taking Stiler from the feed gets them with its next version. This
  package's samples still pin the version before that, which is a separate change.
  (Fhi.Metadata-35w0p.21)
- **New class name `munin-explorer-absent`, and Fhi.Helsedata.Stiler has no rule for it yet.** It is on
  the `<dd>` of a detail fact the catalogue holds nothing for, and on the row panel's one line for a Data
  tab with neither kodeverk nor statistics. Undrawn, "Ingen" reads at full weight, as though it were the
  catalogue's value. Both sample stylesheets colour it `#51547b` (Stiler's `$grey60`), which clears WCAG
  AA on white at 7.23:1 and on Stiler's `$grey10` page ground at 6.76:1; `$grey50` would not. The Stiler
  rule is Fhi.Metadata-iwfji. (Fhi.Metadata-35w0p.24)
- **`munin-explorer-page__facts` is a new class name, and `Fhi.Helsedata.Stiler` 0.1.75 already
  carries a rule for it.** It is the hero row the three detail views now open with, and a host on
  0.1.75 or later needs to do nothing: the rule is in the same
  `components/munin-explorer/_page.scss` the rest of the chassis landed in, laying the list out as
  six equal tracks at desktop, three below 1080px and two below 600px, with the label small and
  uppercase, the value tabular and the note muted under it. A handle otherwise — undefined, the
  `<dl>` is a definition list at browser defaults, with every label and value still on the page and
  in the right order, so what is lost is the row rather than a word. Both sample stylesheets stand
  in at exactly what the pinned 0.1.75 declares. (Fhi.Metadata-35w0p.26)
- **The markup inside it is a `<div>` per fact, `<dt>` then `<dd>`, with an optional `<small>`.**
  If you write your own rule, key the cells off `.munin-explorer-page__facts > div` rather than off
  the `<dt>`/`<dd>` pair directly: a `<dl>` laid out as a grid puts each element in a track of its
  own, and the wrapper is what keeps a label with its value. The `<dt>` deliberately wears no
  `headline` class, unlike the fact lists further down the page, so nothing of Stiler's type scale
  competes with the rule for it. (Fhi.Metadata-35w0p.26)
- **`munin-explorer-page__stuckbar` and three names beside it are new, and `Fhi.Helsedata.Stiler`
  0.1.79 already carries rules for all four.** The others are `munin-explorer-page__stuckbar--on`,
  the shown state the browser module writes, `munin-explorer-page__stuckbar-inner` and
  `munin-explorer-page__stuckbar-name`. A host on
  0.1.79 or later needs to do nothing: the rules are in the same `components/munin-explorer/_page.scss`
  the rest of the chassis landed in, and both sample stylesheets stand in at exactly what that pin
  declares. Handles, all four — but read what an undefined one costs, because it is not the usual
  answer: the bar is **not** a permanently visible band without them, since the markup renders it
  `hidden` and only the module ever takes that off. Undefined, it is a block in ordinary flow at the
  top of the page — which the reader has already scrolled past by the time the module shows it — so
  what is lost is a pinned bar and what is gained is a layout jump. On an older Stiler, either take
  the rules or write `position: sticky; top: 0` for the name yourself. (Fhi.Metadata-35w0p.28)
- **The bar is a repeat, and a host may safely have none of it.** It renders `hidden` with
  `aria-hidden="true"`, and `_content/Fhi.Munin.Explorer/explorer-interop.js` is the only thing that
  shows it — so a Content-Security-Policy that blocks the module, or a reader with JavaScript off,
  gets a page with no bar and no word missing. Do not style it visible: everything in it is already
  on the page above, and a second permanent copy of the page's own title is what the hidden state is
  there to prevent. The bar is text and nothing else — `DetailPage.Actions` is **not** repeated in
  it — so filling that fragment still puts each of your controls on the page exactly once, with
  whatever `id`s you wrote still unique. (Fhi.Metadata-35w0p.28)
- **`munin-explorer-breadcrumb` now dresses a second trail.** The Plassering trail on the
  whole-variable page is wrapped in it, so the rule a host already has for the trail over the
  search results is what strips the list markers and draws the chevrons there too. No new class
  name, and both sample stylesheets already carry the rules unchanged. `Fhi.Helsedata.Stiler`
  declares the name — the divergence baseline reports no unstyled name for it — but whether its
  breadcrumb partial includes the descendant `ol` and `li + li::before` rules could not be read
  from this repository, and `Fhi.Metadata-40y6v` is open to settle it against a checkout. Until it
  does, a Stiler host may draw these steps as a numbered list rather than a chevron-separated
  path: the same steps in the same order, in the wrong shape. (Fhi.Metadata-35w0p.47)
- **`munin-explorer-page__eyebrow` and `munin-explorer-page__actions` are new class names, and
  `Fhi.Helsedata.Stiler` 0.1.75 already carries a rule for both.** They are the detail chassis's
  chrome above the name block — the word naming the kind of page, and the row of page-level
  controls. A host on 0.1.75 or later needs to do nothing. Handles, both: undefined, the eyebrow is
  a paragraph above the title and the action row is its children in ordinary flow, so what is lost
  is the pill and the spacing rather than any word. Both sample stylesheets stand in at exactly
  what the pinned 0.1.75 declares. (Fhi.Metadata-35w0p.49)
- **The breadcrumb wears helsedata's own `breadcrumbs` names and adds none of ours.** `breadcrumbs`
  on the `<nav>`, `breadcrumbs__list` on the `<ol>`, `breadcrumbs__list-item` on each step,
  `breadcrumbs__divider` on the separator and `breadcrumbs__last-crumb` on the current page — all
  global, unscoped classes in Stiler's `layout/_breadcrumbs.scss`, so a host with Stiler gets the
  site's own trail for nothing. **If your stylesheet writes the list rule against `ul` rather than
  against the class, it will not match**: the list is an `<ol>`, because the steps are ordered and
  that is what a breadcrumb tells a screen reader. Unstyled it is a numbered list, which still
  reads correctly. Neither sample host stands these five in — a partial copy of a borrowed rule is
  a divergence, not a stand-in. (Fhi.Metadata-35w0p.49)
- **The detail views emit one new class name, `munin-explorer-page__section`, and nine bare element
  ids.** The class is a handle in the plainest sense: the wrapper it dresses has no padding, border
  or margin, so a host that defines nothing for it sees exactly the page it saw before. What a rule
  buys is `scroll-margin`, so a heading reached by fragment clears a sticky header instead of
  landing under it. `Fhi.Helsedata.Stiler` has the rules already — a new
  `components/munin-explorer/_page.scss`, merged as PR 39299 on 2026-09-11, which hangs the offset
  on the `data-nav-section` attribute the same elements carry — but that PR bumped no version, so
  **no published Stiler carries them** and every host, whatever its pin, draws the wrapper undrawn
  today. That costs nothing until the contents nav that uses it arrives. Both sample stylesheets
  show the offset at Stiler's own 140px. The ids are the part to read twice: `metadata`, `criteria`,
  `source`, `statistics`, `datacollections`, `versions`, `dataperiod`, `datatype` and
  `variablegroups`, and they are written **bare**, without the per-instance discriminator every
  other id this package writes carries — a deep link has to mean the same thing in the next reader's
  browser, and an id minted at run time cannot. Within one mount they cannot repeat, since an
  explorer renders at most one detail view. A page that mounts two explorers, or that already means
  something of its own by `id="source"`, will have duplicates: if that is your page, keep your own
  ids off those nine until `Fhi.Metadata-uobxg` settles it. (Fhi.Metadata-35w0p.5)
- **`DetailSection` is a new public component, and it is not one to mount.** It is the wrapper the
  three detail views draw each of their blocks in, public only because a Razor component has to be
  — the same reason `KildeHierarchyView` and `VariableListFilters` are. It takes an `Id` and its
  content and emits the `<section>` described above, so mounting one on your own page gets you an
  element with an id and nothing in it. (Fhi.Metadata-35w0p.5)
- **Three class names stop being emitted, and one rule you may have written no longer has an
  element.** `munin-explorer-kilde__aside`, `munin-explorer-datasamling__aside` and
  `munin-explorer-whole__aside` are gone from the markup; a rule for any of them now matches
  nothing and can be deleted whenever it suits you. Two consequences are worth knowing rather than
  discovering. Anything scoping `munin-explorer-meta__grid` to one lane inside those asides stops
  applying, so the fact lists take the unscoped two-lane default, which is the right shape for a
  full-width column — do not narrow the base rule to win it back, or the metadata groups lose their
  two lanes with it. And `munin-explorer-kilde__body`, `munin-explorer-datasamling__body` and
  `munin-explorer-whole__body` were grids whose second track is a fixed 320px above 1024px. With
  nothing left to put in it that track was empty space to the right of the page: rendered in
  `samples/LegacyHost` at a 1440px viewport before this change, the main column measured 954px
  inside a 1298px body, the missing 344px being the track and its gap. Do not go and edit those
  three rules, though — `Fhi.Metadata-35w0p.9`, in this same release, stops emitting all three
  names, so the element is `munin-explorer-page__body` and nothing wears them at all. The entry
  under Removed says what to move where. (Fhi.Metadata-35w0p.6)
- **The detail views' shared chassis adds four class names to style.** `munin-explorer-page` on the
  root of the kilde, datasamling and variable views, `munin-explorer-page__body` on the body, and
  `munin-explorer-page__main` and `munin-explorer-page__toc` on the two columns inside it — the last
  of those only on a page whose contents column is filled, which no page in this version is.
  Handles, all four: a host that defines nothing for them gets the body and its columns as blocks in
  ordinary flow, which is the single column those pages draw today. `Fhi.Helsedata.Stiler` has the rules
  already — the same `components/munin-explorer/_page.scss` the section wrapper landed in, extended
  by PR 39300 on 2026-09-11 with a `250px minmax(0, 1fr)` body above 1025px, a sticky contents
  column and a `min-inline-size: 0` main column — but that PR bumped no version either, so **no
  published Stiler carries them** and the body is a block whatever your pin says. Both sample
  stylesheets stand in for the rules at Stiler's own numbers. The one place they deliberately differ
  from what PR 39300 merged is the two-track rule, which they scope to
  `.munin-explorer-page__body:has(> .munin-explorer-page__toc)`: `munin-explorer-page__toc` is
  emitted only when something fills it and nothing does yet, so an ungated fixed first track would
  lay the main column out in 250px of a 1440px page. If you write the rule yourself, scope it the
  same way; `Fhi.Metadata-ex5wb` is the bead that puts the gate into Stiler's own copy before it
  publishes. (Fhi.Metadata-35w0p.9)
- **`DetailPage` is a new public component, and it is not one to mount.** It is the chassis the
  three detail views draw themselves in, public only because a Razor component has to be — the
  same reason `DetailSection` and `KildeHierarchyView` are. It takes the two class names the
  calling view wore before the chassis existed, a header, a contents column and the main content,
  and emits the wrappers described above; mounting one on your own page gets you three nested
  empty divs. (Fhi.Metadata-35w0p.9)
- **BREAKING for hosts: move any rule keyed on the detail views' `__body` names to
  `munin-explorer-page__body`.** `munin-explorer-kilde__body`, `munin-explorer-datasamling__body`
  and `munin-explorer-whole__body` are no longer emitted — the removal and why the element could
  not keep both names are under Removed. What to do about it is one move: whatever your rule
  declared, declare it on `munin-explorer-page__body` instead, and read the note above before you
  give it a second track. Every other old name stays exactly where it was:
  `munin-explorer-kilde`, `munin-explorer-datasamling` and `munin-explorer-whole` are still on the
  roots, `munin-explorer-kilde__main` and its two siblings still on the main column, and
  `munin-explorer-kilde__datasamlinger` still on the table it always named — that last one is why
  the prefixes were kept at all, being styled inside an expanded row of the kildeutforsker's result
  table as well as on the kilde page. A rule keyed on any of them draws what it drew before, and
  needs no new rule. (Fhi.Metadata-35w0p.9)
- **Two class names to style, and one disclosure that is no longer a `<summary>`.**
  `munin-explorer-filters__branch` is the `<li>` of a facet value that has values under it, and
  `munin-explorer-filters__disclosure` is the `<button aria-expanded>` inside it that opens the
  branch. Both are handles: the button carries an arrow as text and a name from `aria-labelledby`, so
  with no rule at all it is still visible, operable and announced, and the row simply draws as the
  blocks it is made of instead of as a row. What a rule buys is the row itself and a 24×24 target,
  which is WCAG 2.5.8 rather than decoration — and a focus state that survives an outline reset:
  the disclosure is a tab stop on every branch of the tree. Both sample stylesheets carry the
  rules to copy. The change to watch for is the kildetype groups inside the Kilde facet: they were
  `<details>`/`<summary>` and inherited whatever you give `.munin-explorer-filters summary`, and
  they are now ordinary branch rows, so a rule written for that summary no longer reaches them.
  Use `Fhi.Helsedata.Stiler` **0.1.75 or later**, which supplies both rules (`Fhi.Metadata-cs3pt`).
  Earlier versions leave the disclosures at browser defaults unless the host adds those rules.
  The disclosure's accessible name keeps the UI action and catalogue name separately language-marked.
  Both names are drawn by every facet that nests values and not by the Kilde facet alone: a
  variabelgruppe nested under another is a branch row too, and it starts shut where it used to be
  drawn unasked, so a rule scoped to the Kilde facet reaches neither it nor its disclosure.
  (Fhi.Metadata-adog5)
- **One class name to style on the variable explorer's Kilde facet.**
  `munin-explorer-filters__badge` is the `<span>` holding the Biobank word on a kilde row. A handle:
  the word is real text inside the `<label>`, so with no rule at all it is visible and part of the
  checkbox's accessible name, and what a rule buys is the capsule that tells it from the name beside
  it. Both sample stylesheets already carry the rule to copy, stood in from the pinned published
  Stiler; which release first supplies it is `Fhi.Metadata-gegtb`.
  The folder glyphs the same facet's kilde and delkilde rows now draw need no new name — they wear
  the `munin-explorer-filters__icons` and `munin-explorer-filters__icon` pair a datasamling's
  datakategori glyphs already wear, which `Fhi.Helsedata.Stiler` **0.1.75 or later** supplies. The
  badge is deliberately outside that slot, so a rule — or a future icon toggle — that hides the
  decoration must not hide the badge with it.
  (Fhi.Metadata-aw203)
- **Two class names to style on the variable explorer's Kilde facet.**
  `munin-explorer-filters__icons` is the `<span>` holding a datasamling row's datakategori glyphs
  and `munin-explorer-filters__icon` is each inline `<svg>` inside it. Handles both: the `<svg>`
  carries `width`, `height` and `stroke="currentColor"` as attributes, so with no rule at all the
  glyphs still draw at text size in the text colour, and the slot is `aria-hidden` either way. What
  a rule buys is the row they sit in and the gap between them. Use `Fhi.Helsedata.Stiler` **0.1.75
  or later**, which supplies both (`Fhi.Metadata-1t36m`); both sample stylesheets carry the rules to
  copy for a host that has neither. The glyphs are drawn between the checkbox and the name, as in
  Runa. Preserve that order and keep checkbox indentation independent of the number of glyphs.
  (Fhi.Metadata-evoil)
- **Four more bare section ids: `variables`, `accesscriteria`, `prices` and `codelists`.** They
  join the nine the detail views already write without a per-instance discriminator, for the same
  reason, so the same bound applies: a page that already means something of its own by
  `id="prices"` will have a duplicate, and the nav's link lands on whichever comes first.
  `Fhi.Metadata-uobxg` is where that bound gets settled. (Fhi.Metadata-fkiz9)
- **Kelda's sections and the whole variable's Kodeverk now sit in `munin-explorer-page__section`
  wrappers, so `Fhi.Helsedata.Stiler`'s rules for that name reach them.** No new class name.
  `scroll-margin-top` puts a heading reached from the nav clear of a sticky header. And unless your
  `KildeSearch.Sections` draws an element, the last of Kelda's sections — Variabler, or Priser with
  `ShowAccessAndPrices` — is now the column's last child, so Stiler's `:last-child` rule gives it a
  `min-height` of `calc(60vh - 120px)`: expect that much space under a one-line section at the
  bottom of an open kilde. Stiler's reason is a scroll-spy, which could otherwise never mark the
  nav's last entry active; Runa's kilde page already ended in a wrapped section and got the same
  space. (Fhi.Metadata-fkiz9)
- **The variable explorer's filter toolbar now lays out four controls, not three.**
  `munin-explorer-filters__toolbar` holds Utvid alle, Skjul alle, Nivålinjer and now Ikoner, and
  the second switch is a second member at its own natural width: the `min-width: 0` half of the
  rule must keep selecting the two fold buttons alone, because with it the switch collapses to one
  character wide. **The row has to be allowed to wrap.** Four controls are 291px of content, and at
  the 320px width WCAG 1.4.10 names the component's mount is 226px — measured, the row hung 42px
  past the viewport, since only the fold buttons shrink and a switch keeps its intrinsic width.
  Both sample stylesheets add `flex-wrap: wrap` to `munin-explorer-filters__toolbar`;
  `Fhi.Helsedata.Stiler` gains the same rule under `Fhi.Metadata-xfsvx`, and until that release a
  host on a published Stiler needs the one declaration itself. A host that wrote a toolbar rule of
  its own has one more member to fit and the same answer to give it.
  No new class name comes with the switch: it wears `munin-explorer-switch` with
  `munin-explorer-switch__track` and `munin-explorer-switch__thumb` inside it, exactly as
  Nivålinjer does, so a host already drawing that control draws this one. A host on a published
  Stiler still draws none of the three itself — that release is `Fhi.Metadata-aonvl` — and now has
  two bare `<button>`s whose appearance never changes with their state rather than one.
  (Fhi.Metadata-kd9ts)
- **Stiler 0.1.68 carries the active-filter chips, the results toolbar and the kilder table's
  sticky-header thresholds.** The README said no published Stiler had the thresholds yet and
  pointed at 0.1.42 as the version pinned here; 0.1.68 is the version helsedata.no pins, and it has
  all three. The branch disclosure rules for the variabelutforsker's Kilde facet are not in 0.1.68;
  0.1.75 has them.
- **BREAKING for hosts without Fhi.Helsedata.Stiler: the variable filter panel starts folded at
  every width unless the host carries the `munin-explorer-filters__*` fold rules.** The panel is folded behind
  "Vis filtre" by the `hidden` attribute, so a stylesheet with none of the rules shows the toggle
  and a folded panel on a desktop as well, while one with a reset such as
  `fieldset { display: block }` holds the panel open behind a toggle that says it is folded. `.munin-explorer-filters__facets[hidden]` needs
  `display: none`, and once there is room for a sidebar `.munin-explorer-filters__toggle` needs
  `display: none` while `.munin-explorer-filters__facets[hidden]` needs `display: block`.
  `Fhi.Helsedata.Stiler` already carries all three. The fieldset itself wears
  `munin-explorer-filters__facets`. The toggle sits beside that fieldset rather than inside it, so
  a host rule scoped under `.munin-explorer-filters` does not reach the button.
  (Fhi.Metadata-l9l2n.102)
- **The variabelutforsker's facet panel emits one new class name,
  `munin-explorer-filters__groupcount`.** It is the count beside a kildetype group in the Kilde
  facet, and it replaces `munin-explorer-filters__chosen` there; `__chosen` itself is unchanged and
  still worn by the kildeutforsker's facet summaries, so a host styling it needs no edit. A handle
  rather than a name that carries meaning — the digits are markup, so an undefined one is still
  read and announced, and what a rule buys is the dimming and `font-variant-numeric: tabular-nums`.
  The tabular figures are the half worth copying: the counts stack, and two of different digit
  widths above one another shift sideways as the facet is narrowed without them. The rule is merged
  on `Fhi.Helsedata.Stiler`'s `main`, added to `__chosen`'s own selector list rather than copied
  beside it, and it is **not in any published Stiler yet** — so every host draws these counts at the
  page's own size and colour today, whatever its pin, and there is no newer pin to move to. Which
  version will first carry it cannot be named from here: Stiler's csproj sits at `0.0.0-local` and
  its pipeline stamps the real `0.1.x` when a release is cut, so the number exists only once that
  happens. The merge is recorded in bead `Fhi.Metadata-l9l2n.73` — its close note names PR 39274,
  merged 2026-09-11 — which is the only record of it this repository can reach; nothing here reads
  Stiler. Both sample stylesheets show the rule. (Fhi.Metadata-l9l2n.104)
- **The kildeutforsker's hierarchy emits one new class name, `munin-explorer-hierarchy__open`.**
  It is the link that opens a datasamling in the tree. A handle rather than a name that carries
  meaning — undefined, it is an ordinary `<a href>` and draws at the browser's own link style, so
  what a rule buys is putting it back on the node's own line, since it is written after the
  node's `<details>` rather than inside the `<summary>`. Both sample stylesheets show that rule.
  No rule for it ships in `Fhi.Helsedata.Stiler` yet. A host on a Blazor `Router` should also know
  that opening a datasamling remounts `KildeSearch`: the press is intercepted, `KildeExplorer`
  reads the new address and rebuilds rather than forcing a page load, so the open kilde is fetched
  again and its hierarchy comes back collapsed. (Fhi.Metadata-l9l2n.107)
- **An `<h2>` now renders inside the contents nav, and it carries no class of its own.** The nav was
  `<nav aria-label>` wrapping `ul.form-menu__list`; it is now that same nav with a heading in front
  of the list. The element is deliberately bare, because helsedata's own contents nav draws an
  unclassed `<h2>` in exactly this position and `Fhi.Helsedata.Stiler` already styles it there — a
  new class name would have needed a new rule, and a name with no rule renders at the browser's own
  heading size. A host carrying neither gets a default `<h2>` above the list, which is a title above
  the thing it titles rather than anything broken. (Fhi.Metadata-l9l2n.116)
- **The kilde table's sortable headings emit one new class name,
  `munin-explorer-kilder__sort`.** It is the button inside each of the four sortable `<th>`s, worn
  beside Stiler's own `hd-button-reset`, and it is a handle rather than a name that carries
  meaning: undefined, the heading is still a real button, still reachable by Tab, and the sorted
  column is still marked by the arrow beside its word and by `aria-sort` on the cell. What a rule
  buys is the hover underline and the colour on the sorted column. `Fhi.Helsedata.Stiler` adds this
  name to the two rules the variabelutforsker's header button already has — merged 2026-09-11 in PR
  39282, recorded in bead `Fhi.Metadata-l9l2n.106` — rather than giving it a block of its own, so
  the two cannot drift apart. Whether a published Stiler carries it cannot be read from this
  repository; nothing here reads Stiler. Both sample stylesheets show the rule.
  (Fhi.Metadata-l9l2n.88)
- **Two new names size the save column: `munin-explorer-dataitem-main__save` on the button's
  cell and `munin-explorer-dataitem-header__save` on the header cell above it.** Give both one
  fixed width, `flex: 0 0 10rem`, where the result row is a flex row. Without it the cell sizes
  to its button, so the "Variabelliste" header does not sit over the buttons, and pressing one
  shifts the later columns in its row as the label changes length. The rule ships in
  `Fhi.Helsedata.Stiler` under Fhi.Metadata-6u1iu, together with the header geometry the
  alignment depends on: the header row loses its 36px left padding, Navn's header button takes a
  3rem indent instead, and the Kode, Datatype and Status header cells get the 16px left padding
  their values have. A failed save's sentence is now its own cell of the row,
  `munin-explorer-data-list__save-status`, after the column strip: `.munin-explorer-data-list__item__row`
  wraps, the cell takes the full width, and its alert is indented `3rem` with `12px` below while it
  has text. Until a host's pinned Stiler carries that, the "Variabelliste" header shows but the
  header cells sit out of line with the values under them, and a failed save's sentence sits in a
  narrow cell beside the row's columns, squeezing that one row, instead of on a line below it.
  (Fhi.Metadata-q7i5e)
- **Two class names to know about, and no rule of yours to write on a Stiler that has
  `components/munin-explorer/` from 0.1.75 onwards.** The legend wears
  `munin-explorer-filters__legend` on the list and `munin-explorer-filters__legend-item` on each
  row, and the glyph inside a row wears `munin-explorer-filters__icon` — the same name a facet
  value's glyph already wears, as a direct child of the row rather than inside the
  `munin-explorer-filters__icons` slot, which is what Stiler's rule for it selects. Both sample
  stylesheets already carried the rules before any markup wore the names, copied off the published
  0.1.75 and compared since against the 0.1.79 `samples/HostileHost` pins, so
  `scripts/assert-sample-css-matches-stiler.sh` against that pin is the whole of the evidence on
  this side that Stiler has them; nothing in this repository reads Stiler. Handles, both: the
  `<svg>` carries its own `width`, `height` and `stroke` and the names are real text, so a host
  that defines neither gets a list of glyphs and words at browser defaults and loses no word. What
  the rules buy is the row each pairing sits on and the columns the eighteen of them are laid out
  in. (Fhi.Metadata-zllxt)

## 0.1.0-alpha.11 — 2026-09-11

### Added

- **A long facet in the kildeutforsker gets a search box over its own values.** Databehandler has
  39 values on the live catalogue, so finding one meant reading all of them. A facet with **more
  than ten** values now draws a small search field inside its disclosure; the threshold is one
  number applied to every facet, never a decision taken per facet, so kildetype's five values stay
  a plain list. Typing narrows that facet's values and nothing else — not the result table, not the
  counts, not the other facets, and not the ticks: a value that is ticked and then typed out of
  sight is still ticked and still narrowing the list, and clearing the box brings it back with its
  tick on. It is counted over the values the facet has rather than the ones its own search leaves,
  so the box does not disappear as it starts working. The values are not merged or normalised:
  four spellings of Folkehelseinstituttet are still four choices with four counts, because deciding
  that two strings name one organisation is a claim about the catalogue and not about the view.
  The box carries no class name of its own — it is a native text input inside the filter panel, on
  the same terms as the dataperiode facet's date fields, so there is nothing new for a host to
  style beyond the form fields it already styles. Committing the search keeps the reader's place:
  focus returns to the box only when the redraw takes away a value they could have been standing
  on, so tabbing or clicking out of the box is never undone under them. (Fhi.Metadata-6we8a)
- The kilde detail view now loads an expandable hierarchy of delkilder, datasamlinger and
  variabelgrupper, initially collapsed. Descriptions and validity periods remain available
  in a separate disclosure. This is shared by the kilde explorer, the variable explorer's
  source view, and hosts mounting `KildeView` directly.
- Hosts can also render `KildeHierarchyView` with a `KildeId` and optional `Language`.
  `KildeView` now needs the registered `IMuninExplorerClient` to load the hierarchy.
- Hierarchy retries retain keyboard focus and announce completion. Rate-limited requests
  show the throttling message without enabling another retry; nested lists keep explicit
  list semantics when hosts hide their markers.
- **The kilder table's scroll box now says how many columns it is holding.** Beside
  `munin-explorer-kilder-scroll` the box wears `munin-explorer-kilder-scroll--cols-N`, where N is
  the number of header cells the table actually rendered rather than the number of choices the
  column picker offers — the picker reaches ten of them and the table draws four or five more that
  it cannot. A stylesheet that has to vary the box by how wide the table is now has something to
  select on, which is a question only the package can answer. (Fhi.Metadata-l9l2n.103)
- **The kildeutforsker's facet panel gets Utvid alle and Skjul alle.** Since the facets began
  folding, a reader who wanted to see all of them opened them one at a time — thirteen facets on
  the live catalogue, one of them open to begin with. The pair sits in a row at the top of the
  panel, above the facets and so before them in the tab order, and each is an ordinary button with
  no state of its own to drift: the disclosures still carry `open` and nothing else, exactly as
  they did. A press is announced to a screen reader, since it rewrites the whole column and is
  otherwise silent. What a press does not cost is the fold belonging to the reader: after Utvid
  alle they can still collapse one facet, and ticking a value leaves it collapsed. The panel gets
  no pair at all with fewer than two facets, where there would be nothing to fold together, and
  Nivålinjer is deliberately not offered beside them — these facets are not nested, so a
  level-lines toggle would draw nothing. English hosts get "Expand all" and "Collapse all".
  (Fhi.Metadata-l9l2n.60)
- **`HierarchyVariabelgruppe` gains `PresentationOrder`.** The hierarchy endpoint sends
  `presentationOrder` on a variabelgruppe as it does on the delkilder and datasamlinger above it,
  and the contract had nowhere to put it, so a host reading the tree could not see the curated
  order at all. `int?`, null when unordered, the same shape the two neighbouring records already
  use. (Fhi.Metadata-l9l2n.61)
- **The kilde list can be sorted, and the order is in the link.** `KildeSearch` draws a
  "Sorter etter" select above the table offering Navn A–Å, Flest variabler, Sist endret and
  Opprettet beside the order the catalogue sent, and `KildeExplorer` reads and writes that order as
  `?sort=` next to `?kilde=` — omitted while the list is in the order it arrived in, so links made
  before this release still mean what they did. The sorting is done in the browser over the rows
  the search and the facets left: `GetKilderAsync` is not paged, so the whole catalogue is already
  in hand and nothing here is asked of the API. Names are collated as `nb-NO`, pinned, whatever
  language the reader has asked for, so æ, ø, å and the digraph aa all come at the end of the
  alphabet — a kilde spelled Aa sorts beside Å rather than beside A. A kilde with no value to order
  by sorts last in every order, never among the smallest, while a recorded zero is ordered as
  zero. A host owning its own query string mounts `KildeSearch` and binds the new
  `Order` / `OrderChanged` pair. (Fhi.Metadata-lhdh0)
- **The Kilde facet in the variabelutforsker reaches datasamlinger.** The source tree stopped at
  delkilde, which is the level that 41 of the catalogue's 44 kilder do not have: 203 datasamlinger hang
  straight off a kilde and none of them could be picked. `FilterOptions` now carries a
  `datasamlinger` facet, and each value hangs under its delkilde where it has one and under its
  kilde where it has none, at any depth. In The Tromsø study that is Tromsø1, Tromsø2 and Tromsø3
  becoming selectable beside the two waves that were already there. The counts are cross-filtered
  like every other value in the panel, a datasamling with no matches is left out as a delkilde
  already is, and the trail step over the results reads a chosen datasamling's name off the facets
  rather than off whichever rows happen to be on screen. Against an API that does not send the
  facet the field is empty and the panel is exactly what it was. A datasamling counts in the
  facet's own search the way a delkilde does — typing its name keeps its kilde — and a chosen
  one draws a chip over the results whether or not that search is showing it.
  (Fhi.Metadata-mgp03)
- **The kildeutforsker says which filters are active, over the results rather than only inside the
  panel.** With the facet panel folded away on a narrow screen — or simply scrolled past — the only
  thing on screen saying a list of 56 sources out of 66 had been narrowed was a clause in the count
  line, which names no value. A row of chips now sits above the table, one per ticked facet value,
  each with its own remove control naming the value it clears, and a "Fjern alle filtre" beside them
  that empties every facet in one press. The row is drawn only while something is ticked. Every
  press writes the same state the facet checkboxes write, so the panel, the chips and the rows
  cannot disagree about which filters the list obeys. The count line is unchanged. (Fhi.Metadata-ofoyw)
- **The kilde detail hierarchy draws Kelda's node icons.** A delkilde wears a folder, a datasamling
  wears one glyph per datakategori it carries, and a variabelgruppe wears none - the same mapping
  Kelda's own tree uses, legacy category slugs and all, so the two surfaces cannot show one
  datasamling as two different things. Several categories draw in a fixed order and never twice; an
  unrecognised token draws the vocabulary's catch-all, while carrying no category at all draws
  nothing, because absence is not "Annet". The glyphs are decorative and `aria-hidden`, so a
  datasamling's categories are read out in words beside them instead. `ShowNodeIcons` on
  `KildeHierarchyView` and `KildeView` turns the icons off without touching the variable counts.
  (Fhi.Metadata-s3l7l)

### Changed

- **Nothing a host renders changes.** The forced `checked` update on the column picker's and the
  facet panel's checkboxes is now guarded by a browser-driven check as well as by unit tests, which
  cannot see the browser's own flip of a box; and the comment beside the facet call named two
  refusal paths where only one needs it, since a rolled-back fetch corrects the DOM on its own.
  (Fhi.Metadata-1s7z1)
- **The variabelutforsker takes its kildetype words from the API rather than from a table shipped
  inside the package.** `GET /api/explorer/filters` resolves `kildeTyper[].displayName` from the
  Kilde-scoped Kildetype master data and follows `Accept-Language`, so an edit there now reaches
  the page. The shipped table stays as the fallback for an API that answers with the raw enum name
  or with no `displayName` at all. Checked against the test API on 2026-09-10 the two agree word
  for word on all eight values in both languages, so no visible text changes today.
- **The kilde facet's kildetype headings say what the facet button above them says.** A kildetype
  Munin adds that this package has no word for used to read as prose on the button and as its bare
  token — `nyKildetype` — on the heading directly beneath it, in the same panel. Both now take the
  API's word, as does the kilde trail in an opened result row. Where neither the API nor the table
  has a word, all three now say the token itself rather than one of them saying "Ikke oppgitt".
  (Fhi.Metadata-3n6e1)
- **A kilde with nothing counted now reads as empty rather than as a measured value.** The
  Delkilder, Datasamlinger and Variabler cells of the kilder table mark a count of nought, so
  Hjerte- og karregisteret's two noughts recede from a reader scanning the column for kilder they
  can use. The digit is still drawn and still read out — nought is a measurement here, and the
  columns beside it say "Ikke oppgitt" for a field nobody filled in, so the two have to stay
  distinguishable. Nothing else about the row changes: the counts were already right-aligned with
  tabular figures, and the Opprettet column is untouched because it holds the year the source wrote
  rather than a number this component counted. (Fhi.Metadata-8vbqf)
- **The package's Microsoft dependencies move from 10.0.11 to 10.0.12.** They are servicing
  patches with no API change, so nothing a host compiles against moves. What does move is the
  floor, and for more than the five references the package names directly: every Microsoft
  10.0.x package it pulls resolves at 10.0.12 too, all 21 of them, transitives included —
  `Microsoft.JSInterop`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Primitives`,
  `Microsoft.Extensions.DependencyInjection` and `Microsoft.AspNetCore.Components` among them,
  all plausible things for a Blazor host to pin. A host that pins any of the 21 at 10.0.11
  itself gets NU1605 on restore — an error, not a warning, since the .NET SDK raises it — and
  has to move its own pin to 10.0.12 or drop it. (Fhi.Metadata-aos2h)
- **The kildeutforsker's facets fold.** Every facet used to render open at once — the databehandler
  facet alone has 39 values on the live catalogue — which left the filter column longer than the
  list it filters. Each facet is now a native `<details>`: the first starts open so the affordance
  is visible, the rest start folded, and each folds independently from there. A folded facet's
  summary carries the number of values ticked inside it, so a filter cannot narrow the list from
  behind a closed disclosure without saying so. Opening a kilde and coming back leaves the search
  and the ticked values as they were and the folds back at their defaults, because the fold belongs
  to the browser and the panel is rebuilt. The same shape the variable explorer's own panel already
  uses, under the same `munin-explorer-filters` handle, so the fold itself asks nothing new of a
  host. (Fhi.Metadata-co3sf)
- **The filter tree's level guides are on when the panel first renders.** `LevelLines` now
  defaults to `true`, so `data-level-lines="true"` is on `munin-explorer-filters` from the first
  paint and `Nivålinjer` turns the guides off rather than on. Runa's own tree loads with its
  toggle pressed, and a reader who never finds the button was reading a deep tree with no guides
  at all. A host that stores what `LevelLinesChanged` raises is unaffected — whatever it passes
  back still wins; a host that stores nothing gets the guides at every visit. Pass
  `LevelLines="false"` to keep the previous state. (Fhi.Metadata-dfygj)
- **The column picker is now real checkboxes, and its trigger sits on the right.** Each optional
  column is an `<input type="checkbox">` inside a `label.form-control`, where it used to be a
  `<button aria-pressed>` - a checkbox reads as a multi-select and a pressed button as a toolbar
  toggle, and assistive technology announces them differently. The trigger gained helsedata's own
  two icons, `icon-layout` leading and a chevron that follows the open state, and no longer emits
  an inline `style="position:relative"`: Stiler positions `.munin-explorer__dropdown` itself. Both
  explorers draw this control from one copy, so the kildeutforsker and the variabelutforsker
  change together. (Fhi.Metadata-f6az7)
- **A kildetype facet's `DisplayName` is resolved prose in the request's language, not the raw enum
  name.** `GET /api/explorer/filters` used to answer `SentraltHelseregister` there, and
  `KildetypeFacet.DisplayName` was documented as such — so a host was told to supply prose of its
  own. It no longer has to: the API resolves the label and follows `Accept-Language`, giving
  `Sentralt helseregister` under `nb` and `Central health registry` under `en`. Two consequences for
  a host that was reading it. Key off `Value`, which is unchanged and language-independent, wherever
  identity matters — `DisplayName` now differs between languages. And the facet list is ordered by
  that resolved label rather than by the value, so `kildeTyper` arrives in a different order in each
  language; a host mirroring the API's order elsewhere on the page inherits that. This package is
  one such host. The words it puts on a kildetype are still its own, looked up by `Value` — those
  did not change — but it draws the kildetype facet, and the kilde headings grouped under it, in
  the order the API sent. Under `nb` that is the order it always was; an English mount now sorts
  them by English prose, so which kildetype heads the list depends on the English words rather
  than on the enum. (Fhi.Metadata-iv9xp)
- **A folded facet in the kildeutforsker says how many of its values are ticked in words, beside
  the heading rather than inside it.** The number used to be appended to the facet's own heading
  text, which put it inside the thing a screen-reader user navigates the panel by and left it to
  say only "(2)" — a figure with no noun. The summary now reads "Kildetype 2 valgt", with the count
  as its own element next to the heading, so that whole sentence is what the disclosure is
  announced as. It is drawn only while at least one of the facet's values is ticked: an untouched
  facet says nothing rather than "0 valgt", and clearing the last tick takes the count away again.
  English hosts get "2 selected". The heading is still a heading at the same level, and the
  disclosure is still the native `open` state with no `aria-expanded` beside it. The summary line
  is laid out as a row to hold all three, which is also what takes the facet's disclosure marker
  off the row of its own it had fallen to — that rule is the host's, and both sample stylesheets
  now carry it. (Fhi.Metadata-l9l2n.53, Fhi.Metadata-l9l2n.58)
- **The kildeutforsker's result count says how many of the catalogue you are looking at, and how
  many filters are narrowing it.** The line over the list used to say only the total it was
  currently showing, so a narrowed list read exactly like a short catalogue and nothing on the page
  said filtering was happening at all. It now reads `56 kilder av 66, avgrenset av 2 filtre` in
  Norwegian and `56 sources of 66, narrowed by 2 filters` in English — the variabelutforsker's own
  words for the same fact, in the same place in the sentence, so the two explorers do not tell it
  two ways. Both clauses are absent rather than zeroed on an untouched list, which still reads
  `66 kilder`: `66 kilder av 66, avgrenset av 0 filtre` is more words saying less. The filter count
  is the same number the empty state already reports, so a narrowed list and a list narrowed to
  nothing cannot name different filters. The sentence is both the polite status line and the
  table's accessible name, as before, so a screen reader hears the change too. No new class name
  and nothing new for a host to style — the three controls above the table still take a row each,
  which needs a Stiler rule and is tracked separately as Fhi.Metadata-xpv2x and Fhi.Metadata-tciss.
  (Fhi.Metadata-l9l2n.54)
- **BREAKING for hosts: every kildetype on the contracts is `string?`, because the API sends null
  for a kilde that has none.** A kilde with no kildetype is a real state in the catalogue —
  `K_NKR-NAKKE` is one today — and the API says so with an explicit `null`. The contract still
  declared a non-nullable `string`, so it told hosts something that was not true: a host
  deserialising with plain `System.Text.Json` got that null written straight over the `= ""`
  initialiser and found it wherever it first read the property, which on a Blazor Server host is
  the circuit and the page. Seven properties change: `KildeSummary.Kildetype`,
  `KildeDetail.Kildetype`, `VariableDetail.KildeType`, `KildeFacet.KildeType`, and the
  `EffectiveKildetype` on `DatasamlingDetail`, `KildeDatasamling` and `KildeDelkilde` — those last
  three are always the owning kilde's kildetype, so they are null exactly when it has none.
  **What a host must do:** handle null where it reads one of these. The compiler now says where,
  which is the point of the change; coalesce to your own word for an unset kildetype, as this
  package renders its reader's — "Ikke oppgitt" under `no`, "Not specified" under `en`.
  `VariableSummary.KildeType` was already `string?` and is unchanged. Nothing changes on the page:
  the client's `NullAsEmptyStrings` modifier never covered a nullable string, so the components go
  on reading these through `Texts.KildeTypeLabel`, which has always taken a null — the kilder
  table's cell, the kilde, datasamling and variable views, the variable detail panel's crumb, and
  the kildetype facet, which drops a kilde with no kildetype rather than offering an unnamed
  checkbox. (Fhi.Metadata-l9l2n.61)
- **The variabelutforsker's filter panel opens on Kilde alone, with a search box in it, and the
  kildetype groups under it fold.** The panel used to open with every facet expanded, which put
  hundreds of controls above the results before the reader had narrowed anything. Exactly one facet
  is open at first paint now and it is Kilde, because that is the one a reader starts in — and the
  first thing under its summary is a box that narrows the kilder, matching a kilde's own name or any
  of its delkilder's. The box narrows only what the panel draws: a kilde ticked and then typed out
  of sight stays ticked, keeps narrowing the list and is still counted in the facet's own heading,
  and emptying the box brings it back with its tick on. A term that takes kilder away puts focus
  back on the box, so committing one with Tab does not drop the reader onto a row the same render
  removes. Inside that facet the kildetype headings are disclosures of their own, closed, each
  saying how many kilder it holds, so the facet opens on three rows rather than on 46. Utvid alle
  and Skjul alle still reach every disclosure in the panel, the groups included. (Fhi.Metadata-l9l2n.67)
- **The variabelutforsker says which filters are active, and its count shares a row with Kolonner.**
  The facet panel is the only thing that ever named a chosen value, and it can be folded away,
  scrolled past or simply longer than the screen — so a reader looking at 630 rows out of 15 020 had
  a clause in the count line and nothing else. A row of chips now sits above the results, one per
  chosen value across every facet the explorer filters by, each with a remove control naming the
  value it clears. "Fjern alle filtre" moves into that row from the foot of the panel: it is the
  same control, moved rather than copied, so there is still exactly one of it on the page, and the
  row is drawn only while something is chosen. Every press writes the same filter the facet
  checkboxes write, so panel, chips and rows cannot disagree about what the list obeys. Below it the
  result count and the Kolonner picker now share one row where they took a row each. The count is
  the same sentence in the same polite live region, still naming the ordering, and the ordering
  itself stays on the column headings and Per side stays at the pager — a second control for either
  would be a second thing to keep in step. Reading order and tab order are unchanged.
  (Fhi.Metadata-l9l2n.68)
- **The variabelutforsker's hit list no longer carries the variable code, which is in the opened
  row and in the Kolonner picker instead.** A code does not help a reader CHOOSE a variable — it is
  what they ask for once they have — and it was the widest column of the eight, because a code is
  one unbreakable token with nowhere to wrap. That width now goes to the name and the datasamling,
  which is what a researcher scans by. Nothing is lost: the code is in the Identifikasjon block of
  the panel an opened row shows, whatever the picker says, and Kolonner still offers it as a column
  for a reader who wants to scan it — off to begin with, like Status, and their choice sticks for
  as long as the page does. The name plus the datasamling is what tells two variables apart in any
  case: Barnediabetes alone holds six called "Pasientens alder", and the code was never the thing
  doing that work. The variable detail page and the reader's saved variable lists are unchanged and
  still show the code, since those are what an applicant attaches to an application. A host needs
  no new rule for this — no class name is added, renamed or removed, and the column's existing
  `munin-explorer-dataitem-*__code` width is what dresses it whenever it is turned back on.
  (Fhi.Metadata-l9l2n.69)
- **Neither explorer's filter panel counts the filters in its title any more.** The variabelutforsker's
  legend read "Filtre (3)" and the kildeutforsker's heading read the same, over a count line already
  saying "avgrenset av 3 filtre" and a chip row already naming each of the three. The title was the
  least useful of the three statements — it said how many without saying which — so both now read
  "Filtre" alone, and the count line and the chips are unchanged. The panel keeps its accessible
  name: the variabelutforsker's fieldset still announces as "Filtre", and the kildeutforsker's
  fold toggle still reads "Vis filtre" / "Skjul filtre". (Fhi.Metadata-l9l2n.83)
- **Nivålinjer is a real switch now, not a button that stays pressed.** The toggle over the
  variabelutforsker's facet tree was a `<button>` carrying `aria-pressed`, which a screen reader
  announces as a button held down; it is a `role="switch"` carrying `aria-checked` now, which
  announces as on and off — what the control has always meant. It is still a native `<button>`, so
  it keeps the keyboard behaviour a button has: Tab reaches it and Space activates it, with no key
  handler of ours in the way. Its label and its position in the toolbar are unchanged, and it still
  writes the same `data-level-lines` marker on the panel, so nothing a host stores or styles for the
  lines themselves changes. What did change is the control's own markup: it wears
  `munin-explorer-switch` alone, with a track and a thumb inside it, and no `hd-button-square` or
  `button-square--*` beside it — see the note for hosts. (Fhi.Metadata-l9l2n.87)
- **The kildeutforsker's result count, Sorter and Kolonner share one row above the table.** They
  took a row each, so a reader scrolled past three rows of chrome to reach the first kilde — and on
  a phone, where the facet panel is folded away, that was most of the first screen. The three now
  sit on one line, the count at the left and the two controls at the right, wrapping onto their own
  lines when there is no room rather than pushing the table further down. Nothing about what they
  say has changed: the count is the same sentence in the same live region, announced the same way,
  and the picker is still drawn only when there are rows to have columns. Reading order and tab
  order are unchanged — the row is laid out with flex rather than reordered — and the Sorter label
  still names its own select. (Fhi.Metadata-tciss)

### Fixed

- **Kilde and variable detail pages no longer show the catalogue's storage vocabulary in a
  field's label.** "(språkmerket)" and "(flerspråklig)" - and the English "(language-tagged)" /
  "(multilingual)" - describe how the catalogue stores a value, not something a reader needs;
  they are stripped from every label and group name at render time, in both languages.
- **Formål no longer renders twice on a kilde page.** `Formaal` and its EHDS/HealthDCAT-AP mirror
  `FormaalFlerspraklig` curate the same prose in every fixture that populates both, so the plain
  field is now dropped from the metadata list once the mirror also holds a value; a source
  curating only the mirror still shows it. `Rettslig grunnlag`/`hasLegalBasis` and
  `Tittel`/`TittelFlerspraklig` were suspected of the same duplication but are NOT deduplicated:
  real fixtures show their EHDS mirror can carry a translation the plain field lacks, so hiding it
  would delete content rather than tidy the page. Both still render, and both still lose the
  "(språkmerket)"/"(flerspråklig)" qualifier from their label. (Fhi.Metadata-43jrq)
- **A signed-out reader is now told that variable lists exist and require signing in.** Where the
  Variabelliste tab would otherwise sit, `VariableExplorer`/`VariableSearch` now draw one sentence
  instead of nothing at all — no tab, no save button and no filter panel ever appeared for a reader
  who had not signed in, and nothing on screen said why. The host's own sign-in control is
  untouched: this is text, not a second login button, and it carries no link. (Fhi.Metadata-4ifsa)
- **Creating a variable list no longer reads the list being left.** Two unawaited reads for the
  outgoing list used to reach the API on every create, spending part of the per-address rate
  limit's shared budget for nothing - their answers were already discarded. (Fhi.Metadata-7x62u)
- **Datatype now reads in the page's own language instead of English.** `Streng`, `Heltall`,
  `Boolsk` and the rest were only matched against the numeric code a fully re-normalized variable
  carries; a variable still holding its pre-normalization value (`"String"`, `"Integer"`, …) fell
  through to the raw English word on a Norwegian page. Codes and the legacy aliases now resolve to
  the same name on the variable detail and the datatype facet. (Fhi.Metadata-88fui)
- **Double-clicking "Vis koder" leaves the kodeverk code table open** - The second click of a
  double-click gesture toggled the code table straight back shut, so the line flashed and the
  reader landed where they started. Deliberate repeated pressing still toggles both ways, and
  Enter or Space on the control is unaffected. (Fhi.Metadata-fy747)
- **Saving a variable when the API answers 401/403 now tells the reader to sign in, not to try
  again.** A host that declares `IsAuthenticated` true while its token provider sends nothing the
  API accepts used to draw enabled save buttons that failed with "try again shortly" — advice
  that could never work, because the failure was never one a retry could fix. The my/lists write
  and read methods on `IMuninExplorerClient` now throw the new `MuninExplorerUnauthorizedException`
  for a 401/403 instead of the general `HttpRequestException`; a host with its own implementation
  of the interface should throw it too. (Fhi.Metadata-h5o3o)
- **Double-clicking or shift-clicking a control in a variable's panel leaves what it opened
  open** - "Vis datakilde", "Vis datasamling", the kilde step of the panel's trail, "Vis hele
  variabelen" and its way back, and a version row in the whole variable: the second click of a
  double-click toggled the view straight back shut, so it flashed and the reader landed where
  they started. A shift-click extending a selection across the panel's text did the same, at
  those controls and at "Vis koder". Both gestures are now refused the way a result row already
  refused them; deliberate repeated pressing still toggles both ways, and Enter or Space on the
  control is unaffected. (Fhi.Metadata-j1j3i)
- **"Vis hele variabelen" no longer promises a disclosure it does not make** - the button carried
  an `aria-expanded` that was always `false` and an `aria-controls` naming an element that is
  only in the document once the button is gone. It opens a view in place of the list rather than
  expanding anything beside itself, so it now carries neither, matching the trail's kilde step.
  (Fhi.Metadata-j1j3i)
- **Double-clicking a variable row's name leaves the detail panel open** - The name is the row's
  disclosure, and the second click of a double-click gesture toggled the panel straight back shut,
  so the row flashed and the reader landed where they started. Deliberate repeated pressing still
  toggles both ways, and Enter or Space on the name is unaffected. (Fhi.Metadata-kbwo3)
- **A saved list's "Sist endret" no longer moves when variables are added or removed.** Munin
  moves `updatedAt` on a rename only, so the stamp the component showed after an add or a remove
  jumped back to the day of the last rename on the next refresh. A host reading
  `VariableList.UpdatedAt` off `VariableListState.Lists` now sees it move on the same change the
  API moves it on; the variable count beside it still moves on both. (Fhi.Metadata-l9l2n.45)
- **A failure inside the explorer now says what it was in the host's log.** Twenty-nine of the
  thirty `catch (Exception)` sites in the package caught the exception and threw it away — the
  thirtieth rethrows — and every one of them now records it through `ILogger<T>` before it writes
  the sentence the reader sees, as do the thirty branches beside them that tell a 429 or a 401 from
  a fault. `LogError` for a failure, `LogWarning` where the outcome is expected, and the exception
  as the first argument so the stack survives. Expected means expected wherever it happens: every
  saved-list path — the mount, a page turn, switching list, create, rename, delete, remove,
  annotate and export — records the rate limiter's 429, and the API's own 401 for a token it will
  not accept, at `Warning` rather than at `Error`. Nothing on screen changed, and nothing changed about when
  it is shown. What did change is that a fault on a host's own server was previously diagnosable
  only by elimination: the kildeutforsker rendering "Kunne ikke laste kilder nå" while the
  variabelutforsker beside it worked took an afternoon across two repositories, the CMS, the
  cluster and the live API, and never reached an answer. The message templates carry a kilde,
  variable or list id, a page number and the like — never a URL, a token, a response body, or
  anything the reader typed, and never the component's own name, which the log category already is.
  A cancelled call writes nothing: the hierarchy view cancels its own fetch on every new kilde, so
  logging above that guard would have made ordinary clicking produce Error entries for a kilde that
  loaded fine. And what the components log through is wrapped, so a host whose sink throws loses a
  log line rather than the page — `Logger<T>` rethrows a provider's failure, and these calls sit
  inside the catches that exist to keep the circuit up. (Fhi.Metadata-l9l2n.47)
- **A datatype no longer reads "String" in a result row beside "Streng" on the variable's detail
  panel and on the datatype facet.** The list rows in the search results and in the reader's saved
  list name a datatype from the API's own facets, and the filters endpoint echoes back the word a
  variable predating the codes was stored as — `displayName` "String" for code `1`, whatever
  language the call asked for. A stored value is now resolved to its code before the facet is
  looked up, so a row holding "String" finds the same facet a row holding "1" does, and both list
  paths and the facet itself then pass the API's word through the same alias table: a legacy
  stored spelling, English or Norwegian, becomes the shipped table's name for the code it means,
  in the reader's own language — "Streng" under `no`, "String" under `en`. Every other name
  reaches the page exactly as the API sent it, so a datatype added on the API's side is named by
  the API rather than by a table frozen inside this package. When no API name reaches a row at all
  — the filters call failed, or has not answered yet, or answered without a name for that code —
  the row now falls back where the facet and the detail panel already did, to the shipped word for
  the code, rather than showing the bare number beside a facet showing a word. A datatype the
  shipped table has never heard of still reads as its code there, as it does on the panel.
  (Fhi.Metadata-l9l2n.49)
- **Pressing a kilder row opens its datasamlinger, as pressing the chevron does.** On helsedata.no
  the row already lit up under the pointer and had no handler behind it, so it looked like a
  control and did nothing when pressed. It now opens the same drawer the expand chevron opens, and
  only where there is something to open — a kilde with no datasamlinger has no chevron and the row
  stays inert. Selecting text in a row is not a press: a drag that begins and ends inside the row
  lands a click on the row too, so a pointer that travels more than a few pixels in either
  direction between press and release leaves the drawer alone, and so does a shift-click extending
  a selection onto the row. A double-click, which is how a short code like K_ALS is taken, opens
  the drawer once instead of flashing it open and shut under the selection being made and asking
  the catalogue twice for the same kilde — so the code under the name stays copyable. The controls
  inside the row keep their own jobs: the name still opens the kilde, and the selection box still
  only ticks. (Fhi.Metadata-l9l2n.55)
- **The kilde hierarchy tree puts variabelgrupper in the catalogue's curated order.** Every other
  level of the tree — delkilder and datasamlinger — has been ordered by `presentationOrder` since
  the tree was drawn; a variabelgruppe was left in name order because the contract had no property
  to read. Tromsø4's first visit now opens on GENERAL INFORMATION, PHYSICAL EXAMINATION, BLOOD
  SAMPLES as the catalogue curated it, rather than on ALCOHOL, BLOOD SAMPLES, COFFEE. A group the
  catalogue has not ordered still falls back to name order, after every ordered sibling. A
  delkilde's unassigned variabelgrupper are curated among themselves but stay behind its
  datasamlinger and child delkilder, where they have always been drawn: a group's number counts a
  sequence of its own — Tromsø4 numbers its datasamlinger 1 and 2 where its groups run 537 to
  1189 — so it is not comparable with theirs, and an orphan does not outrank a datasamling on the
  strength of a smaller number. (Fhi.Metadata-l9l2n.61)
- **The Kilde column names the kilde again where it has no kortnavn.** A kortnavn the Explorer API
  leaves out arrives as null or as an empty string, and the fallback to the full kilde name was
  spelled `??`, which only catches the first — so the result list, and the saved-list view beside
  it, wrote "Ikke oppgitt" over a name they were already holding. Nearly three in five variabler
  are affected. The saved list's kilde filter fell the same way, leaving a checkbox whose whole
  accessible name was its count; it now says "Ikke oppgitt" where the list names a kilde neither
  long nor short, takes a later entry's name for a kilde whose first one carried none, and sorts
  by what the checkbox says. (Fhi.Metadata-l9l2n.62)
- **Double-clicking a kilde row's expand chevron leaves the datasamlinger open** - The second click of a double-click gesture toggled the drawer straight back shut, so the row flashed and the reader landed where they started. Deliberate repeated pressing still toggles both ways, and Enter or Space on the chevron is unaffected. (Fhi.Metadata-l9l2n.72)
- **Pressing a variabelutforsker row opens its panel, as pressing the variable's name does.** On
  helsedata.no the row's column strip already computed `cursor: pointer` and had no handler behind
  it, so it looked like a control and did nothing when pressed — the same complaint that made the
  kildeutforsker's rows pressable. The name keeps being the disclosure: it is the button that
  carries `aria-expanded`, it is what the panel is named after, and Runa gives it no second
  destination to be freed up for, so the row is a pointer shortcut onto it and adds no tab stop.
  Selecting text in a row is not a press: a drag that begins and ends inside the row lands a click
  on it too, so a pointer that travels more than a few pixels in either direction between press and
  release leaves the panel alone, and so does a shift-click extending a selection onto the row. A
  double-click, which is how a short code is taken, opens the panel once instead of flashing it
  open and shut under the selection and asking the catalogue twice for the same variable. The
  controls inside the row keep their own jobs: the name still toggles exactly once, and "Lagre i
  liste" still only saves. (Fhi.Metadata-l9l2n.81)
- **A drag over a row's own controls is a selection in both explorers, and an abandoned press no
  longer swallows the next click.** Highlighting a variable's name — or a kilde's — and dragging
  across the row released the pointer over the row rather than the button, and the row read that as
  a press and opened. It now measures the gesture that began on the control, because the press
  reaches the row while the click still stops at the button. A gesture that begins *and* ends
  inside one of those controls is a selection as well, so copying a variable's name no longer opens
  the panel over the words being copied, and copying a kilde's no longer takes the reader off the
  list. The other half is what happens when a gesture never reaches a click at all: a right-click
  on the row, or a drag released outside the window, left a coordinate standing that the next click
  was measured against, so a row activated by speech control or another assistive tool could
  silently do nothing. A press is now settled when the pointer comes up and belongs to that gesture
  alone, and a click that reports no click count — which is how that tooling and the keyboard
  activate a row — is always a press. (Fhi.Metadata-l9l2n.81)
- **Ticking one value in the variabelutforsker's filter panel draws one active-filter chip.** A
  delkilde, variabelgruppe or saved filter the facet payload listed twice - once under a parent
  that is in the payload, once as an orphan - was drawn as two checkboxes, so one press ticked both
  copies, the facet's own count said two and the row over the results showed two chips for one
  filter. A repeated id is now placed once however many times the payload names it, keeping the
  copy that hangs off a parent that is present rather than the copy listed first, so where a value
  sits and the words it carries are the payload's meaning rather than its order. The kilde facet -
  the one facet reading its ticks off the payload rather than off the drawn tree - collapses its
  delkilder on those same terms, so a chip and the checkbox it stands for can never keep copies
  naming one filter two ways. A repeated kilde is collapsed the same way, by id alone, since a
  kilde carries no parent; that one was also a crash, because two checkboxes drawn under one key
  took the whole panel down at the first render after a press. The kildeutforsker's chip row is
  unchanged and was never affected: it walks four fixed facet definitions over a set of ticked
  values apiece, so it cannot name one value twice. (Fhi.Metadata-l9l2n.82)
- **The kilder table's expand control now clears the 24 x 24 minimum target size.** It drew a
  literal "+", and since `hd-button-reset` strips a button's padding and font, that narrow glyph was
  most of the control: measured, the box was 20 x 24 CSS px, under what WCAG 2.5.8 asks for. It now
  discloses with helsedata's chevron — Stiler's `icon` is a 24px box — so the kilder table and the
  variable table open a row the same way and both controls are big enough to hit.
  (Fhi.Metadata-mpx2p)
- **The variabelutforsker's filter panel and its active-filter chips now say which language their
  words are in.** Every chip over the results passed no language at all, so a screen reader on an
  English page announced "Tromsøundersøkelsen" with English phonetics (WCAG 3.1.2) — the marking
  the kildeutforsker's own chips and facet labels have carried from the start. A chip drawn from a
  catalogue value now carries `lang` on those words alone, never on the capsule or on the remove
  control whose name is this package's prose, and the checkbox that chip stands for carries the
  same marking, so one kilde is never named two ways on one page. Words that are this package's
  own — a kildetype, a datatype, a level's fallback word, the catch-all's yes/no filters — carry
  none, and neither does a token belonging to no language: a datakategori CURIE, an OID, a code,
  or a helsefaglig kodeverk's short name, which is the catalogue's key and holds ICD-10 beside
  DÅR. Both sweeps that build the row decide it the same way, so one kilde's chip cannot differ
  from another's on nothing but which sweep drew it. (Fhi.Metadata-o49mx)
- **The hierarchy trail no longer offers a second way to undo a filter.** Ticking a kilde, a
  delkilde or a variabelgruppe drew it twice over the results — once as a removable chip, once as a
  trail step with an × of its own — so one ticked value had two remove controls a screen reader
  announced one after the other. The chip row is now the only place a value is removed, and it
  still holds one chip per chosen value across every facet. A trail step still narrows to its own
  level and clears every level below it, which is the thing a chip cannot do, and the trail is now
  a navigation landmark named "Valgt hierarki" rather than a filter list. (Fhi.Metadata-oj286)
- **A hierarchy value the facets do not name now has a chip of its own.** The facets are
  cross-filtered and can come back without a value the reader chose — and there are none at all
  before the first answer, or after one that failed, while a host may have mounted with a filter
  already set. Such a value was drawn in the trail and nowhere else, so with the trail's × gone the
  only control left would have been "Fjern alle filtre", which drops the datatype, the kodeverk and
  the dates with it. It is listed under its level's own word, exactly as the trail's step reads it,
  and it stands where its own facet stands in the row rather than after every unrelated one. Where
  a level has several such values they are one chip carrying a count — "Datasamling (+1)", the way
  a trail step collapses — because two chips reading the same word are two controls a screen reader
  cannot tell apart, and one press takes all of them off. (Fhi.Metadata-oj286)
- **Creating or renaming a variable list no longer risks swallowing an unrelated save or removal
  raised by another surface while it is in flight.** The two page reads a create used to skip, and
  the one a rename did, were suppressed by counting notifications rather than naming them, so a
  same-numbered but unrelated `VariableListState` change during that window could be silently
  dropped instead of redrawing the list on screen. Suppression is now by identity - which list a
  change names and whether it could touch that list's rows - carried as the argument of the
  `Changed` event. (Fhi.Metadata-wuxkn)
- **BREAKING for a host that subscribes to `VariableListState.Changed`: it is
  `Action<VariableListState.ListChange?>` now, not `Action`.** A handler written as `() => ...`
  no longer compiles; take the argument and ignore it (`_ => ...`) to keep the old behaviour. The
  identity travels on the event rather than in a property beside it because these methods await
  with `ConfigureAwait(false)`, so a shared property could belong to the next raise by the time a
  handler read it - and reading the wrong one skips a reload, which is the defect above.
  (Fhi.Metadata-wuxkn)
- **Double-clicking or shift-clicking "Vis filtre", "Legg til ny liste", "Gi nytt navn" or
  "Slett listen" leaves each where the first click left it** - the kildeutforsker's filter panel
  and the saved-list view's three controls took no click count at all, so the second click of a
  double-click folded the panel, or shut the form, the first click had just opened. On the delete
  control it also worked the other way: the same button cancels the confirmation, so a stray
  second click re-armed the delete the reader had just called off. A shift-click extending a
  selection across the words beside them did the same. Both gestures are now refused the way the
  rows and the variable panel's disclosures already refused them; deliberate repeated pressing
  still toggles both ways, and Enter or Space on any of the four is unaffected. These were the
  last four controls in the package that took no click count. (Fhi.Metadata-zel47)
- **The disclosure chevron on a Runa variable row is now clickable.** It used to render as a
  decorative sibling of the row's toggle button, so a reader aiming at the chevron hit nothing;
  it now sits inside the button, still `aria-hidden`, so the button's accessible name and
  `aria-expanded` are unchanged and there is exactly one control. (Fhi.Metadata-zqe14)

### Removed

- **The kildeutforsker's handover button no longer carries a class of its own.** It is
  `hd-button-square button-square--primary` and nothing else; the
  `munin-explorer-selection__explore` it also wore is gone. No stylesheet anywhere defined that
  name — not `Fhi.Helsedata.Stiler` 0.1.38, not 0.1.42, and not the live helsedata.no bundle — so
  it read as a styling seam and was not one. `munin-explorer-selection`, the ribbon around it,
  stays and is unchanged. The markup is all that changed: the wrap, the auto height and the height
  floor the removed name carried in the sample stylesheets moved to
  `.munin-explorer .munin-explorer-selection .hd-button-square`, so the sample hosts draw the button
  as they did at every width. The three declarations left behind are the `min-width` floor Stiler
  declined, the `max-width` that existed only to cap it, and a `justify-content` Stiler's own
  `.hd-button-square` already declares. A host on `Fhi.Helsedata.Stiler` alone still needs the
  three moved ones until `Fhi.Metadata-s4es0` puts them there. (Fhi.Metadata-kvgu7)

### Notes for hosts

- **`munin-explorer-kilder__count--zero` is new, and a host that styles it must dim rather than
  hide.** It joins `munin-explorer-kilder__count` on a kilder-table cell whose count is nought. A
  host that defines no rule for it loses nothing but the emphasis — the cell still reads `0`. A host
  that writes one owes the digit legible contrast: `display: none`, `visibility: hidden` and
  replacing the value with a dash all take away the reader's way of telling a register that counts
  nothing from a field nobody filled in, which the table renders as "Ikke oppgitt". Both sample
  stylesheets show the shape, a colour on the cell and nothing else. (Fhi.Metadata-8vbqf)
- The kilde detail hierarchy requires host styling for `munin-explorer-hierarchy`,
  `munin-explorer-hierarchy__nodes`, `munin-explorer-hierarchy__branch`,
  `munin-explorer-hierarchy__leaf`, `munin-explorer-hierarchy__count` and
  `munin-explorer-hierarchy__metadata`. Preserve native `details`/`summary` disclosure behavior,
  list semantics, visible keyboard focus and wrapping of long names. Count badges are text,
  not controls; leaf rows must not appear expandable.
- Helsedata styling is tracked separately in `Fhi.Metadata-wihod`; this change alone does not
  supply those rules in Stiler. The sample styles are provisional helsedata-based stand-ins.
  The component ships no CSS or JavaScript assets. Tab visits disclosure summaries, and
  Enter or Space toggles them locally in the browser.
- **The 3:1 the level-guide rule owes is now due on first paint.** The guides used to appear only
  once a reader pressed `Nivålinjer`, so a rule that drew them too faintly was hard to notice;
  they are drawn at every first render now. A guide line is a non-text control under WCAG 1.4.11
  and has to clear 3:1 against whatever the host's own page ground paints — the sample stylesheets
  use `--grey60` for 6.76:1, and `--grey30`, the token every other border in that panel wears,
  measures 1.16:1 and is invisible on a desktop. `Fhi.Helsedata.Stiler` carries the rule; a host
  with its own is the one that owes the ratio. (Fhi.Metadata-dfygj)
- **The column picker needs seven names a host without Stiler must now style**, and one it no
  longer uses. New: `form-control` and `form-control__label` (the row a checkbox and its label
  share), and `icon`, `icon-layout`, `icon--right`, `icon-keyboard-arrow-down` and
  `icon-keyboard-arrow-up` on the trigger. Gone: `hd-button-reset`, with any rule drawing a tick
  for `aria-pressed`. A host must also supply `position: relative` on `.munin-explorer__dropdown`,
  which the component used to emit inline and now leaves to the stylesheet - without it the open
  list anchors to whatever is positioned further up the page. Both chevrons are always in the DOM;
  hide the one that contradicts `[open]`, since the component ships no script and cannot swap a
  class. Both sample stylesheets show the whole set. (Fhi.Metadata-f6az7)
- **A host on Stiler needs at least `Fhi.Helsedata.Stiler` 0.1.53.** The picker's positioning, its
  right-aligned trigger, the chevron that follows `[open]` and the cue on the column that refuses
  all ship in that release. Measured against an earlier Stiler: the dropdown computes
  `position: static` so the open panel anchors to whatever is positioned further up the page, the
  trigger falls back to the left of the results row, and both chevrons draw at once. helsedata.no
  pinned 0.1.42 on 2026-09-08, so this component must not reach them in a release that does not
  lift Stiler with it. (Fhi.Metadata-f6az7)
- **Delete any rule you wrote for `munin-explorer-selection__explore`; it is no longer emitted.**
  A previous note here asked hosts for a `min-width` on it, so that the handover button held still
  across its three labels. `Fhi.Helsedata.Stiler` measured that floor and declined to ship one: the
  widest label is English's, and a floor wide enough for it leaves around 104px of dead button in
  the common Norwegian state. Nothing intends to hold the width now, so the button sizes to its own
  label and the *Nullstill utvalg* button and the count slide when it changes — accepted rather
  than overlooked. The button itself is unchanged; `hd-button-square button-square--primary` is
  what always drew it. (Fhi.Metadata-kvgu7)
- **The floor went, but the handover still has to be allowed to wrap.** `hd-button-square` is
  `white-space: nowrap` at a fixed `2.75rem`, so the widest label is one unbreakable line: measured
  on the sample host at a 320px viewport it draws 306px wide in a 226px row and scrolls the page
  sideways, which is WCAG 1.4.10 Reflow. Give the ribbon's button `white-space: normal`,
  `height: auto` and a `min-height: 2.75rem` floor, on a selector that outranks the bare
  `hd-button-square` — both sample stylesheets scope it
  `.munin-explorer .munin-explorer-selection .hd-button-square`, three classes, so source order
  cannot take it back. Those three are the whole of what a host needs. The removed rule carried
  six, and the other three are gone deliberately: `min-width: min(21rem, 100%)` is the floor Stiler
  declined; `justify-content: center` is already declared by Stiler's own `.hd-button-square`, so
  repeating it drew nothing; and `max-width: 100%` was there to cap that floor, and with no floor
  left the ribbon's flex row is what keeps the button inside the line — the 320px measurement above
  is with the three and without it. `Fhi.Metadata-s4es0` asks `Fhi.Helsedata.Stiler` for the same
  three, and until that ships a host with no rule of its own overflows. (Fhi.Metadata-kvgu7)
- **`munin-explorer-selection`, the ribbon around it, stays and still needs `display: flex`.** A
  host that draws nothing for it gets the handover, the reset and the count stacked. Stiler has the
  rule on `main` and it is **not** in 0.1.42, the version helsedata.no pins today, so a host on that
  pin still supplies it themselves until the pin moves (`Fhi.Metadata-kpmt3`). Both sample
  stylesheets show what it wants. (Fhi.Metadata-kvgu7)
- **`munin-explorer-kilder-scroll--cols-N` needs no rule, and that is the point of it.** It is a
  modifier on a box whose base class `munin-explorer-kilder-scroll` you are already styling, so a
  stylesheet that ignores it leaves the box exactly as it renders today — nothing degrades to a raw
  browser default, unlike every other name this package invents. N runs from 4 to 15: four columns
  are always drawn, a fifth appears where the host wired the handover to the variable explorer, and
  the reader turns the other ten on and off from the column picker.
  What a rule buys is the failure it exists for. A host that gives the box `overflow-x: auto` at
  every width needs nothing here. A host that makes it `overflow-x: visible` above a breakpoint —
  which `Fhi.Helsedata.Stiler` does above 780px, so that the page is the sticky ancestor the table's
  header pins to — has a table that runs past its box once the reader turns the wide columns on:
  measured at 1440×900, fifteen columns put the table 663.6px outside the box and the whole host
  page into a horizontal scroll, which is a WCAG 1.4.10 failure on the host's own site. Select on
  the counts that overflow your layout. **Do not answer it by restoring a scroll container above the
  breakpoint** — that takes the sticky ancestor away and the table's header stops pinning, which is
  a worse regression than the spill. (Fhi.Metadata-l9l2n.103)
- `AddMuninExplorer` now calls `AddLogging`. It is idempotent and `TryAdd`-based inside, so a host
  that already configured logging keeps every provider, filter and minimum level it set, and a host
  that configured none gets the default factory rather than a component that cannot report a fault.
  The categories to filter on are `Fhi.Munin.Explorer.Blazor.*`, `Fhi.Munin.Explorer.Client.*` and
  `Fhi.Munin.Explorer.State.*`, and the highest level written is `Error`. `Error` means a fault:
  a throttled request and a request the API declined to accept the caller for are `Warning`, on
  the saved-list paths as much as anywhere else, so a host whose token provider is misconfigured
  fills the `Warning` channel rather than the `Error` one. A host that mounts a
  component without calling `AddMuninExplorer` at all still renders: the logger is resolved
  optionally, and the components no-op when there is none, and a provider that throws is caught at
  that same seam rather than reaching the page. Message templates no longer repeat the component
  name the category carries, so filter and group on the category.
- **One new class name for the kildeutforsker's facet summaries, `munin-explorer-filters__chosen`,
  and one rule a host owes the summary line itself.** The name is the "2 valgt" beside a facet
  heading, and it is a handle: the words are markup, so a host that defines nothing still gets the
  count on screen and in the summary's accessible name, and what a rule buys is the dimming, the
  tabular figures and `flex: none` so the number is not broken across lines. The rule that has to
  be there is the other one — a `<summary>` laid out as a row, because the heading inside it is a
  block box and takes the whole first line otherwise, putting the count under the heading and the
  disclosure marker under that. Both sample stylesheets show the pair, scoped to the kildeutforsker's
  facets; the rule shipping in `Fhi.Helsedata.Stiler` is unscoped and shared with the
  variabelutforsker's panel, and it redraws the marker on the trailing edge, since a summary laid
  out as a row is no longer a list-item and the browser stops drawing one (Fhi.Metadata-l9l2n.58).
  Not to be confused with `munin-explorer-filters__count`, the hit count beside a single value,
  whose rule pushes it to the column edge with `margin-left: auto` — on the summary row that edge
  is the marker's, which is why the two names exist. (Fhi.Metadata-l9l2n.53)
- **The kilder table's rows are click targets now, and no host stylesheet says so.** No class name
  changed and no rule is required, but a row that opens on a press wants `cursor: pointer` — the
  package ships no CSS and cannot supply it, so on `Fhi.Helsedata.Stiler` the pointer stays an
  arrow until the rule lands there. Every behaviour the row press reaches is still on the chevron
  button beside it, which is in the tab order and unchanged, so the missing cursor costs
  discoverability and nothing else. (Fhi.Metadata-l9l2n.55)
- **The kildeutforsker's fold row wears `munin-explorer-filters__toolbar`, the name the
  variabelutforsker's own panel already uses**, and it is emitted as a direct child of
  `munin-explorer-filters`, which is what the rule pinning it to the top of a scrolling facet
  column selects on. A host that defines nothing gets the two buttons back in inline flow above the
  facets, which is a usable row and loses no information; what the rule buys is the row staying on
  screen once the reader has opened several facets and scrolled, plus the background that keeps the
  facets from showing through as they pass under it. `Fhi.Helsedata.Stiler` carries both, scoped to
  the width at which the panel is a scrolling sidebar — below that there is no scroll area to pin
  to and the row simply sits where it is drawn. Both sample stylesheets carry the flex row.
  (Fhi.Metadata-l9l2n.60)
- **One new class name for the variabelutforsker's Kilde facet, `munin-explorer-filters__search`,
  styled from `Fhi.Helsedata.Stiler` PR 39236.** It is the box that narrows that facet's own values.
  A handle: a host that defines nothing for it gets a browser-default search field, which is
  visible, operable and named by a `<label>` of its own, so what a rule buys is the box — full
  width in a sidebar column, 34px tall and at the panel's own type size rather than the page's.
  A host writing its own rule owes it one thing that is easy to miss: **lead the selector with the
  element**, `input.munin-explorer-filters__search`. Stiler's global `input[type="search"]` list is
  (0,1,1) and a bare class is (0,1,0), so a class-only rule loses its `font-size` to that list and
  the field draws at 18px where 14px was measured — both figures measured on the real stylesheet
  rather than derived. The rule merged to Stiler's `main` as `0bd0b34` on 2026-09-09 with no version
  bump, so the floor is the first release that follows 0.1.42 — a host on 0.1.42 or older is in the
  undressed case above rather than a broken one. Both sample stylesheets carry the stand-in, declaration for
  declaration. The kildetype groups that appear inside the same facet add no name at all: they are
  `<details>`/`<summary>` like the facets around them, so their marker, open state and focus ring
  come from the `.munin-explorer-filters summary` rules a host already has, and their counts wear
  `munin-explorer-filters__chosen`, which the kildeutforsker's summaries introduced.
  (Fhi.Metadata-l9l2n.67)
- **The variabelutforsker now emits four class names that only the kildeutforsker emitted before,
  and their rules are not in `Fhi.Helsedata.Stiler` 0.1.42.** `munin-explorer-filters__active`,
  `munin-explorer-filters__chip` and `munin-explorer-filters__chip-remove` are the active-filter row,
  the capsule around one chosen value and the close control inside it; `munin-explorer-results__toolbar`
  is the row the result count shares with the Kolonner picker. No name here is new to the package and
  no new rule is needed for this change — a host already styling the kildeutforsker's chip row and
  result row is done. What is new is that the variabelutforsker draws them too, so a host on 0.1.42,
  the version pinned here, now has two surfaces in the undressed case rather than one: the floor is
  the Stiler release of 2026-09-11, the first that follows 0.1.42, and it carries all four. Undressed
  is undressed rather than broken — the chips fall back to inline flow with every word and control
  intact, and the count and picker to two blocks in ordinary flow — except for the close control's
  24×24 box, which is a WCAG 2.5.5 target and is the one thing lost rather than merely undrawn. Both
  sample stylesheets show the shape. (Fhi.Metadata-l9l2n.68)
- **Three new class names, and a toolbar rule that must not reach them.** `munin-explorer-switch` is
  the Nivålinjer control, `munin-explorer-switch__track` and `munin-explorer-switch__thumb` are the
  two empty spans inside it that draw the on/off state. `Fhi.Helsedata.Stiler` carries all three
  from the release that follows PR 39257 — the rules are on `main` there but no version has shipped
  them yet, so pinning a published Stiler today gets a browser-default `<button>` with its label.
  That release is tracked as `Fhi.Metadata-aonvl`; until it lands, the only run that measures the
  control that ships is `STILER_FROM_SOURCE=1 ./scripts/check-hostile-host.sh`, which builds Stiler
  `main` from a checkout instead of restoring the pin.
  That is operable and named, since the state is announced from `aria-checked` rather than drawn,
  but it has no visible on/off mark, so a sighted reader loses the state a screen reader still
  hears. Both sample stylesheets show the shape Stiler draws: a 30×18 track and a 12×12 thumb
  travelling 12px, keyed on `[aria-checked="true"]` and never on a modifier class. Read the off
  state's colours before rewriting them — the track's `--grey20` fill is the same colour as the
  control's own hover surface and vanishes on it, so what carries WCAG 1.4.11's 3:1 off is the
  `--grey60` border, not the fill. **The trap is the toolbar rule.**
  `munin-explorer-filters__toolbar > .hd-button-square` sets `min-width: 0` and `overflow-wrap:
  anywhere`, and the switch deliberately no longer wears `hd-button-square` so that rule cannot
  reach it. A host whose own toolbar rule selects the row's children instead of that class will
  reach it, and the cost is measured rather than guessed — on the Stiler side of this pair, against
  Stiler source rather than against this repository's fixture, the same markup under those two
  declarations came out 4.72×304.34px, one character wide with every letter on a line of its own,
  where 114.98×32 is what the control should draw. (Fhi.Metadata-l9l2n.87)
- **`munin-explorer-kilder__expand-icon` is new, and a host on `Fhi.Helsedata.Stiler` needs no rule
  for it.** The chevron inside the kilder table's expand button wears Stiler's own `icon`,
  `icon--nomargin` and `icon-keyboard-arrow-right` / `icon-keyboard-arrow-down` beside it, and those
  are what draw it: `.icon` is `1.5rem` square, which is the whole of why the button now measures at
  least 24 x 24. The new name is a handle for a host or a test to find it by. A host **without**
  Stiler owes it that box itself — under 24 x 24 the control fails WCAG 2.5.8 — and both sample
  stylesheets show the shape it needs. (Fhi.Metadata-mpx2p)
- **Three new class names for the active-filter row, styled from `Fhi.Helsedata.Stiler` PR 39206.**
  `munin-explorer-filters__active` is the row, `munin-explorer-filters__chip` the capsule around one
  ticked value and `munin-explorer-filters__chip-remove` the close control inside it. Handles, all
  three: a host that defines none of them still gets every word and every control, in inline flow
  rather than in a row of capsules, and the close control's accessible name is written down in the
  markup rather than drawn. A host writing its own rules owes the close control a 24×24 box — that
  is a WCAG 2.5.5 target and the only thing here that is lost rather than merely undressed — and
  owes the capsule's edge 3:1 against whatever the page ground is. Both sample stylesheets show the
  shape. Nothing else in the row is new: the heading wears Stiler's `caption margin--none`, where
  `margin--none` is load-bearing because a bare `caption` paragraph's block margins break the row's
  alignment, and the clear-all is new but wears no new name: `hd-button-square
  button-square--ghost` is Stiler's, and the facet panel's own fold toggle already wears it.
  (Fhi.Metadata-ofoyw)
- The `munin-explorer-breadcrumb__clear` class name is gone: the control it dressed, the × that
  emptied the hierarchy, is no longer rendered. A host carrying a rule for it can drop it.
  `munin-explorer-breadcrumb` keeps its name and now carries `role="navigation"` and the trail's
  own accessible name — but it holds one child where it held two, so a rule that reached the ×
  through a descendant selector, or laid the wrapper out expecting a second box beside the list,
  wants checking against the markup rather than assuming nothing moved. (Fhi.Metadata-oj286)
- **The node icons need two names a host without Stiler must style**:
  `munin-explorer-hierarchy__icons`, the slot in front of a row's name, and
  `munin-explorer-hierarchy__icon`, each glyph in it. Both sample stylesheets show the pair. An
  undefined one is not an unstyled one: every glyph carries `width="1em"`, `height="1em"` and
  `stroke="currentColor"` of its own, so it draws at text size in the text colour rather than at an
  SVG's 300x150 default. What is lost is the colour that tells two datakategorier apart at a
  glance, and the rule to write it with is `data-node-icon` on the glyph - `PHDR`, `EINS`, `other`
  and the rest of the EHDS codes, plus `kilde` on the grouping folder. helsedata's own appearance
  is not in this release: it ships from `Fhi.Helsedata.Stiler` under `Fhi.Metadata-wihod`.
  (Fhi.Metadata-s3l7l)
- **One new class name for the kildeutforsker's result row, `munin-explorer-results__toolbar`,
  styled from `Fhi.Helsedata.Stiler` PR 39220.** It is the row holding the result count, the Sorter
  control and the Kolonner picker. A handle, and the plainest kind: a host that defines nothing for
  it gets the three back as three blocks in ordinary flow, which is exactly what shipped before the
  name existed, so what a rule buys is two rows of vertical space and no reader loses a word or a
  control. The rules landed in Stiler after 0.1.42 was cut, so the floor is the first release that
  follows it — a host on 0.1.42 or older is in the undressed case above rather than a broken one.
  A host writing its own owes the row three things the sample stylesheets show: the count is the
  row's first child and has to take the slack, or it stops holding the left edge; the row has to
  end its wrapped lines at the trailing edge, or the picker's right-aligned menu opens off the
  screen; and the open menu needs a width of its own, because a row makes
  `munin-explorer-header__actions` content-wide and the menu otherwise shrinks to the width of the
  Kolonner button with the column names spilling out of it. The name says `results` and the element
  sits above `munin-explorer-results` rather than inside it, which is deliberate: the results
  container is drawn only when there are rows, and the count in this row is the component's one
  polite live region, which has to be in the DOM before its text arrives. (Fhi.Metadata-tciss)

## 0.1.0-alpha.10 — 2026-09-07

### Added

- **The kilde table has a column picker**, the one Munin's own kildeutforsker has. Ten optional
  columns in its order — Kildetype, Datasamlinger, Variabler, Delkilder, Dataansvarlig,
  Databehandler, Grad av personidentifikasjon, Gyldighet, Importert and Sist endret — of which the
  first three start on, which is the table hosts already have. Navn, Status and Opprettet are
  outside the picker's reach, so no choice can empty a row. The choice is not persisted and is not
  in the URL: it lasts as long as the page, exactly as it does in the kildeutforsker.
- **Seven of those columns are new data on this table**, and two of them are dates the payload
  spells confusingly: `Importert` is `opprettet`, Munin's own row timestamp, while the
  always-visible `Opprettet` column stays the founding year the catalogue states in
  `additionalProperties.Opprettet`; `Sist endret` is the catalogue's own
  `additionalProperties.SistOppdatert`, not `sistOppdatert`, which is Munin's. A source date the
  catalogue did not write as `yyyyMMdd` is shown exactly as it stands rather than blanked or
  guessed at. (Fhi.Metadata-ay3zz)
- **The Variabelliste tab has a filter panel of its own: the kilder the reader's list draws from,
  each with a count, and a tick that narrows the rows.** `VariableSearch` gained
  `VariableListFilters`, a second `RenderFragment` beside `VariableList`, drawn in the filter column
  while that tab is open and in place of the search's own facets. `VariableExplorer` passes the new
  `VariableListFilters` component there; a host composing its own page must pass it too, or that
  tab keeps the empty filter column it has today. The tally is built over the whole list, never the
  page on screen, and the narrowing is the API's — so the pager and the row count stay its answer.
- **`IMuninExplorerClient.GetMyListVariablesAsync` takes an optional `kildeIds`.** Several of them
  union rather than intersect, and null or empty is every kilde. A host implementing the interface
  rather than consuming `MuninExplorerClient` must widen its own signature. **Requires Munin API
  5658 or later**: an older API does not know the parameter, ignores it, and answers with the whole
  list.
- **`VariableList` gained `VariableCount`, and the saved-list picker now says what every list
  holds** rather than only the one on screen. Munin's `GET api/explorer/my/lists` started sending
  `variableCount` and nothing read it; the picker's options read `Mine hjertevariabler (247
  variabler)` now, and a list with nothing in it reads `0 variabler`. The line under the picker
  takes its count from the same field, so the two cannot disagree about the same list.
  (Fhi.Metadata-uiqfs)

### Changed

- **BREAKING for hosts: `KildeExplorer` is the whole kildeutforsker.** The kilde list, the drill-in
  and the open kilde in the address bar, from one mount — `Language` is all it takes. The list on
  its own, with no `?kilde=` handling, is now `KildeSearch`, and the separate
  `KildeExplorerWithUrlState` is gone — `KildeExplorer` is now the component that name used to
  mean, so mounting `KildeExplorer` now gets you the wrapper's behaviour rather than the old bare
  list. Both mounts still declare `ShowAccessAndPrices`, and the merged one still takes
  `VariableExplorerPath`. This is the same fold the variable side had in the previous release, so a
  host that upgrades once renames both.
- **`VariableSearch` is sealed.** It now unsubscribes from `VariableListState.Changed` in
  `Dispose`, and an unsealed disposable owes CA1063 a virtual pattern a Blazor component has no use
  for — the same reason `VariableListView` is sealed. Mounting it is unaffected; only a host that
  derived from it has to stop. (Fhi.Metadata-ehghv)
- **BREAKING for a host that derives from a component: every published component is now `sealed`.**
  Counting from the released `0.1.0-alpha.8`, three change in this release — `VariableSearch` (see
  the bullet above), `VariableExplorer` and `KildeExplorer` — and five were already
  sealed. The last two were open by silence rather than by decision: neither file said why, and
  neither had ever carried the keyword. Nothing in this package or its samples derives from either,
  and helsedata mounts by type name out of a CMS field rather than by inheritance, so the door was
  open for no consumer we know of. Unsealing later is invisible to a consumer; sealing later is a
  binary break, which is why alpha is the moment to decide it. If you do need to derive from one,
  say so and it can be reopened with a reason attached. (Fhi.Metadata-l9l2n.43)
- **A released version now has a curated changelog entry, and the feed shows it.**
  `PackageReleaseNotes` carries that version's own `CHANGELOG.md` section rather than a link to a
  GitHub release page of auto-generated commit titles, and the release for the tag carries the same
  text. `CHANGELOG.md` also gained the eight `0.1.0-alpha.*` sections it never had: a host bumping
  `0.1.0-alpha.7` to `0.1.0-alpha.8` can now read that `VariableExplorerWithUrlState` was removed
  and the bare component renamed to `VariableSearch` where it would look for it. (Fhi.Metadata-l9l2n.44)
- **BREAKING for hosts that read Munin's own timestamps: they are `DateTimeOffset?` now.**
  `opprettet` and `sistOppdatert` on `KildeDetail`, `DatasamlingDetail` and `KildeSummary`, and
  `createdAt`, `updatedAt` and `addedAt` on the variable-list types. The wire format is unchanged —
  every `[JsonPropertyName]` is untouched — and so is what the component renders. A host that does
  not recompile at all gets a `MissingMethodException` on the getter, since the return type is part
  of the signature.
- **The half that does not compile is the easy half.** A host reading one into a non-nullable local
  is told by the compiler. Two things compile unchanged and behave differently. `k.LastUpdated <
  cutoff` — a "not touched since" report — was **true** for a kilde whose payload omitted the field,
  because the property held `0001-01-01`, and is **false** now; those rows leave such a report
  silently. `$"{kilde.LastUpdated}"` renders an empty string where it rendered a date. And
  `Min()` over a set that includes one now answers the earliest real date rather than `0001-01-01`.
  `OrderBy` is *not* affected — `null` sorts exactly where `MinValue` did. Nor is `== default`, which
  stays true for the absent case; it is `== DateTimeOffset.MinValue` that stops matching.
  (Fhi.Metadata-se0by)
- **The saved-list view reads as part of helsedata rather than as default markup.** Its variables
  are a real `<table>` with one row of `<th scope="col">` instead of a header line over a stack of
  `<div>`s that repeated all seven field names in every row; creating and renaming a list are
  behind their own buttons instead of two standing forms; the list on screen says how many
  variables it holds and when it last changed; and the buttons wear helsedata's own
  `button-square--primary` and `button-square--ghost-blue` where every one of them used to be
  `button-square--ghost`. The export block is unchanged — it is per-list, and helsedata has no
  equivalent to copy.
- **A rename or a removal now moves the list's "sist endret" as well as its name.**
  `VariableListState` patches its own copy of a list rather than refetching it, which is what makes
  a rename look instant — so the timestamp is patched with it. Without that the new line would keep
  naming the day the page was loaded while the name above it changed under the reader.

### Fixed

- **Creating a variable list no longer leaves the previous list's rows on screen under the new
  list's name.** Making a list starts page reads for the list being left, and a late answer from
  one of them overwrote the new list's own — so the reader saw their old variables under the new
  name and had every reason to think creating had copied them across. (Fhi.Metadata-2ifw8)
- **A kilde or datasamling whose payload carries no `sistOppdatert` no longer shows "1. januar
  0001".** The DTOs declare Munin's own timestamp non-nullable, so an absent one arrived as
  `default` and the source-information block rendered the year 1 under "Sist oppdatert i Munin" — a
  date the catalogue never sent, stated as fact. The row is now dropped instead, which is what that
  block already does for every other field the payload leaves out. The kilde table's `Importert`
  column reads through the same guard now and behaves exactly as it did. (Fhi.Metadata-6r6rf)
- **The kilder table scrolls in a box of its own instead of scrolling the host's page.** Its eight
  columns want 779px at their narrowest, which is more than helsedata's content box below about
  827px, and the overflow used to land on the document — a WCAG 1.4.10 failure on the whole site
  rather than a table that looks wrong. The table now sits in a `region` with `overflow-x`,
  `tabindex="0"` and the table's own name, so a keyboard can reach it; the column picker stays
  outside the box and on screen. (Fhi.Metadata-b3brc)
- **Removing a variable in `VariableListView` now reads as removed in the search results too.** The
  save button beside the same variable went on saying "Fjern fra liste" with `aria-pressed="true"`,
  offering to take out something already gone — reachable on any page that mounts both surfaces.
  The membership set is now maintained by `AddVariablesAsync` and `RemoveVariablesAsync` rather than
  only by the save press, and `VariableSearch` redraws on `VariableListState.Changed`. No host
  change needed, and no extra request: neither surface refetches. (Fhi.Metadata-ehghv)
- **A payload carrying `"navn": null` — or a null on any other string the contracts declare
  non-nullable — no longer takes the host's Blazor circuit down.** The null used to pass
  deserialisation and throw at render time instead, past the try/catch around the fetch, so the
  component's own error message never appeared. Those properties now read an explicit null as
  `""`, exactly as they already read an absent key; properties declared `string?` are untouched.
  (Fhi.Metadata-o355u)
- **A kilde or datasamling whose payload carries `"sistOppdatert": null` no longer fails to load
  at all.** The property was non-nullable, so an explicit null threw inside deserialisation and the
  component reported "Kunne ikke hente datakilden" — the reader lost the whole panel over one
  field. Munin declares these columns nullable and already sends explicit nulls for dates
  elsewhere, so this was reachable from the live API. An absent key and an explicit null now mean
  the same thing: the field is simply not shown. (Fhi.Metadata-se0by)
- **A catalogue entry with no name no longer leaves the control that carries it unnamed.** A kilde,
  delkilde, datasamling or variable whose name is empty — reachable two ways: the catalogue can hold
  an empty `preferredTerm`, and since `Fhi.Metadata-o355u` an explicit `navn: null` from the API
  reads as the empty string rather than taking the host's circuit down — drew a row button with no
  text, a checkbox announcing "Velg " with nothing after it, a facet whose checkbox announced only
  its count, a datasamling row whose header cell was empty, and headings with nothing in them. Every
  such control and heading now falls back to the entry's **code** — or its short name, where the
  contract carries no code — which is the identifier already drawn beside it and the one the API
  itself falls back to. Where that stands in for the name, the line that usually repeats it
  underneath is not drawn, and the `lang="no"` marker comes off: a code is not Norwegian prose for a
  screen reader to sound out. (Fhi.Metadata-w13lk)
- **The variable row's disclosure button says which variable it opens.** It answered "Vis hele
  variabelen" for every unnamed variable — a name that satisfies a checker and leaves a reader no
  way to tell one row from the next. It now answers with the variable's code. (Fhi.Metadata-w13lk)
- **An entry with neither a name nor a code reads "Ikke oppgitt".** That is the last arm and it is
  reachable, since a code can be empty exactly as a name can. Two such entries still announce
  identically — a fallback cannot invent an identifier the catalogue does not hold — so the data
  problem underneath is filed separately as `Fhi.Metadata-xku9b`. (Fhi.Metadata-w13lk)

### Notes for hosts

- **The sample stylesheets are a stand-in for `Fhi.Helsedata.Stiler`, and 175 of their declarations
  do not match it.** A host that copies `samples/*/host.css` as a starting point gets those
  differences with it. They are now listed, one per line, in `test/sample-css-known-divergences.txt`
  — the missing row-collapse block below 1280px, the eleven `munin-explorer-meta` table rules, the
  absent base `.munin-explorer` rule, and the rest — so the list can be read before the stylesheet
  is trusted. A new guard compares the two files' declarations against the published package on
  every pull request, so the count can only go down from here. Hosts that link Stiler itself are
  unaffected; this is about what the samples claim to reproduce. (`Fhi.Metadata-3dwar`)
- **The sample stylesheets now push the facet count to the label's right edge**, with
  `margin-left: auto` on `munin-explorer-filters__count`. Without it a count sits wherever its own
  label's text ended — measured in the samples at 81 distinct right edges across the variable
  explorer's 107 facet values, and 7 across the kildeutforsker's 8 — so the `font-variant-numeric:
  tabular-nums` that rule already carried had no column to line its digits up in.
  `Fhi.Helsedata.Stiler` carries the same declaration (ADO PR 39101), so a host on that stylesheet
  already had the column and the samples were the ones out of step; a host copying its rules from
  the samples should take it. It is a physical `margin-left` rather than `margin-inline-start`,
  matching the declaration it stands in for — a host serving `dir="rtl"` wants the logical form.
  (Fhi.Metadata-7bchj)
- **The explorer's root element carries its own width, and the drill-in spans the whole grid.**
  `Fhi.Helsedata.Stiler` gives `.munin-explorer` a base rule of its own — `max-width: 1488px;
  width: 100%; padding: 0 24px; margin: 0 auto` — because the component is a `<section>` a host
  drops straight into the page and nothing above it bounds the width; and it lifts
  `.munin-explorer-drilldown` to `grid-column: 1 / -1`, excluding it from the catch-all that places
  everything else in the results column. A host laying the component out in two columns needs both.
  Without the first the component runs to the window edge; without the second the drill-in renders
  in the results column with the filter track standing empty — measured in the sample at 408px of
  dead gutter, and 1041px of content in a 1024px viewport. (Fhi.Metadata-7e7de)
- **Two renames, one line each.** A host mounting `KildeExplorerWithUrlState` mounts
  `KildeExplorer` instead — same three parameters, same behaviour. A host that deliberately wanted
  the bare list under the old `KildeExplorer` name now names `KildeSearch`. A CMS field holding
  `Fhi.Munin.Explorer.Blazor.KildeExplorerWithUrlState` resolves to nothing after this release and
  renders no component: it is a string, so nothing fails to compile first.
  <br><br>
  **`KildeExplorer` must be mounted at an interactive render mode** — `render-mode="Server"`, never
  `ServerPrerendered`; `@rendermode` with `prerender: false` in a modern host. It owns `?kilde=`
  and throws on initialisation rather than drawing a page whose URL never follows the view.
  `KildeSearch` has no such requirement, and no query handling either: a link to it always lands on
  the list.
  <br><br>
  **`VariableExplorerPath` is unchanged and still optional.** It is the one thing only the host
  knows, so a CMS that mounts by type name and cannot pass it gets no selection column and no
  handover button — the same page that mount renders today. Set it from a host you write yourself
  to offer the handover; it is not defaulted to a guess, because a button leading to a page you may
  not have is worse than no button.
  <br><br>
  **No new or removed class names**, and no `Fhi.Helsedata.Stiler` rule changes with this: the
  markup is the same component under a different name.
- **The two static blocks over an open kilde are now off by default.** "Kriterier for tilgang til
  data" and "Priser" draw only when the host sets `ShowAccessAndPrices="true"`, which is declared
  on both of Kelda's mounts — `KildeSearch` and `KildeExplorer`. Both blocks send
  the reader to helsedata.no, so a host embedded on a site that already publishes its own access
  and pricing pages was shipping a second copy of content it does not own, inside a component it
  cannot edit. **A host mounting the kildeutforsker on its own site has to add the parameter to
  keep the blocks it had.** Nothing else on the kilde page moves with it: the variable count, the
  metadata, the datasamlinger and the sidebar are drawn either way. (Fhi.Metadata-ay3zz)
- **The column picker's names now appear on the kilde table too**, unchanged:
  `munin-explorer-header`, `munin-explorer-header__actions`,
  `munin-explorer-header__actions-button` and `munin-explorer__dropdown`, plus Stiler's own
  `dropdown-choicepicker`. No name is new, so a host that styles the variable explorer's picker
  has nothing to add — **unless its rules are scoped to that explorer's own container**, in which
  case the same control renders undressed above the kilde table. Worth reading the selector rather
  than the name: a rule reaching this picker has to match a `<details>` inside
  `munin-explorer-header`, wherever that header sits.
- **A toggle in that picker should not have its tick read out.** The sample stylesheets draw the
  on/off state as `☑`/`☐` in `::before`, and a browser folds generated content into the accessible
  name — so the control announced as "☑ Kildetype" and said in words what `aria-pressed` already
  says. The samples now write `content: "\2611" / ""`, whose empty alternative text keeps the glyph
  out of the name; a host drawing its own tick owes the same, or it owes no glyph at all. That
  syntax has a floor — Chrome 77, Safari 17.4, Firefox 133 — and below it the whole declaration is
  invalid, so the tick disappears rather than degrading. A host supporting older browsers should
  mark the glyph up as `aria-hidden` content instead. (Fhi.Metadata-ay3zz)
- **`munin-explorer-kilder-scroll` is new and needs `overflow-x: auto`.** It wraps the kilder table
  alone. Undrawn, the table's overflow goes to the document and the host's whole page scrolls
  sideways; the markup already carries `role`, `tabindex` and the name. Both sample stylesheets
  carry it, and it ships in `Fhi.Helsedata.Stiler` from 0.1.41 onward. (Fhi.Metadata-b3brc)
- **The same box is focusable, so it also needs a visible focus indicator.** `tabindex="0"` is
  unconditional, so a keyboard lands on the box whether or not it has anything to scroll, and a
  focus stop the reader cannot see is WCAG 2.4.7 — one failure traded for another. A host whose CSS
  reset strips outlines must put one back. Both sample stylesheets and `Fhi.Helsedata.Stiler` use
  `outline: 2px solid <focus colour>; outline-offset: 4px` on `:focus-visible`. (Fhi.Metadata-b3brc)
- **`munin-explorer-kilder__expand` needs a rule that out-specifies the host's own cell rule, and
  less side padding than the rows carry.** In `Fhi.Helsedata.Stiler` it is written scoped —
  `.munin-explorer-kilder .munin-explorer-kilder__expand` — because a stylesheet that pads `th, td`
  under the table's class is (0,1,1) and a bare class name loses to it. A host that writes the rule
  bare gets a left-aligned column that keeps the rows' 12px of side air, and at that padding the
  column is content-driven, so `width: 32px` does nothing either. Measured in the sample: 46.95px
  wide with the glyph flush left, against 32px and centred once the rule is scoped and the padding
  is 4px. (Fhi.Metadata-cuo0e)
- **A host stylesheet with a bare element rule un-hides everything this package marks `[hidden]`.**
  The browser's own `[hidden] { display: none }` loses to any author rule of equal specificity, so a
  reset carrying `div { display: block }` leaves the folded filter panel on screen while the toggle
  still says "Vis filtre". Put `[hidden] { display: none }` back for the elements you dress; both
  sample stylesheets now do. (`Fhi.Metadata-fih3y`)
- **One lane in the sidebar is the host stylesheet's job, and `Fhi.Helsedata.Stiler` has done it
  since 0.1.39.** The fact lists in the kilde, datasamling and whole-variable asides wear
  `munin-explorer-meta__grid`, the same class the package draws every fact list with. That grid is
  two `1fr` tracks and `1fr` floors at min-content, so in a 320px sidebar the Lovverk prose sizes the
  tracks past the panel edge, and the reader sees the whole page scroll sideways at any width above
  1280px. Stiler closes it with a rule scoped to the three asides —
  `.munin-explorer-kilde__aside .munin-explorer-meta__grid` and its `__whole__`/`__datasamling__`
  siblings, `grid-template-columns: minmax(0, 1fr)` — and the sample stylesheets here now carry a
  copy of that rule, selector for selector, so the stand-in works the way the real one does. A host
  on an older Stiler, or on a stylesheet of its own, needs the equivalent or it gets the scrollbar.
- **Reach it by scoping to the aside, not by the `munin-explorer-meta__grid-1` modifier.** The
  package still writes that name in one place — the variable detail panel, where helsedata's own
  layout expects it — and in none of the asides, so a host should not reach for it there either. In
  Stiler the same name also carries `grid-row: 1/3` for the variable page's layout, so on an aside it
  brings a placement rule along with the single track. The scoped rule out-specifies it there
  regardless. (Fhi.Metadata-hi0po)
- **`munin-explorer-meta__grid-1` carries a placement rule as well as a track count, and
  `munin-explorer-meta__grid-2` only gets its one lane if the override is ordered after the base
  rule.** In `Fhi.Helsedata.Stiler` the two modifiers share `grid-template-columns: auto` and
  `-1` additionally has `grid-row: 1/3`, both declared after `.munin-explorer-meta__grid`'s own
  `1fr 1fr`. A host writing its own stylesheet needs the same two things: the placement rule, which
  is inert wherever the element's parent is not a grid container and stops being inert the moment
  it is, and the source order, because the modifier and the base rule are equally specific and the
  later one wins. The sample stylesheets had neither, so `-1` lost its row span and `-2` silently
  kept two lanes. (Fhi.Metadata-l9txl)
- **The saved-list filter panel has to be a direct child of the explorer's own section, which is why
  it is a fragment `VariableSearch` draws and not something `VariableListView` draws for itself.**
  `Fhi.Helsedata.Stiler` places the filter column with `.munin-explorer > .munin-explorer-filters`,
  a child combinator, and the saved list renders two levels down inside the tab panel. A host
  mounting `VariableListFilters` on a page of its own must put it where the search's own panel would
  go, or the panel lands in the result column and the 384px filter track stands empty — which is
  what that tab looked like before this: 408px of gutter on the left and none on the right.
- **No new class name.** The panel wears `munin-explorer-filters`, `form-element__label`,
  `munin-explorer-filters__count` and `hd-button-square button-square--ghost`, all of which Stiler
  and both sample stylesheets already draw. Nothing to add.
- **A null `int`, `bool`, `Guid` or enum still fails the whole call, on purpose.** Unlike a name or
  a list, those have no value that means "nothing" — a kilde reported as having `0` datasamlinger
  when it has fourteen is worse than the "could not load" the component draws instead. Munin backs
  that up: each is a primary key, a `NOT NULL` column or a `Count()` aggregate, so a null in one is
  a broken payload rather than a shape the API can send. (Fhi.Metadata-o355u)
- **Every root element now carries the package version in `data-munin-explorer-version`, so you can
  tell which version is serving a page from the browser alone.** Read it with
  `document.querySelector("[data-munin-explorer-version]").dataset.muninExplorerVersion` — signed in
  or not, on any page that mounts one. The value is the assembly's `AssemblyInformationalVersion`,
  so it carries the prerelease suffix and the commit behind the `+`. Read it from the rendered DOM
  rather than from `curl`: an interactive mount is not prerendered.
- **`munin-explorer-version` is part of an attribute name and not a class**, so no stylesheet rule
  is possible for it and none is wanted. (Fhi.Metadata-sqbei)
- **A rule for `munin-explorer-filters__count` has to switch off shrinking and wrapping.** The
  facet value's `<label>` is a flex row carrying `overflow-wrap: anywhere`, so that a
  200-character databehandler breaks inside the sidebar rather than leaving it. The count sits in
  that row as a shrinkable item and inherits the wrapping, so a long value squeezed it until
  `(32)` broke between the digits and the closing bracket — two lines, reading as two numbers.
  `flex: none` and `overflow-wrap: normal` on the count are what hold it whole, and the sample
  stylesheets now carry both. Taking the wrapping off the label instead fixes the count and lets
  the long values out of the panel. (Fhi.Metadata-z1895)
- **`munin-explorer-list-scroll` is new, and a host owes it a `:focus-visible` outline.** It wraps
  the saved-list table alone, the way `munin-explorer-kilder-scroll` wraps the kilder table. Unlike
  that one it carries its own `overflow-x: auto` inline: measured in `HostileHost`, nine columns
  put 1323px of table in an 843px page and the *document* scrolled, which is WCAG 1.4.10 on the
  host's page rather than a table that looks wrong on ours — and no host stylesheet can be assumed
  to have the name the day it appears. It carries `position: relative` inline too, which is what
  makes it the containing block that clips: without it every row's absolutely positioned
  `screenreader-only` label escapes the box, and a host whose visually-hidden idiom is `clip`
  rather than a negative offset gets the sideways scroll back through it — 292px of it, measured.
  What is still the host's is the focus ring, because the box carries `tabindex="0"` and a focus
  stop nobody can see is WCAG 2.4.7. Both sample stylesheets have it; `Fhi.Helsedata.Stiler` has
  no rule for the name at all as of 0.1.37.
- **The saved list's rows are `<table>` markup now, so rules written against the old flex row miss
  them.** `munin-explorer-data-list` is still the name, but on a `<table>`, and the cells wear
  their per-column modifier without `munin-explorer-dataitem-main__column` — that class is
  `display: inline-flex`, which on a `<td>` takes the cell out of its own table. A host with rules
  of its own should scope them on the element: `VariableSearch` still draws the same names as a
  `<ul>` of flex rows and must keep doing so, because that shape is helsedata's own variable page.
  Stiler needs no change for the table to be legible — an element brings its own default where a
  class name brings nothing — but it has no rule for a `<table>` under this name either.

## 0.1.0-alpha.8 — 2026-09-03

### Added

- **The kilde table can open a row on its data collections.** A leading control column carries a
  toggle on every kilde that has any; pressing it expands the row in place and lists that kilde's
  datasamlinger, grouped as they are in the catalogue — the kilde's own first, then one group per
  delkilde. Several rows can be open at once, each is fetched once and cached, and an open row
  survives filtering: it is held by the kilde's id, not by its position in the list.

### Changed

- **The clear-search control is now an ✕ inside the search box, left of the search button, in
  both explorers.** It was a separate *Tøm søket* button standing under the field. It is drawn
  only while there is something to clear, replacing the always-present greyed state — an ✕ inside
  an empty box invites a press that would do nothing. It is still the package's own `<button>`
  and the field is still `<input type="text">`: the browser's own ✕ on a `type="search"` field
  fires an event Blazor does not bind, which is the defect that removed it from inside the box in
  the first place. Pressing it does exactly what the old button did — the variable explorer
  re-runs the search with no term so the API, the facet counts and `SearchChanged` all follow,
  the kildeutforsker restores its list without a request — and it now returns focus to the search
  field, because the control leaves the page as it acts.
- **BREAKING for hosts on `0.1.0-alpha.7`: `VariableExplorer` is the whole variabelutforsker.**
  Search, the reader's own variable lists behind Runa's two tabs, and the view in the address bar,
  from one mount. The search half on its own is now `VariableSearch`, and the separate
  `VariableExplorerWithUrlState` is gone — `VariableExplorer` does what it did.
- **The tabs sit below the search box and the filters, not around them**, which is where Runa puts
  them: the heading, the search field and the facets stay on screen whichever tab is open, and only
  the results and their pager belong to the first. A signed-out reader gets no tablist at all.
- **Removed: the "Koble konto" account-link control.** It was Munin's own and no host wants it. The
  client keeps `RedeemIdentityLinkAsync`, so a host that wants the feature can still build one.

### Notes for hosts

- **`munin-explorer-search` is gone, and `munin-explorer-search__clear` now needs a rule that puts
  it inside the field.** The wrapper existed only to place the clear button beside the search box
  and has no element to name any more; a host styling it can drop those rules. The clear control
  itself moved into `searchbox__freetext-container`, which is the positioned box the search button
  already sits in, so its rule wants `position: absolute` and a `right` offset that clears the
  search button while staying inside the padding the field reserves — in the sample stylesheets
  that is `right: 72px` at `2rem` wide against a 104px reservation. Those numbers are the samples'
  own: a host whose search button is a different width needs its own. A host that defines nothing
  for the name gets the control in normal flow after the field, which is roughly where it stood
  before, so nothing disappears. It carries no visible text — the label is on `aria-label` — so a
  rule that hides it hides the only way to clear the search.
- **It still needs a muted `[aria-disabled="true"]` appearance, in the variable explorer.** The
  control is drawn whenever the box has a term, but the variable explorer refuses the press while
  its own search is in flight, so there is a window where it is on screen and will not act. It
  says so with `aria-disabled` rather than `disabled`, which would drop the focus this control was
  moved inside the field to keep — so it stays focusable and hoverable, and both states need to
  stop looking like invitations. The kildeutforsker fetches nothing and never carries the
  attribute.
- **Two renames, one line each.** A host mounting `VariableExplorerWithUrlState` mounts
  `VariableExplorer` instead — same parameters, plus the Variabelliste tab. A host that deliberately
  wanted the bare search component under the old `VariableExplorer` name now names `VariableSearch`.
  Nothing else moves: the CMS field whose default is `Fhi.Munin.Explorer.Blazor.VariableExplorer`
  becomes correct without being touched.
  <br><br>
  **A page that mounts `VariableExplorer` and `VariableListView` side by side now draws the list
  twice**, once in the tab and once beside it. That page still compiles, so nothing will tell you:
  drop the separate `VariableListView`, or mount `VariableSearch` if you want the two apart.
  <br><br>
  It must be mounted at an **interactive render mode** — `render-mode="Server"`, never
  `ServerPrerendered` — because it now owns the query string, and it throws on initialisation
  rather than drawing a page whose URL never follows the view. Pass `IsAuthenticated`, or the
  Variabelliste tab is empty by design.
  <br><br>
  **No new class names.** The tablist wears `munin-explorer-meta__tabs`, `munin-explorer-meta__tab`,
  `munin-explorer-meta__tab--active` and `munin-explorer-meta__tab-content`, which the detail
  panel's tabs already wear, so `Fhi.Helsedata.Stiler` needs no new rule for this change.
- **`munin-explorer-account-link` and `munin-explorer-account-link__actions` are gone**, with the
  control that wore them. Any rule for them in `Fhi.Helsedata.Stiler` now matches nothing and can be
  dropped. No new name replaces them: the tablist wears `munin-explorer-meta__tabs`,
  `munin-explorer-meta__tab`, `munin-explorer-meta__tab--active` and
  `munin-explorer-meta__tab-content`, which the detail panel's tabs already wear.
- **Three new class names need rules: `munin-explorer-kilder__expand`,
  `munin-explorer-kilder__expand-toggle` and `munin-explorer-kilder__expanded`.** The first is the
  control column and needs a width; the toggle wears `hd-button-reset`, so without a rule it is a
  bare glyph with no hit area of its own; the third is the expanded row's cell, which needs padding
  to read as a panel rather than as another table row. A host that skips them still gets a working
  toggle and a readable list — this is look, not information. The rule for them in
  `Fhi.Helsedata.Stiler` is tracked as its own bead.

## 0.1.0-alpha.7 — 2026-09-03

### Added

- **A way from a kilde or datasamling back to *its* variables.** The drill-in view offered only
  "← Tilbake til variabler", which returns to the unfiltered list — so a reader who opened a
  datakilde to find out what it was had no way to then see what it holds, and Runa's kilde and
  datasamling views both have that path. Beside the existing control there is now **"Vis bare
  variabler fra denne datakilden"** (and the datasamling's equivalent), which closes the view and
  narrows the list to that owner.
- **The narrowing sets the filter rather than trimming the rows**, so the facet panel shows it as
  active, the reader can take it off again, and the host's URL carries it — the narrowed list is a
  link that can be shared. Other facets the reader had set are kept; the owner's own facet is
  replaced rather than added to, because the button says *bare*. (Fhi.Metadata-3yse5)
- **A signed-in reader can redeem an account-linking code from inside the component.** The same
  person gets one `ExplorerUser` per ID-porten client, so lists saved through helsedata.no were
  invisible in Runa and the other way round. "Koble konto" in the header actions takes a code
  minted on the other login, shows what linking will do, and redeems it against
  `POST /api/explorer/my/link/redeem` with the bearer token the component already holds. Signing
  in starts nothing and navigating starts nothing — the component only ever *receives* a link,
  because it runs inside a CMS page that is not ours. Drawn only when `IsAuthenticated` is true,
  and reset whenever that crosses: the panel's stage and the code in its field belong to the reader
  who put them there, so a sign-out drops both and an answer that arrives afterwards is discarded
  rather than announced to whoever signs in next. (Fhi.Metadata-bl448)
- **`IMuninExplorerClient.RedeemIdentityLinkAsync` and `IdentityLinkOutcome`.** Each refusal the
  API distinguishes — an unknown code, an expired one, a spent one, one presented by the login
  that minted it, and two logins already linked — comes back as its own `IdentityLinkOutcome`
  rather than as an exception, so the caller can say which of them happened in the reader's own
  language. A 429 still throws `MuninExplorerRateLimitedException`, as every other write does.
  The member carries a default implementation that throws `NotSupportedException`, so a host
  implementing the interface itself keeps building. (Fhi.Metadata-bl448)
- **A coded variable's statistics now show how its values are distributed.** Under the statistics
  table, a variable whose statistic carries `kodefrekvenser` draws Runa's categorical frequency
  table: code value, category, share of valid values with a bar, and count. An accumulated
  statistic is a running total, so only its last row is drawn, and its first column is headed with
  the date the total was last computed over rather than with a year — "Sist oppdatert" / "Last
  updated" against "År" / "Year". A yearly statistic still draws every row, headed as before.
- **`ExplorerUrlState` — the explorer state a URL carries, in one value.** Sits beside
  `VariableFilter` in `Contracts/` with the same `Parse`/`ToQueryString` pair, composing the filter
  with search, sort, direction, page and page size. It owns the default page size, so a host no
  longer keeps its own copy of that number to know what to leave out of a link.
- **The list view can rename and delete a list.** The holder patches its own copy, so the new name
  is on screen without a round trip, and a deletion is confirmed first because the API offers no
  undo. (Fhi.Metadata-fjiba)
- **Deleting the list on screen leaves another one active, or none.** The active list used to go on
  pointing at the deleted one, so the view asked for the variables of a list the API no longer has
  and drew an empty table for a list that is gone.
- **Neither takes the circuit down when the API refuses.** A throttled rename or delete says the
  reader has asked too often; any other failure says to try again. Both stay inside the handler.
- **`DatasamlingView`, the datasamling in the shape a kilde already had**: name and code, the
  catalogue's own metadata in its own groups, the inclusion and exclusion criteria as prose, and a
  sidebar of who owns the data and how much of it there is. The variable explorer's drill-in used a
  flat list of eleven fields that drew none of the curated metadata. (Fhi.Metadata-jgfum)
- **A statistics block, headed by the kind of statistics** — "Statistikk (Årsbasert)" — with the
  telleenhet, the frekvens and the variable count. A datasamling that counts nothing draws no block
  at all rather than an empty one.
- **The saved-list view has an "Ønskede data" column, and a signed-in reader can write in it.**
  Free text per variable — what the reader wants out of that variable, which is the other half of
  a data application from which variables they picked. It is stored server side, so the same note
  written in Runa shows up here and the other way round; the two surfaces stopped disagreeing about
  the same list. Signed out the component still renders nothing at all, exactly as before: it has
  no anonymous list, and this change does not add one. (Fhi.Metadata-m74i4)
- **`IMuninExplorerClient` gained `SetMyListDesiredDataAsync`, and `VariableListItem` gained
  `DesiredDataType` and `DesiredDataFreeText`.** The two fields are optional and additive, so a
  host reading the rest of the item is unaffected. The method carries a default body that throws,
  like `ExportListAsync` before it, so a host implementing the interface itself keeps building.
- **The API's refusal of an over-long note reaches the reader.** The text is capped at 500
  characters server side, and the cap is not written down in this package: `DesiredDataResult`
  carries the ceiling the API named, so the sentence the reader sees quotes the API's own number
  and cannot drift from it. Their text stays in the field rather than being reverted under them.
- **A refused note stays refused until it is rewritten.** The mark on the field and the sentence
  naming the ceiling used to be dropped by the next thing the reader did — saving another row,
  removing one, downloading the list — leaving 500-odd unsaved characters looking saved, or the
  field marked wrong with nothing saying why. Both now stand until that row is written again or
  leaves the list, in an alert region of their own that the field points at, and the text survives
  a reload from anywhere.
- **Two notes written at once no longer answer for each other.** Blur is what saves, so a reader
  correcting one row and moving to the next has more than one write out at a time. An answer is now
  applied only if the row, the list and the page it was typed against are still the ones on screen:
  a late refusal cannot mark a text that was accepted, a late success cannot take away the sentence
  another row's failure just put on screen, and a page read landing under a write cannot leave the
  reader told to shorten a text that is no longer there. A list whose switch was refused takes its
  rows with it, so no row is left on screen against a list it did not come from.
- **The filter panel has a toolbar: Utvid alle, Skjul alle and Nivålinjer.** The first two fold and
  unfold every facet at once, which a native `<details>` cannot do for itself. Nivålinjer puts
  `data-level-lines="true"` on the panel for a host to draw a guide line per level from. **The
  package draws no lines**, and a host with no rule for that attribute sees nothing change when the
  toggle is on; both sample stylesheets show the rule. It is an attribute and not a class name so
  that no new name enters the set a host has to style. (Fhi.Metadata-wcbxi)
- **A host drawing those lines must clear 3:1.** They are a non-text control under WCAG 1.4.11, and
  the panel sits on the page ground rather than on a card: the samples' ordinary border grey gives
  1.16:1 there and is invisible above about 1000px, so the rule uses a darker token at 6.76:1. The
  same bar applies to a host's dark theme, which neither sample defines.
- **`LevelLines` / `LevelLinesChanged` is a new two-way parameter**, off by default. The package
  remembers nothing itself — `localStorage` from a Blazor circuit is a JS interop call this package
  makes none of — so a host that wants the choice to survive a visit stores what the press raises
  and passes it back.
- **`VariableExplorer` and `KildeExplorerWithUrlState` — the explorers with their state in your
  address bar, and no glue to write.** A link restores the search, the facets, the sort, the page
  and the open kilde; every change the reader makes updates the URL. `ExplorerUrlState` is still
  there for hosts that would rather own the address bar themselves.
- **`VariableFilter.QueryKeys`**, the facet half of what an explorer link carries. `DeclinedKeys`
  names query keys the explorer must leave alone, for a page that already means something else by
  `?page=`.

### Changed

- **The count beside a facet value is now its own element in both explorers**, a
  `<span class="munin-explorer-filters__count">` inside the value's `<label>`, where it used to be
  part of the label's text run. The visible text is unchanged — `Biobank (1)`, parentheses and all
  — and so is the checkbox's accessible name, which still holds the count. Hosts that want the
  number dimmed or right-aligned can now style it on its own. (Fhi.Metadata-cgk85)
- The pager is helsedata.no's own shape now: `Forrige`, numbered pages, `Neste`, and a page-size
  dropdown. It drew "Side 1 av 907" between two buttons before, which says where the reader is and
  gives them nowhere to go — the last page of a long result was reachable only by pressing `Neste`
  until it arrived. The run carries the first page, the last page and three around the one in
  force, as `1 2 3 … 907`; where a skip would stand for a single page, that page is drawn instead.
- The page-size control is a `<select>` where it was three buttons. It takes its accessible name
  from a `<label for>` rather than repeating the phrase on every button, and a size a host asks for
  that is not one of the three — `PageSize="30"` — is added to the list rather than left out, since
  a select with no option for the size in force falls back to showing the first one.
- `VariableListView`'s pager changed with it and draws the same run from the same renderer, so a
  reader's own saved list is no longer walkable one page at a time either.
- The pager's buttons wear the classes helsedata.no's own pager wears: `hd-button-reset` on the
  numbers with `current` on the page in force, and `hd-button-square button-square--ghost` on
  `Forrige` and `Neste`. They were square buttons throughout, which looked right without a
  stylesheet precisely because it is not what helsedata draws.
- **A filter value in the variable explorer is a checkbox rather than a two-state button**, which is
  the shape Kelda's facets have always had — so a reader who uses both meets one way of choosing a
  value rather than two. The visible text is unchanged, count and all, and the count is still part
  of the control's accessible name. (Fhi.Metadata-j0a2h)
- **The way out of a drilldown is a blue link rather than a plain ghost button.** `--ghost` carries
  no border and no background until `:hover`, so "← Tilbake til variabler" and "← Tilbake til
  kildeutforsker" read as bold text to anyone not using a mouse. All three now wear Stiler's own
  `button-square--ghost-blue`, which is the colour a reader already takes for a link — and these
  controls are navigation. (Fhi.Metadata-l9l2n.34)

### Fixed

- The saved-list view draws its columns with the same cell helper the search results use, so a
  row reads the same on both. Its cells gained what the results already had: the field name for a
  screen reader, and the full kilde name on hover where the column shows the short one.
- The retry offered beside a failure now stands next to the sentence it answers and is drawn as a
  button. It was a ghost-styled control on the line below a coloured infobox, which read as stray
  text under a box rather than as something to press. Both retries move — the search's and the
  filters' — so two failures reported at once do not look like two different kinds of thing.
- While that retry is running, the box says so rather than emptying. It used to clear its sentence
  the moment the fetch started and leave the button standing on its own, which reads as a control
  with nothing to answer. The box cannot leave — the button inside it would go out from under the
  focus of whoever pressed it — so its words change instead, and it carries `aria-busy` while they do.
- **A throttled download names the cause, instead of reading as a plain failure.**
  `ExportListAsync` sent its own request rather than going through the client's shared write
  helper, so the one write added after 429 handling landed never inherited it: a rate-limited
  export arrived as a plain `HttpRequestException`, and the list view answered "kunne ikke laste
  ned" with nothing to say why. It now raises `MuninExplorerRateLimitedException` like every other
  call, and the view tells the reader they have asked too often. (Fhi.Metadata-3gzw5)
- A delkilde's beskrivelse is drawn in the kilde view, under its name and identifier line. It was
  held back while the view could only print catalogue text raw — the field is authored with
  markdown links more often than any other, so drawing it then would have put `[label](url)` beside
  every wave of a study — and it now goes through the same renderer as the kilde's own description,
  so the link is a link. Six of the sixty-six kilder carry one, and in the Tromsø study it is the
  only route from a wave to that wave's own page. (Fhi.Metadata-3osk6)
- The catalogue's authored markup now renders instead of printing as source. The kilde and
  datasamling descriptions and the datasamling table's description column turn markdown links
  into real links and `<br>` tags and bare newlines into line breaks, and a property the
  catalogue types as a `Url` — Hjemmeside is the one readers meet — becomes a followable link
  instead of a `[label](url)` printed whole. The grammar is deliberately that small: the text is
  parsed with Markdig and the AST is walked straight into the render tree, so no raw-HTML
  pathway exists — a heading, emphasis, a `javascript:` link or any HTML tag renders as literal
  text, links carry `rel="noopener noreferrer"` and only `http`, `https` and `mailto` schemes
  become anchors, and text over 20 000 characters is not parsed at all.
- **The open kilde reads as loading from the click that opens it**, not from the moment its fetch
  starts. `KildeExplorer` raised `SelectedKildeIdChanged` before starting the detail request, so a
  host that does anything asynchronous in that handler — writing the URL, as both sample hosts do
  — got one render of the drilldown with `aria-busy="false"` over an empty status line, announcing
  a finished and empty lookup that had not been made. (Fhi.Metadata-74cbp)
- **Going back from an open kilde is no longer undone by the fetch that follows it.**
  `KildeExplorer` asked for the detail of the kilde the click carried rather than the one still
  open, so with a host that does anything asynchronous in `SelectedKildeIdChanged` — writing the
  URL, as both sample hosts do — a reader who pressed Back inside that window had a request issued
  for the kilde they had just left. (Fhi.Metadata-8wpau)
- **A kilde's description is printed once, not twice.** Opening a source drew the whole description
  as the lead paragraph and again as a field under EHDS / HealthDCAT-AP — on Barnediabetes, 1441
  identical characters a screen apart, under a heading that suggested it was something else. The
  panel is about a third shorter for it, and the genuinely unseen fields sit higher up.
  (Fhi.Metadata-8yqoz)
- The mechanism that prevents this already existed and was already used by the variable view: the
  kilde view now names the keys it renders itself, so the catalogue metadata leaves them out. Both
  the plain and the multilingual spelling, since a source curates one or the other. The **English**
  description is deliberately not excluded — the lead paragraph is the Norwegian one whatever the
  reader's language, so that text appears nowhere else and dropping it would delete a field rather
  than de-duplicate one.
- **The open variable is in the address bar, so a link to one can be shared.** Opening a variable
  writes `?variabelId=` and closing it removes the key again; a link opens that variable with the
  search, facets, sort and page around it intact. `ExplorerUrlState` gained a matching
  `SelectedVariableId`. (Fhi.Metadata-deogd)
- **The startup failure and the README snippet for `ApiBaseUrl` name a host that answers off the FHI
  network** - both offered the Munin test host without its `runa` prefix, which resolves to a private
  address reachable only from inside FHI. A host that copied either one booted, called an address it
  could never reach and showed "Kunne ikke hente variabler nå" while the API logged nothing. Both now
  give `https://runa.munin.skytest.fhi.no`, the exception says why the prefix matters, and a test
  reads the whole checkout - sources, samples, tests, docs, scripts, workflows and the packaged
  README - to keep the unprefixed host out of everything a host developer can copy. (Fhi.Metadata-ip02g)
- **A variable's statistics now show in the result row's Data tab**, beside the kodeverk, as Runa
  shows them. The tab drew kodeverk alone and left the numbers one click further in, inside the
  whole-variable view — so a reader who opened a row to see what its values look like was told
  nothing about them. A variable with no statistics draws no heading and no empty table, exactly
  as the full view already behaved. (Fhi.Metadata-isvb7)
- **The statistics heading and table are now one shared block** rather than a section only the
  whole-variable view knew how to draw. The emptiness check lives inside it, so the two surfaces
  cannot drift apart on the question of what an absent set looks like. No markup changed in the
  full view.
- A multilingual catalogue field now shows **every** language it holds rather than the reader's
  alone, each on its own line and each named. The bag the catalogue stores these in is open while
  the page offers two languages, so a slot in any third was unreachable by construction — no
  toggle on the page could ever have selected it. Fields holding both Norwegian and English are
  the common case today: 39 of them across some 20 kilder.
- A value in a language this package cannot name is now marked with the language tag the
  catalogue used, instead of the reader's. Marking it with the reader's left `lang` off the
  element altogether, so the text inherited the host's and a Norwegian page announced German as
  Norwegian to a screen reader (WCAG 3.1.2). The same fix covers an English-only value on a
  Norwegian page, which is reachable in today's catalogue rather than hypothetical.
- A language-tagged list that spells Norwegian both ways now shows all of its entries. The
  entries were gathered under the tag each one carried, so a list mixing `no` and `nb` — which
  includes any list mixing tagged entries with untagged ones, since an untagged entry is read as
  `no` — became two Norwegian slots, and everything after the first was dropped with nothing on
  the page able to reach it.
- Catalogue properties are drawn according to the type the catalogue declares for them, so a
  value stored as structure no longer reaches the page as JSON. `MultilingualText` and
  `LangTaggedList` resolve to the reader's language, `MultiSelect` resolves each of its codes
  through the property's own vocabulary instead of matching the whole array against it, and
  `Object` — which has a curated label but no curated parts — drops its row rather than printing
  the record. A value that is not the shape its type promises is still shown as it arrived.
- Rows carrying a multilingual value now report the language they resolved to, so an English
  title is no longer marked `lang="no"` and read aloud in a Norwegian voice.
- **Creating a list can no longer take the circuit down.** The call was made without a guard, so
  anything it threw — a 429 from the rate limiter, an API that had gone away — left the
  event handler and took the Blazor circuit with it. The reader got a blank page and a reconnect
  banner in place of the list they were building. It now says what happened and stays where it is:
  a throttled attempt says too many requests, anything else says the save failed.
  (Fhi.Metadata-l9l2n.32)
- **Switching to the newly created list is guarded too**, the same way choosing one from the picker
  already was. The list exists on the server either way; only the switch to it is lost, which is
  what the message says.
- **The alert answers for the action the reader just took.** Four conditions share that one region,
  and a load that had failed earlier outranked all of them — a failed save read as "kunne ikke hente
  listen". Starting an action now clears the other three; a load that fails again says so again.
- **Removing a variable from a list can no longer take the circuit down.** The call was made
  without a guard, so a 429 from the rate limiter left the event handler and took the Blazor
  circuit with it — a blank page and a reconnect banner in place of the row the reader wanted
  gone. A throttled removal now says the reader has asked too often, anything else says to try
  again, and the view stays where it is. (Fhi.Metadata-l9l2n.33)
- **A throttled switch to a newly created list names the cause.** It was guarded already, but
  every failure read as "Kunne ikke hente listen"; a 429 now says so, the way the create half of
  the same handler does.
- **A removal the API declines is no longer a silent non-event.** The list view acted only on a
  removal that threw; one the API took and answered no to — a 404 for a list that is no longer
  the reader's — left the row on screen with nothing said about why. It now says "Kunne ikke
  endre listen", the same sentence a refused rename or delete has always shown.
  (Fhi.Metadata-l9l2n.35)
- **The filter panel's toolbar keeps its three buttons on one row.** Utvid alle, Skjul alle and
  Nivålinjer sat in inline flow with a margin each, and the last one's trailing 16px counted
  against the line: at the 369px an expanded panel leaves once it grows a scrollbar, the row needed
  369.05px and Nivålinjer dropped onto a row by itself. They now sit in a container of their own,
  spaced by `gap`. (Fhi.Metadata-l9l2n.37)
- A variable whose payload names the same kodeverk twice no longer crashes the component when
  its kodeverk list is re-rendered. The two lines were given the same Blazor key, and diffing
  that list threw inside the renderer — which in a Blazor Server host takes down the page the
  component is embedded in, not just the component.
- A kildekodeverk the Explorer API resolves no name for is now drawn by its code values
  instead of as "Ukjent navn" above an internal Munin reference and a collapsed "Vis koder"
  button. Up to eight codes appear inline as the link's identity; beyond that a preview and a
  "Vis alle (N)" control open the same full code list the existing toggle opens. Those codes
  are fetched when the panel opens rather than on a press, so hosts see one extra request per
  nameless link per variable opened — named links are still fetched only when asked for. While
  the fetch is out the line says so; if it fails or the API publishes no codes, the reference
  and the control come back, so nothing becomes unidentifiable. Administrativt and helsefaglig
  kodeverk links are unchanged.
- **`VariableView` no longer writes an English reader a Norwegian ordinal dot.** Its sidebar dates
  read "20. Sep 2022" whatever language the host asked for; they now read "20 Sep 2022" in English
  and "20. sep. 2022" in Norwegian. The abbreviated month stays — the sidebar is narrow enough that
  a spelled-out one wraps — and the kilde and datasamling views still spell theirs out.
  (Fhi.Metadata-n39ea)
- **`KildeView.Sections` is documented as what it actually receives.** The XML docs that ship with
  the package said the kilde explorer passes its datasamling hierarchy through this slot and that
  the variable explorer passes a datasamling section: the hierarchy is drawn by the view itself, and
  the variable explorer passes nothing. (Fhi.Metadata-x8sd9)
- A call that cannot reach Munin now gives up in about five seconds instead of up to a hundred.
  The client had no timeout of its own, so it inherited `HttpClient`'s hundred-second default, and
  an unreachable host is a connect the OS retries for roughly twenty-one seconds per address —
  measured at 12 and 33 seconds against a dropped network, under a spinner, with nothing the reader
  could press. `ConnectTimeout` is now five seconds and the whole request is bounded at thirty,
  which is far above any healthy search: the live catalogue answers in well under a second.
- The connect limit is set only where it exists. `SocketsHttpHandler` is unsupported on `browser`,
  so a WebAssembly host keeps the plain handler and is bounded by the thirty-second request timeout
  alone — fetch decides its own connect there and gives us no say. Getting that wrong is a build
  error rather than a host that fails to start, which is how it was caught.
- A read that fails because the connection under it had died is sent once more, on a fresh one.
  A pooled connection can be dead with nothing having said so — the network goes away, the sockets
  stay in the pool, and the next request is written into one and fails on the read after seventeen
  seconds of retransmission. No connect happens there, so no connect timeout shortens it, and
  .NET's own retry does not cover it: that one repeats a request the connection refused before it
  was sent. Only GET and HEAD, and only once — a reset during the response read says nothing about
  whether the server processed the request, so a save must not be repeated, and a second failure is
  the network being down rather than one stale connection.
- Connections are retired on a schedule this package chooses rather than on whichever of two
  mechanisms fired first. `PooledConnectionLifetime` is thirty seconds and the factory's handler
  rotation is off: supplying a primary handler without setting the first leaves DNS refresh to the
  factory discarding the handler every two minutes, which is the pairing the setting exists to
  replace rather than race.
- **`KildeExplorer` no longer heads the datasamling section "Delkilder og datasamlinger" on a source
  that has no delkilder.** It passed that word over every source it opened, so on 61 of the 66
  sources the API serves the heading named something the section did not draw. It now passes no
  heading and takes `KildeView`'s default, which reads the source: "Delkilder og datasamlinger" when
  there are delkilder, "Datasamlinger" when there are none. `VariableExplorer` already behaved this
  way, so the two explorers now head the same source with the same word. A host that wants a word of
  its own still sets `DataCollectionsHeading` on `KildeView`. (Fhi.Metadata-rhybi)
- **The empty state says when historical variables are being hidden.** "Vis historiske" is off by
  default and lives in a collapsed facet group, so a search for a variable that exists only
  historically returned "ingen treff" with nothing to suggest a toggle would find it. The sentence
  now names the toggle — and only while it is off, since offering one already on would point the
  reader at the explanation they have already ruled out. Both languages. (Fhi.Metadata-rkjlx)
- **The datakategori and dataperiode filters are drawn.** Both were carried by the contract and by
  shareable links already, and neither was ever rendered — so someone using the explorer through
  helsedata.no could not filter on them while the same person could in Runa. Datakategori is an
  ordinary multi-select facet; dataperiode is a from and a to date, bounded by the range the API
  reports for the current selection. (Fhi.Metadata-uidue)
- **Datakategori shows the catalogue's own words**, resolved through the same property-metadata
  vocabulary Kelda reads, so the panel says "Befolkningsundersøkelser" rather than
  `ehds-cat:population-health-surveys`. A token the vocabulary does not name is shown as itself
  rather than dropped. Losing the vocabulary costs the choices their words and nothing else — the
  facet still filters, and reports no second error.
- **Placement is Runa's where it can be.** Dataperiode takes Runa's own slot, after Datatype and
  before Helsefaglig kodeverk. Datakategori is third rather than Runa's first, because the two
  above it are in helsedata's own order deliberately.
- A facet may now carry **its own control** instead of a list of values or an empty-state sentence.
  The dataperiode needed it: holding no facet values, it was dropped as empty under the old rule,
  and given empty text to survive that it drew the sentence instead of the date fields. No new CSS
  class name — the date fields are native inputs, for the reason the panel's `<details>` and bare
  `<ul>` are elements too.
- **A facet you have chosen from stays on screen when the selection matches nothing.** The counts are
  cross-filtered against the whole selection, so a selection returning zero rows made the API report
  nothing for every facet — the chosen value included, name and all — and the panel dropped them.
  The reader was left filtering by something they could neither see nor undo, with the address bar
  as the only way out. Measured against skytest: one kilde plus a date matching nothing took the
  kilde facet from 43 entries to none. (Fhi.Metadata-v2bgr)
- **The counts disappear rather than going stale.** While a selection matches nothing, the controls
  on screen are the ones the reader was last offered; the numbers beside them would describe a
  different moment, so they are not shown. They come back as soon as the API has something to say.
- A reader who **arrives on a link that already matches nothing** has no previous answer to keep, so
  the panel asks once what the catalogue holds at all. Without it a shared link could strand
  whoever opened it, which is the harder half of this to notice.
- **The address bar keeps the path the host mounted the explorer at** - filtering, searching, sorting,
  paging or opening a kilde changed only the query before it also moved the reader to the application
  root, where the explorer is not mounted. Only hosts mounting under a sub-path were affected.
  (Fhi.Metadata-ydpny)
- **A half-typed date no longer empties the result list.** A native date input reports a complete
  value as soon as all three segments hold digits, so typing `01.01.2017` into *Til og med* arrived
  as `0002-01-01` on the way — which was applied, emptied the list and reached the host's URL. A
  date outside the bounds the field itself advertises is now ignored. Since the *to* field's lower
  bound is the *from* date, this is also the check that the end of the period comes after its
  start. (Fhi.Metadata-yxhv1)
- **The dataperiode facet stays on screen while it is filtering.** A date filter matching nothing is
  exactly when the API stops reporting a range, and the facet was dropped on that — taking away the
  only control that could undo the filter that emptied the list, and leaving the address bar as the
  way out. A facet carrying an active filter is now drawn whether or not the API reports a range
  for it.

### Notes for hosts

- **Two class names to style if the facet panel is to be open on a wide screen.** Kelda's kilde
  list emits `munin-explorer-filters__toggle` for the "Vis filtre" button and
  `munin-explorer-filters__facets` for the panel it unfolds. The fold itself is the browser's own
  `hidden` attribute, so a host that supplies no rule for either still gets a panel that opens and
  closes at every width, and nothing is broken. What it does not get is the sidebar: the filters
  stay folded behind "Vis filtre" on a screen with room to show them outright. What a host has to
  supply is one media query at its own sidebar width holding two declarations,
  `display: none` on `.munin-explorer-filters__toggle` and `display: block` on
  `.munin-explorer-filters__facets[hidden]`. Both or neither: hiding the button while the facets
  stay folded leaves no way to open them at all, which is worse than the fold.
  `Fhi.Helsedata.Stiler` carries the pair at `min-width: 1024px`, the width its own rules move the
  panel into the sidebar at, and both sample hosts' `host.css` carries the same pair. A host on
  neither has to write it. (Fhi.Metadata-2fomm.3)
- One new class name, `munin-explorer-alert`, the row holding a failure and the control that
  answers it. A host without a rule for it gets what it had before — the message and the button
  stacked — so this is a name a host owes rather than one it breaks without. Both sample hosts
  draw it as a wrapping flex row with a 16px gap.
- It also needs `.munin-explorer-alert .infobox { margin: 0; flex: 1 1 auto }`. Stiler centres an
  infobox in its column with `margin: auto`, and inside a flex row an auto margin eats the free
  space and pushes the button off the end of it. `flex: 1` is the other half: the sentence changes
  while a retry runs, and a box sized to its own words would move the button left and right under
  the reader's pointer. Filling the row up to Stiler's existing 720px cap keeps it still. The rule
  belongs beside the row's own in `components/munin-explorer/`.
- The two retry buttons wear `button-square--secondary` where they wore `button-square--ghost`,
  which is Stiler's own filled pair and the one Tøm søket already uses. No new name, and nothing
  further owed for it. (Fhi.Metadata-31ogu)
- `aria-busy="true"` appears on the failure box while the retry it offered is running. Both sample
  hosts draw a gradient wave across the box from it, behind a `prefers-reduced-motion: reduce`
  guard, since a moving gradient is what WCAG 2.3.3 asks to be able to turn off. A host that styles
  nothing for it loses only the wave: the words in the box already say a retry is running.
- **The inert rule for `munin-explorer-retry` in Stiler must gain a background.** It currently sets
  `color: var(--grey60)` and nothing else, which was right while these buttons were ghosts and is
  wrong now they are `button-square--secondary`: that is grey60 text on a grey60 background, a
  caption nobody can read until a hover changes the background under it. The pair the pager already
  uses is the fix — `background-color: var(--grey30); color: var(--grey60)`, on both the base and
  the `:hover` — because the pager's buttons are secondary too. Both sample hosts carry it.
  Until Stiler ships it, a Stiler-only host draws a retry button whose words are invisible while it
  is inert, which is worse than the state `Fhi.Metadata-x6vqc` fixed. Neither guard here can catch
  that: both ask whether a name has a rule, not which declarations the rule carries.
- **One new class name, `munin-explorer-kilde__delkilde-description`**, the paragraph holding a
  delkilde's own words inside the delkilde tree. The rule has to land in `Fhi.Helsedata.Stiler`
  under `components/munin-explorer/`, which this repository's CI cannot see, so a green build here
  does not mean the paragraph is styled on helsedata.no. Undrawn it costs look rather than
  information — it is a `<p>` and a browser draws one readably — but it is prose sitting between a
  heading and a table, so without a measure and a margin it runs the full width of a wide window
  and crowds the table under it. Both sample stylesheets carry the same rule the kilde's own
  description wears one size down: `margin: 8px 0 0`, `max-width: 65ch`, `color: var(--grey60)`
  and `font-size: 0.9375rem`. That last one is measured against Stiler's own `headline-xxs`, which
  is `1rem` — the size the delkilde's name above it wears — so the prose sits just under its
  heading rather than over it. The samples' `headline-xxs` stand-in is smaller than Stiler's, so in
  a sample host alone the two read the other way round until that stand-in is corrected.
  (Fhi.Metadata-3osk6)
- **The package now depends on Markdig** (BSD-2-Clause), which every consuming host restores
  transitively. It is the parser behind the catalogue-text rendering above; nothing about
  registration or configuration changes. No new class names come with this change — the anchors
  and breaks render inside elements the views already emit. (Fhi.Metadata-5bcr7)
- **The README now lists every `munin-explorer*` class name the package emits, name by name.** The
  eight `VariableView` writes — `munin-explorer-whole` with its `__header`, `__code`,
  `__description`, `__body`, `__main`, `__aside` and `__list` — had been in no document here at all,
  and the hand-written counts beside the other views had all drifted, so the counts are gone and an
  inventory table with a kind per name replaces them.
  (Fhi.Metadata-6gkjd)
- **`scripts/assert-class-names-listed.sh` keeps that list honest.** It reconciles the whole prefix
  against the README on every CI run, in both directions, where the older check could only ask about
  names new on a branch. (Fhi.Metadata-6gkjd)
- **The sample hosts drew Stiler's `headline-xxs` and `headline-s` a size too small.** The stand-ins
  were 14px/150% and 20px/150% where the real classes are 16px/160% and 21px/160%, so the samples
  under-stated the component: the `dt` field labels, the delkilde names and the kilde facet headings
  all sat a step below what helsedata.no renders. A host that took its rules from the samples should
  take the corrected sizes with them; the weight stays the sample's own 500, standing in for the
  licensed `graphik-medium` face their declaration names.
- **The account-link panel adds two class names a host has to provide,
  `munin-explorer-account-link` and `munin-explorer-account-link__actions`.** Neither is in
  `Fhi.Helsedata.Stiler` today and neither carries state: the panel is the box that hangs under
  the "Koble konto" trigger, and the second is the row its two buttons sit in. Undefined, both
  still work — the panel renders in the flow of the actions row instead of floating over the
  results, and the buttons stack instead of sitting side by side. That is cosmetic rather than
  misleading, which is why this is a note and not a defect, but a panel that widens the header
  row is visibly not what was intended. Both sample hosts show a working approximation: absolute
  under the trigger at `top: 36px`, the same offset the choicepicker beside it uses.
  (Fhi.Metadata-bl448)
- **Everything inside the panel wears a name Stiler already defines.** The label is
  `form-element__label`, the code field is `searchbox__freetext` — the search box's own input —
  and the four buttons are `hd-button-square` with `button-square--secondary` or
  `button-square--ghost`. Nothing there needs a new rule, which is deliberate: an unstyled text
  input inside an otherwise styled page is the failure this package exists to avoid, so the field
  borrows rather than inventing. (Fhi.Metadata-bl448)
- **New class name: `munin-explorer-filters__count`**, on the number beside every facet value in
  both the kilde explorer and the variable explorer. A host that defines no rule for it loses
  nothing it had before — the count renders inline, exactly as it did when it was part of the
  label's text. A rule is what buys the dimming and the tabular alignment that keep a column of
  numbers from reading as more of the words in front of them; the sample stylesheets show one.
- **Style the count freely, but do not hide it.** The label names the facet checkbox, so the count
  is announced with the value it counts. No layout rule undoes that: `position: absolute`, a flex
  `order` and `display: contents` on the label all leave the announced name at `Aktiv (3)`, because
  CSS changes where a box is drawn, not what the label contains. `display: none` and
  `visibility: hidden` on the count do drop it from that name — a host that hides the number hides
  it from screen readers with it. Dimming, alignment, spacing and repositioning are all safe.
  (Fhi.Metadata-cgk85)
- **Three new class names need rules: `munin-explorer-frequency`, `munin-explorer-frequency__track`
  and `munin-explorer-frequency__fill`.** The first is the categorical frequency table and needs
  only what a host gives its other tables. The other two are the share bar and carry meaning
  nothing else carries: `__fill` is an inline element whose width is set per row, so without
  `display` and a height it draws nothing at all and the bar simply disappears. The percentage is
  written beside it as text, so a host that skips these loses the visual encoding rather than the
  fact. The rule for them in `Fhi.Helsedata.Stiler` is tracked as its own bead.
- One new class name, `munin-explorer-pagination-pages`, the row of numbered page buttons between
  `Forrige` and `Neste`. **A host has to draw this one.** The buttons inside it wear helsedata.no's
  own `hd-button-reset`, which strips the button chrome, and the page in force is marked with their
  `current` — so without a rule the run is a line of bare digits and nothing says which page you
  are on. `Forrige` and `Neste` are `hd-button-square button-square--ghost`, also helsedata's own
  pair. All four were read off their live pager on 2026-09-03 rather than guessed at. Both sample
  hosts draw the run as a wrapping flex row and bold the `.current` digit.
- **The three names that carry the pager's layout still owe a rule.** `munin-explorer-pagination`,
  `munin-explorer-pagination-content` and `munin-explorer-pagination-size` were measured on the
  helsedata mount on 2026-08-31 computing to `display: block` with `gap: normal`, filling 2025px:
  the whole pager stacked as blocks instead of laid out as a row. Only
  `munin-explorer-skiplink-pagination` had a rule, and re-checking the `Fhi.Helsedata.Stiler`
  working copy on 2026-09-03 found `origin/main` still carrying exactly that one and no other.
  (This repository's README used to say the pager's rules shipped in 0.1.14. They did not, and it
  no longer says so.) `-content` is the one that matters most — it is
  where `display: flex` with a 16px `gap` belongs — and `-pages` and `-size` want the same
  treatment inside it. Until they land, a host renders this pager as a column of controls whatever
  else it has. Neither guard in this repository can see that: both ask whether a name has a rule in
  the capture of helsedata's live page or in the sample stylesheet, and neither reads Stiler.
- The page-size `<select>` deliberately wears **no class**. An element degrades to its own browser
  default where an unknown class name degrades to nothing, and no select name could be verified
  against Stiler from this repository. A host styling it should reach it as
  `.munin-explorer-pagination-size select` — Stiler's `components/_select.scss` is the look it is
  meant to have. Both sample hosts style it that way.
- `munin-explorer-pagination-size` no longer holds three buttons, so a host with a rule written
  against `.munin-explorer-pagination-size button` is styling something that is no longer there.
  The label inside it is a `<label>` where it was a `<span>`; both still wear `caption`.
- **Shareable search links no longer need writing from scratch.** The parsing a host had to build
  to put explorer state in its own address bar is now `ExplorerUrlState.Parse` / `.ToQueryString`.
  What stays yours is what only you know: reading the incoming query server-side, the path the
  component is mounted on, where a sibling explorer lives, and the `history.replaceState` call.
  `ExplorerUrlState.QueryKeys` names the parameters we read, so you can tell them from your own —
  anything not in that list is left untouched.
  <br><br>
  Three things worth keeping if you write that glue: mount with `render-mode="Server"`, never
  `ServerPrerendered` (an `EventCallback` serialises to an empty delegate across a static-SSR
  boundary, so the URL silently stops following the view); build the path from `PathBase + Path`
  rather than `Path`, which is identical locally and wrong behind a reverse proxy; and use
  `replaceState` rather than `pushState`, or every filter change becomes a history entry the reader
  has to walk back through.
- **`munin-explorer-group` is now spacing only, and the heading style is yours again.** The
  catalogue group headings in the detail panel, the kilde view, the datasamling view and the
  variable view already wear `headline headline-xxs margin--none`. The rule for
  `munin-explorer-group` used to write Runa's eyebrow over the top of that — `0.6875rem`, weight
  700, uppercase, `letter-spacing: 0.08em` and a navy of its own — which drew a group heading at
  11px above the 16px values it introduces, so the panel did not scan as sections at all. All that
  is left in the rule is `margin: 20px 0 8px`, the space between one group and the next. Both
  sample hosts do this now, and the rule in `Fhi.Helsedata.Stiler` under `components/munin-explorer/`
  wants the same five declarations removed; a host that wrote its own copy of the eyebrow should
  drop it too. (Fhi.Metadata-gvtt9)
- **This moves both explorers, and that is the intent.** `munin-explorer-group` is shared with the
  variable detail panel, so the variable panel's Identifikasjon and Plassering headings change with
  the kilde panel's seven. The class is deliberately not split: the argument for letting the host's
  own heading scale win is the same on both sides, and a second name would be a second thing for
  every host to style. Runa is untouched — this is only about what the component does inside a
  host's pages. (Fhi.Metadata-gvtt9)
- **The kilde, datasamling and variable ingress paragraphs no longer carry a colour of ours.**
  `munin-explorer-kilde__description`, `munin-explorer-datasamling__description` and
  `munin-explorer-whole__description` share one rule, and it set a grey the host's own `ingress`
  class never asks for: `ingress` is styled only inside particular page types and none of those
  rules set a colour. Each paragraph inherits the body colour now. Spacing and the `65ch` measure
  are unchanged. (Fhi.Metadata-gvtt9)
- **Nothing in this repository can see Stiler, so green CI here is not evidence of the result on
  helsedata.no.** The two checks read the sample stylesheet and the capture of helsedata's live
  page, and neither is Stiler. Until the same five declarations come out there, a host on Stiler
  still gets the eyebrow — which is why this change alone does not finish the bead, and the
  matching `Fhi.Helsedata.Stiler` edit has to land before it can be closed. Note too that the
  samples' `headline-xxs` is a stand-in at 14px/600 while the real class measures 16px/400, so the
  samples under-state how much the heading grows. (Fhi.Metadata-gvtt9)
- **This needs the Stiler release carrying `.munin-explorer-filters li > label`** (Stiler PR 39039).
  Before it, Stiler dressed a facet checkbox only under `.munin-explorer-filters__facets`, which is
  Kelda's container and one the variable explorer has never emitted — so on an older Stiler the
  variable explorer's values render as an unspaced inline checkbox with no wrapping control. Measured
  in a host loading `main.css` and nothing else.
- The variable explorer's facet values no longer wear `hd-button-square`, `button-square--secondary`
  or `button-square--ghost`, and no longer carry `aria-pressed`. They emit no class at all: a bare
  `<input type="checkbox">` inside its own `<label>`, inside the `<li>` that was already there. No
  `munin-explorer` name is added or removed, so a host that styles the panel by that handle needs no
  change — but a rule scoped to a *button* inside `.munin-explorer-filters` now reaches only the
  toolbar. Both sample hosts style `.munin-explorer-filters li > label` instead, which is the
  selector Stiler now uses too. (Fhi.Metadata-j0a2h)
- **Eight class names to style if you are not on Stiler.** `munin-explorer-datasamling` wraps the
  view; `munin-explorer-datasamling__header`, `munin-explorer-datasamling__identifiers` and
  `munin-explorer-datasamling__description` are the name block; `munin-explorer-datasamling__body`,
  `munin-explorer-datasamling__main` and `munin-explorer-datasamling__aside` are the main column and
  the sidebar; `munin-explorer-datasamling__criteria` is the inclusion-criteria paragraph. Seven of
  them want exactly what the kilde view and the variable view already want, so both sample hosts
  carry all three prefixes on one rule rather than writing the layout out three times.
  (Fhi.Metadata-jgfum)
- **All eight are handles rather than names carrying meaning nothing else carries.** A host that
  supplies no rule gets the sidebar stacked under the main column and prose at full window width —
  look, not information, and nothing that misreports a state.
- **These are not in Stiler yet.** Nothing in this repository can see `Fhi.Helsedata.Stiler`, so
  green CI here is not evidence the view is styled on helsedata.no. The rules have to land in
  Stiler under `components/munin-explorer/` the way the rest of the prefix did.
- **Two more sample stand-ins drew at their widest size on every screen.**
  `datasourcecard__heading` was pinned flat at 21px where helsedata.no lets it follow the stepped
  base (21 / 18 / 16), and `datasourcecard__info` was flat at 16px where the real class steps
  16 / 14 / 13. The heading now takes the base like production does and keeps only the weight
  substitution; the info line gets the same two breakpoints as `caption`, which it matches at
  every width.
- **`munin-explorer-meta__language` is a new name to style.** It is the language's name beside a
  catalogue value that is held in more than one, and it is drawn only in that case. An undrawn one
  costs look and not information: the element is a `<p>`, so a host with no rule still gets each
  language on its own line above its value. Both sample stylesheets carry a rule — a quieter,
  uppercase label — and a host that wants the same should scope it to its own component root.
- **This is not in `Fhi.Helsedata.Stiler` yet.** Nothing in this repository can see Stiler, so a
  green build here is not evidence the marker is styled on helsedata.no. Until a rule lands there,
  a host on Stiler sees the language names at body size rather than as labels; the languages are
  still separated and still correct.
- **`button-square--ghost-blue` is a new name to style**, and it is Stiler's own rather than one
  this package invented. A host on Stiler already has it; a host with a stylesheet of its own draws
  the drilldown's way back without the variant's colour until it adds a rule — the element still
  carries `hd-button-square`, so whatever base button rules the host has keep applying. Both sample
  stylesheets carry it. (Fhi.Metadata-l9l2n.34)
- **The ghost buttons that stay ghosts want a border, and both sample stylesheets now draw one:**
  `1px solid` at `--grey60` scoped to the component's root section, which is 6.76:1 on the page
  ground and clears WCAG 1.4.11's 3:1 for a border that identifies a control. The shared
  `.button-square--ghost` is deliberately untouched — it is helsedata's shape and is used far
  outside Munin.
- **This is not in Stiler yet.** Nothing in this repository can see `Fhi.Helsedata.Stiler`, so green
  CI here is not evidence the buttons are bordered on helsedata.no. The matching rule is open as PR
  39031 in that repo; until it merges, a host on Stiler still sees the old borderless ghost.
- **`munin-explorer-filters__toolbar` is a new name to style**, on the container now holding the
  filter panel's Utvid alle, Skjul alle and Nivålinjer. The three buttons no longer carry
  `margin-right` or `margin-bottom` of their own, so a host that defines nothing for the name gets
  them back in plain inline flow with only word spacing between them. Both sample stylesheets carry
  the rule — `display: flex` with a `gap`, and buttons that shrink and wrap their own labels rather
  than the row breaking apart at a longer translation. (Fhi.Metadata-l9l2n.37)
- **This needs the Stiler release carrying `.munin-explorer-filters__toolbar`** (Stiler PR 39046).
  That PR must ship before this package's, or a host on Stiler gets the three buttons with no
  spacing between them until it does. The rule is inert on a Stiler that has it before the package
  draws the container.
- **Two new class names, both in the saved-list view's new "Ønskede data" column.**
  `munin-explorer-dataitem-header__desiredData` on the column header and
  `munin-explorer-dataitem-main__desiredData` on the cell, which is the one cell in the component
  that holds an editable field rather than a value. `Fhi.Helsedata.Stiler` carries no rule for
  either yet, and this repository's CI cannot see Stiler, so a green build here is not evidence
  the column is drawn. (Fhi.Metadata-m74i4)
- **What an undefined pair costs, and what the rule owes.** Undrawn, the field is a browser-default
  text box: visible, operable and named, so nothing is lost but the column's width and the mark on
  a refused text. Two things the rule does owe when it is written. The field's own border is what
  says a field is there, which makes it a non-text control indicator under WCAG 1.4.11 and owes
  3:1 against whatever the row sits on — `--grey30`, which every other border in the sample
  stylesheet uses, measures 1.16:1 and is invisible on a bright desktop, so both samples use
  `--grey60`. And `input[aria-invalid="true"]` is the state marking the row the API refused; the
  samples thicken the border as well as colouring it, because 1.4.1 does not accept a hue as the
  whole signal. The refusal is a sentence in the component's alert region either way, so a host
  that draws neither loses the mark and not the reason.
- **The sample stand-in for `munin-explorer-kilde__delkilde-description` was a pixel larger than the
  value it is meant to match.** The samples declared 15px (`0.9375rem`) and now declare 14px
  (`0.875rem`) — the `$font-s` step Fhi.Metadata-p4j8r records for this class in Stiler, one below
  the `headline-xxs` the delkilde name above it wears. CI here cannot read Stiler, so this is an
  intended match recorded from the bead rather than a value verified against that stylesheet.
- The page-size buttons now carry `aria-disabled` while a fetch is running, so the existing
  `.munin-explorer-pagination-content [aria-disabled="true"]` rule draws them inert for that
  moment. A host that styles the pager already has this and owes nothing new; one that does not
  will show a control that is inert to the keyboard and to a screen reader but undrawn, the same
  gap the pager's own buttons have without that rule. (Fhi.Metadata-phgeg)
- **The sample hosts drew Stiler's `headline-3`, `form-element__label` and `caption` at their
  desktop base size on every screen.** Stiler steps all three down at 2881px and again at 767px;
  the stand-ins carried only the base, so the samples rendered them 32px/21px/16px throughout
  where helsedata.no renders 28px/18px/14px on an ordinary desktop and 24px/16px/13px below
  768px. `headline-3` is what the component pins its own title with, so a host checking heading
  hierarchy in a sample was checking a title up to 8px too large. A host taking its rules from
  the samples should take both breakpoints with them.
- **The sample hosts rendered every unstyled string at 16px, where helsedata.no's inherited base
  steps 21px / 18px / 16px.** Stiler sets the base on `body` and steps it at 2881px and 767px;
  the samples carried only the smallest value, flat, so anything that does not declare its own
  size read 2px small on an ordinary desktop and 5px small above 2881px. The root font-size is
  untouched, so `rem` values are unaffected — only the inherited size moves. A host reading the
  samples for its own base rule should take the two breakpoints with it.
- **Shareable search links now need no host code at all.** `VariableExplorer` and
  `KildeExplorerWithUrlState` read the query the page was opened with and write it back themselves,
  so the wrapper component, the parsing and the `history.replaceState` a host used to copy out of
  our samples are gone. Both mount at an **interactive render mode only** — `render-mode="Server"`,
  never `ServerPrerendered` — and now **throw on initialisation** rather than rendering a page whose
  URL silently never follows the view. `KildeExplorerWithUrlState` takes `VariableExplorerPath`
  instead of a handover callback, because a delegate from a statically rendered parent arrives
  empty; it is relative to your application, so a path base survives it.
  <br><br>
  Your own parameters survive: each component rewrites only the keys it owns and carries everything
  else through untouched, `?utm_source=` included. `DeclinedKeys` keeps one of ours as well.
- **`ExplorerUrlState.QueryKeys` now names the filter's parameters too.** It listed only `search`,
  `sort`, `sortDir`, `page` and `pageSize`, while `ToQueryString` also writes `kildeIds` and the
  other facets — so a host using it to tell our parameters from its own kept those as its own and
  wrote them a second time. `ExplorerUrlState.ScalarQueryKeys` is the old five, and the set a
  component will let you decline.

## 0.1.0-alpha.6 — 2026-08-28

### Added

- **`IMuninExplorerClient` now carries the signed-in user's variable lists** - seven methods over
  `api/explorer/my/lists`: `GetMyListsAsync`, `CreateMyListAsync`, `RenameMyListAsync`,
  `DeleteMyListAsync`, `GetMyListVariablesAsync`, `AddVariablesToMyListAsync` and
  `RemoveVariablesFromMyListAsync`, with the `VariableList` and `VariableListItem` contracts they
  answer in. Ported from Runa's own client, so the routes, the verbs and the wire names are the
  ones the API already serves. (Fhi.Metadata-1cxfm)
- **These are the first calls in the package that need a token.** The whole of `my/lists` is behind
  the API's authenticated explorer policy, so a host registers its `IMuninExplorerTokenProvider`
  *before* `AddMuninExplorer` or every one of them answers 401 - which is thrown, not read as an
  empty list, because a host that believes it wired up sign-in has a fault rather than a user with
  nothing saved. The seam itself is unchanged: `BearerTokenHandler` attaches the token, and the
  anonymous default still wins when nothing is registered.
- **A batch of more than 2000 ids is refused before it is sent**, with a message naming the ceiling
  and what to do instead, rather than sent and answered with a `400` whose explanation
  `EnsureSuccessStatusCode` discards. `IMuninExplorerClient.MaxVariablesPerBatch` is that ceiling,
  and splitting is left to the caller on purpose: a client-side split turns one call that either
  happened or did not into several that may have half happened, with nothing in the return value to
  say which. It is a `static readonly` field rather than a `const` so that the number a host chunks
  by is the one in the package it restored: a const literal is copied into the host's own assembly
  when it compiles, and would go on saying 2000 after an upgrade that said otherwise.
- **A list that is not the caller's answers `false`, or `null` for the paged read.** The API cannot
  distinguish a list deleted in another tab from somebody else's and deliberately does not try -
  both are `404`, so that a caller cannot probe for which list ids exist. That is the same
  not-a-fault the read endpoints already map to `null`.
- **A row can be saved to the reader's variable list, and taken out again** - one control in two
  states beside the variable name, carrying `aria-pressed` so a screen reader is told the same fact
  the word shows. Signed out there is no button at all rather than a disabled one: a control that
  can never do anything is worse than no control, and the state holder would refuse the call anyway.
  (Fhi.Metadata-4uwh3)
- **Whether a variable is saved is read from the circuit's state holder on every render, never
  remembered by the row.** The results are rebuilt whenever the facet counts change, so a button
  that kept its own answer would forget it at the next refiltering and then show "Lagre i liste" for
  a variable that is in the list.
- **A reader who has no list yet gets one when they first save**, named "Min variabelliste". That is
  helsedata's 118497, and it is the same action as 118721 rather than a separate one: refusing to
  save until the reader had made a list somewhere else would make the button lie about what it does.
- **The button wears Stiler's `hd-button-square` and no `munin-explorer-*` name of its own.** The
  package ships no CSS, so a name invented here would be one with no rule behind it — it would
  render unstyled in the host until somebody wrote the rule in Stiler. The class-name guard is
  asserted with the button in both of its states.
- **`KildeExplorer` can hand a selection of kilder to the variable explorer.** A checkbox column,
  a velg-alle over the rows the reader can see, a `{n} kilder valgt` line and a *Nullstill utvalg*
  beside it — Kelda's own workflow, in the component. The new
  `ExploreVariablesRequested` (`EventCallback<IReadOnlyList<Guid>>`) is how the selection leaves:
  the component has no router and no idea where you mounted a `VariableExplorer`, so it tells you
  which kilder the reader chose and you decide where that goes.
  `new VariableFilter { KildeIds = ids }.ToQueryString()` writes the query
  `VariableExplorer.Filter` already reads, which is the whole of the pairing. (Fhi.Metadata-5ghur)
- **What travels is not always what is ticked**, and the three cases are Munin's own. Ticked rows
  win outright — a ticked kilde the current search has hidden still travels. With nothing ticked
  but a search or a facet in force, the rows on screen travel instead, because most of what Kelda
  filters on has no equivalent facet on the other side. With neither, the list is empty, which
  means the whole catalogue rather than a selection of none. (Fhi.Metadata-5ghur)
- **The ticks stay in the component.** They are not a parameter and not two-way: like the search
  text and the facets, they are Kelda parity state that goes away on refresh. What is worth
  sharing is the destination the selection produces, and that is a URL you own.
  (Fhi.Metadata-5ghur)
- **The saved list can be downloaded** — Excel or CSV, with or without codebooks, from the list view.
  `IMuninExplorerClient.ExportListAsync` posts the ids to `api/explorer/lists/export` and returns the
  file the API produced. That endpoint is anonymous: the ids travel in the body, so it has no need to
  know whose list they came from. (Fhi.Metadata-7mx2s)
- **The file's name and content type come back from the API, not composed here.** CSV *with*
  codebooks is answered as a zip of two files, so a caller that built the name from the format it
  asked for would hand the reader a `.csv` their spreadsheet refuses to open.
- **The download is every id in the list, not the page on screen.** The reader asked for their list;
  a file that quietly held only the 25 rows they happened to be looking at would be wrong in a way
  nobody notices until they open it.
- **No JavaScript file ships with the package.** A download started inside a Blazor Server circuit is
  not a link click — the bytes are on the server and the reader is at the end of a WebSocket — so the
  browser's own built-ins are driven through `IJSRuntime`: a `Blob` is built, an object URL minted, a
  synthetic anchor clicked, and the URL revoked. The packaging guard forbids a `wwwroot` because a
  stylesheet riding along would compete with the host's own; it is not a ban on interop, and the
  sample host already drives `history.replaceState` this way.
- **`ExportListAsync` carries a default body**, like `GetKildePropertyMetadataAsync` and for the same
  reader: a host that implements the contract rather than consuming `MuninExplorerClient` would
  otherwise stop building on the upgrade, and a version already on the feed cannot be taken back from
  whoever restored it. It refuses rather than answering emptily — an empty file is a worse answer
  than a clear no.
- **A refusal from the browser is said out loud.** A Content-Security-Policy without `blob:` would
  land in the catch, and the reader is told, rather than left with a button that appears to do
  nothing. A host whose Content-Security-Policy omits `blob:` will see that message.
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
- **`VariableListState` holds the signed-in reader's variable lists for the circuit** - one scoped
  service over six of the seven `my/lists` client methods, so the save action in the result list,
  the list view and the download all read and write the same copy and are told when one of them
  changes it. `GetMyListVariablesAsync` is deliberately not wrapped: it is a paged read of one
  list's contents, and paging state belongs to the surface showing it rather than to a holder shared
  by three of them.
  Scoped and never singleton: a singleton would be one reader's lists served to every circuit on the
  server. (Fhi.Metadata-jjry3)
- **`VariableExplorer` gains an `IsAuthenticated` parameter, defaulting to signed out.** Whether the
  reader is signed in is told by the host rather than discovered by calling `my/lists` and reading a
  401: probing spends a failed request per render on every signed-out reader, and cannot tell "no
  session" from "expired token" or "Munin is down". The default matters as much as the mechanism - a
  host that forgets the parameter loses saved lists, which somebody notices, where the other default
  would send unauthorised calls on every render, which nobody does.
- **Signed out, not one call reaches `my/lists`** - the guard sits in the holder rather than at each
  call site, so a surface added later cannot forget it. The test asserts on the number of calls that
  reached a counting client, not on what the page shows, because an implementation that calls and
  swallows the 401 looks identical on screen.
- **The holder is resolved with `GetService`, not `[Inject]`**, so a host that renders the explorer
  without calling `AddMuninExplorer` still gets an explorer and merely loses saved lists - the same
  tolerance the package already extends to a host with no localisation services registered.
- The reader can choose how many rows a page holds. Three buttons beside the pager — 10, 20 and
  50, Runa's own values, so the two explorers behave alike for the same person — with a matching
  `PageSizeChanged` callback in the same shape as `PageChanged`. `PageSize` is therefore two-way
  now: a host that mirrors it into its URL keeps the choice on a shared link, and one that ignores
  the callback still gets a working control and loses the choice on reload.
- Choosing a size returns the reader to page 1 and raises `PageChanged` with it. A change of size
  renumbers the rows, so keeping the page number would leave someone on page 3 of 15 looking at an
  arbitrary part of the result without anything on screen saying they had been moved. Sizes outside
  1–100 are still clamped rather than refused, and the control reads through the same clamp.
- A failed size change can be retried like any other failed request, and the retry sends the size
  the reader asked for rather than the one the rollback restored. Without that it would refetch the
  old size, succeed and clear the error, reporting a change that never happened — from the one
  control a reader cannot press again once a single-page result has taken the pager away.
- **`VariableListItem` carries the display fields the API resolves for it** - code, name, kilde and
  its short name, datasamling, variabelgruppe, datatype, data period and version status, alongside
  the id and the time it was added. Munin began sending these with `Fhi.Metadata-kejyv`; the
  contract here was written before that and read none of them, so a saved list could be drawn with
  an id and a date. (Fhi.Metadata-vdtcv)
- **They are all optional and may be null together**, which means that id has no row in the read
  model — retracted, unpublished, or not yet projected. Such an entry is still returned rather than
  dropped, so a list of 247 does not answer with fewer than it counted, and the caller decides what
  to draw for it.
- **The wire keeps the Norwegian stem** — `variabelCode`, `variabelName`, beside the `variabelId`
  this contract already spelled out. Every field carries an explicit `[JsonPropertyName]`: the
  package deserialises with `JsonSerializerDefaults.Web`, whose camelCase mapping would look for
  `variableName` and quietly find nothing, which reads on screen as an empty list rather than as
  names that did not arrive.
- **Version status is a string, not an enum**, the same way `VariableSummary.VersionStatus` is —
  `JsonSerializerDefaults.Web` carries no string-enum converter, so an enum would need one
  registered by every host.

### Changed

- **`KildeExplorer`'s table shows the columns Munin's own Kelda shows by default** - Navn,
  Kildetype, Status, Datasamlinger, Variabler and Opprettet, in Kelda's order. Dataansvarlig,
  Databehandler and Delkilder are gone from it: Kelda keeps all three behind its column picker,
  off by default, and a reader comparing the two side by side was looking at two different
  tables. (Fhi.Metadata-bc4x1)
- **Opprettet is the kilde's founding year, not when Munin registered it** - it comes from
  `KildeSummary.AdditionalProperties["Opprettet"]` and is shown exactly as the catalogue wrote
  it, since the source holds values like `2916`, `1900` and `0` that a date formatter would
  blank or misread. `KildeSummary.Created` is the other fact - Munin's own row timestamp, which
  Kelda draws as Importert and keeps off by default - and no column is bound to it.
  (Fhi.Metadata-bc4x1)
- **`VariableExplorer.PageSize` now defaults to 20, not 25.** A host that never set the parameter
  will show 20 rows a page where it showed 25, and should set `PageSize="25"` if the old size
  matters to it. The default has to be one of the sizes the new control offers: left at 25 it would
  have drawn three buttons with none of them pressed on first load, which is truthful — no size the
  reader can choose is in force — and reads as broken. 20 is the middle of the three and Runa's own
  starting size, so the two explorers now open the same way for the same person.
- `IMuninExplorerClient.SearchVariablesAsync` still defaults `pageSize` to 25, which is Munin's own
  API default and unrelated to what the component asks for. Only the component's default moved.
  (Fhi.Metadata-nd2q3)
- **`IMuninExplorerClient` gained `GetKildePropertyMetadataAsync`** - the vocabulary behind the
  curated properties the kilde list carries, over `api/explorer/kilder/egenskaper`, as the same
  `PropertyMetadataEntry` list the detail endpoints ship with a record. It is a sibling of the list
  rather than a field on it because the vocabulary is one definition per property and not one per
  kilde. Not breaking: it is the one member on the interface with a default implementation, which
  answers an empty list, so a host that implements the interface itself keeps compiling and its
  kategori and tilgangsnivå facets show the catalogue's own tokens instead of words - the same
  degradation as the endpoint being unreachable. Overriding it is what turns those tokens back into
  words. It takes no language, deliberately, since the entries carry every label in `OptionsJson`
  and the caller picks per render. (Fhi.Metadata-tbpbr)
- **The datasamling section's default heading now follows the source rather than the explorer.**
  A kilde with delkilder is headed "Delkilder og datasamlinger" ("Sub-sources and data collections");
  one without keeps "Datasamlinger". It followed the explorer before — Runa said "Datasamlinger" and
  Kelda said "Delkilder og datasamlinger" over identical rows — which was a difference of one word
  over one flat table. It is not that any more: the section draws the delkilder themselves, so on a
  study series the Runa wording headed five waves and promised none of them. Which word is right is
  a question about the source, not about who is rendering it. `DataCollectionsHeading` still wins
  when a host sets it. A host that relied on the default reading "Datasamlinger" over a kilde with
  delkilder should pass the parameter. (Fhi.Metadata-wtz80)
- **A kilde's datasamlinger are now shown under the delkilde each belongs to.** `KildeView` — the
  view both `VariableExplorer` and `KildeExplorer` open a source with — drew one flat table of every
  datasamling the source holds, gathered through the delkilder and then sorted as if they were one
  list. It now draws the source's own in that table and then a nested `<ul>`, one item per delkilde,
  each carrying its own datasamlinger and any delkilder below it, walked to whatever depth the
  catalogue nests them. For a study series this is the difference between what the source holds and
  how it is arranged: Tromsø's fourteen datasamlinger are three of the study's own and eleven spread
  over five waves, and the waves are the study's organising fact. (Fhi.Metadata-wtz80)
- **A kilde with no delkilder is unchanged** — one table, same columns, same order. That is most
  kilder, and the section a host has already styled. (Fhi.Metadata-wtz80)
- **Each delkilde's name is a heading one level below the section's**, and one level deeper again
  for each level of the tree, flattening at `h6`. A host that sets `HeadingLevel` to keep its page's
  outline unbroken gets the tree in the outline too. (Fhi.Metadata-wtz80)

### Fixed

- **The variable result list is a table to a screen reader** - it drew seven columns under a header
  row and told assistive technology nothing about any of it, so a reader got a flat run of text with
  no way to hear which column a value was in or to move by column. The rows, the header cells and
  the columns now carry `table`, `rowgroup`, `row`, `columnheader`, `rowheader` and `cell` roles,
  and the sorted column's `aria-sort` finally sits on a role that may carry it — it was on a
  roleless `<div>`, which is invalid ARIA, so the sort state was announced to nobody. Visual layout
  is unchanged: the roles go on the elements that were already there, and the two boxes that only
  lay the columns out step out of the accessibility tree instead. WCAG 2.1 AA, 1.3.1 and 4.1.2.
  (Fhi.Metadata-3b1l4)
- **The saved-list view got the same treatment** - it shares the result list's markup and had the
  same missing structure, which no automated check reports because absent structure is not a rule
  violation. (Fhi.Metadata-3b1l4)
- **"Hopp til paginering" moved above the result table** - it used to sit between the header row and
  the rows, which is inside the table now, and a table may own nothing but rows. It is still beside
  the list it skips and still invisible until focused. (Fhi.Metadata-3b1l4)
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
- **The field is now `<input type="text" enterkeyhint="search">` with a clear control of the
  package's own.** No user-agent ✕ to mislead, a soft keyboard still offers a search key, and one
  press restores the whole list — in the variable explorer that runs the search again with no term,
  so the API, the facet counts and `SearchChanged` all follow. Neither
  clear touches the facets or the filter: a reader who narrowed twice asked for both, and one
  control must not quietly undo the other. Where that control sits, and when it is drawn, was
  settled after this entry was written and before either shipped — see `Fhi.Metadata-ag4n7`.
  (Fhi.Metadata-5ghur)
- **Kelda's handover button says what it is about to carry.** One button, three payloads — so three
  wordings, read off the same two questions the payload is, which is what keeps the label and the
  ids from disagreeing. *Utforsk variabler for utvalget* with rows ticked, *Utforsk variabler for
  treffene* when a search or a facet is narrowing and nothing is ticked, and *Utforsk alle
  variabler* on an untouched list. Munin's Kelda writes the first in all three cases; the behaviour
  here is identical and only the sentence differs. (Fhi.Metadata-5ghur)
- **The new-list name field now has an accessible name** - it carried only a placeholder, so a
  screen reader announced an unnamed edit field and the hint vanished the moment the reader started
  typing. It has a visible `<label>` tied to it with `for`/`id` instead. WCAG 2.1 AA, 4.1.2 and
  3.3.2. (Fhi.Metadata-6vbwa)
- **The save and remove buttons say which variable they act on** - a page of results was 25 buttons
  all announcing "Lagre i liste", and a saved list of forty was forty announcing "Fjern", with
  nothing to say which row a screen reader user was standing on. Each is now named from two
  elements — its own words, then the row's name cell — so the words stay in the reader's language
  while the variable's name stays Norwegian and marked as such, which a single `aria-label` string
  could not do. The words on the button are unchanged and come first, so speech input still reaches
  them. A row whose variable has left the catalogue borrows the sentence its name cell shows.
  (Fhi.Metadata-6vbwa)
- **A variable with no name still announces what its row opens** - the variable's own name is the
  button that opens its panel, so a variable the catalogue gives no preferred term for left that
  button with no content and no accessible name at all: a screen reader announced "button,
  collapsed" and nothing else. It now falls back to "Vis hele variabelen" in that one shape, and
  keeps announcing the variable's name in every other. WCAG 2.1 AA, 4.1.2. (Fhi.Metadata-6vbwa)
- **Downloading a variable list never worked** - `ExportListAsync` sent `format` as `"Csv"`/`"Xlsx"`,
  but the API spells those members `[JsonStringEnumMemberName("csv"/"xlsx")]` and answers PascalCase
  with a 400, so every download ended in the failure message. Now sends the name the API accepts.
  (Fhi.Metadata-7mx2s)
- **The list showed the raw datatype code where the explorer showed its name** - a variable saved as
  datatype `2` rendered as `2` in `VariableListView`, next to a `VariableExplorer` calling the same
  variable `Heltall` on the same page. The list now reads the names from the API the same way the
  explorer does, and still falls back to the code when the API has no name for it.
  (Fhi.Metadata-ffjtx)
- **The statistics table survives a statistic whose properties arrive as null** - a variable whose
  payload carries an explicit `"additionalProperties": null` on one of its statistics took the view
  down while rendering: the table read that bag straight off the contract, where the non-nullable
  declaration and its initialiser promise something `System.Text.Json` does not keep for an explicit
  null. It reads it as the empty bag it means now, so the row draws the same dash a statistic with
  no numbers already drew. This is the one read the guard added for the kilde detail view did not
  cover, because the statistics table does not go through the shared property rows.
  (Fhi.Metadata-hox1c)
- **And the client now keeps that promise for every collection on every contract** - the same
  explicit null lands the same way in any of them, and the two fixes so far each closed only the
  read the payload happened to reach. The client's serialiser reads a null where a collection is due
  as the empty collection, so `AdditionalProperties`, `PropertyMetadata`, the translation bags and
  every list beside them are non-null because the deserialiser makes them so rather than because a
  property initialiser was hoped to. A host substituting its own `IMuninExplorerClient` deserialises
  with its own options, so the components still coalesce a null bag where they read one.
  (Fhi.Metadata-hox1c)
- **A throttled reader is told they asked too often, not that the catalogue is down** - the API
  answers 429 with a `Retry-After` when too many requests arrive from one address, and the client
  used to throw that as the same generic `HttpRequestException` as a 500 or a timeout. So a reader
  who hit the limit was advised to try again shortly, which is the one thing that cannot help. The
  client now raises `MuninExplorerRateLimitedException`, carrying the wait the API asked for in
  either form the header takes, and the result list, the facet panel, the kilde list, the kilde view
  and the row's save button each say so in their own place, in both languages. The reads and the
  writes both raise it: a save refused by the limiter used to read as "could not save", and a list
  the reader still has is not a list they have lost. The wait is carried for a host that
  logs it and never rendered: a countdown against a window shared with every other reader is a
  promise this package cannot keep. Nothing retries by itself — helsedata's cluster reaches Munin
  as one address, so components retrying on a shared `Retry-After` would rebuild the burst that
  caused the 429. A 429 is also deliberately not mapped to "no hits" the way a 404 is: a search
  that was never run must not come back as a search that found nothing. (Fhi.Metadata-l9l2n.30)
- **A host substituting its own `IMuninExplorerClient` has to throw it too** - every non-2xx used
  to reach the components as `HttpRequestException`, so an implementation that wrapped its own
  `HttpClient` needed nothing beyond `EnsureSuccessStatusCode`. A 429 is now its own type in
  `Fhi.Munin.Explorer.Contracts`, and the rule the components rely on is stated on
  `IMuninExplorerClient`: it must not come back as null, as an empty collection, as `false` from
  one of the writes, or as a retry of the implementation's own. Catching around the client changes
  the same way - `MuninExplorerRateLimitedException` does not derive from `HttpRequestException`,
  so a host that catches the latter to log or to swallow will no longer see a throttled call.
  (Fhi.Metadata-l9l2n.30)
- **A refused list read no longer leaves the save buttons permanently wrong, or takes the page
  down** - reading which variables are in the reader's list happens once when the component mounts,
  alongside the search and the facet refresh, which is the burst the limiter counts. That read
  escaping a Blazor lifecycle method tore down the circuit - in a legacy Blazor Server host, the
  whole page rather than this component. It is now caught, and the read is tried again on the
  reader's next save rather than abandoned for the life of the circuit, so that press puts every
  other row's label right as well. Without it, "wait and try again" repaired the save and nothing
  else. Only a press retries: rendering does not, because the component reads this on every
  parameter set, and a membership read alongside every search and page turn would rebuild the
  burst that earned the 429. The press itself is decided from the row as the reader saw it, so a
  variable already in the list - drawn as "save" because the read was refused - is added rather
  than deleted when the repair arrives mid-press, and a repair that is refused again no longer
  costs the reader the save they asked for. Overlapping asks now join the read already running
  instead of each sending their own, and a read publishes its pages only once it has walked them
  all, so a walk that is refused partway through leaves no half-read list behind.
  (Fhi.Metadata-l9l2n.30)
- **A failed search now offers a way out instead of only a sentence.** Both failures reported in
  the explorer's alert region — the result list and the filter counts — gain a retry button of
  their own, inside that region, so a reader no longer has to reload the host's page to get past
  one. The button re-sends the request that failed: the page they were turning to, the ordering
  they asked for, the filter they picked and the query the rows came from, rather than a fresh
  search from whatever is in the box. A retried search or filter change brings the facet counts
  back into agreement with the rows it fetched — including after a failed first load, which leaves
  the filter panel off the page entirely until they arrive — while a retried page turn or sort
  leaves them alone, because neither moves them. The host is told what actually moved and nothing
  else, so a retried page turn does not push three spurious history entries at a host that mirrors
  each callback into a URL. None is offered on a 429, where the sentence beside it says to wait;
  and once there is nothing left to retry the button stays where it is, inert, so it cannot take a
  keyboard user's focus with it — until the next fetch started elsewhere settles, answered or
  throttled, which is when a dead offer would otherwise start being announced beside every later
  failure in that atomic region. The labels are in both languages and follow the `Language` parameter. (Fhi.Metadata-p9c76)
- **Kelda's kategori and tilgangsnivå facets read the catalogue's own vocabulary** - the words on
  those checkboxes were transcribed into the package, which made them right on the day they were
  written and out of date from then on: a category the catalogue added afterwards showed as
  `ehds-cat:` in the facet while the kilde view one click away showed its Norwegian word, from the
  live vocabulary the API sends with a kilde. `KildeExplorer` now fetches that same vocabulary
  beside the list and both surfaces read it, so a value the catalogue adds is a word in the panel
  the day it is added. The transcribed table is gone. (Fhi.Metadata-tbpbr)
- **A token is matched whole rather than from the last colon on** - the transcribed table was keyed
  on the bare token, so `annet-vokabular:biobanks` read as "Biobanker" in the facet and as itself in
  the detail panel. One value, two labels, depending on which screen the reader was on. Two prefixes
  over one bare token are two values in the catalogue, and both surfaces now say so.
  (Fhi.Metadata-tbpbr)
- **A value the vocabulary does not list is unchanged: it keeps its checkbox, its count and its
  token**, unmarked by `lang` because a CURIE is prose in no language. So is what happens when the
  vocabulary cannot be fetched at all - the facets fall back to the catalogue's tokens and the list
  itself is unaffected, since the two are separate calls that fail apart. (Fhi.Metadata-tbpbr)
- **Nothing on screen waits for that vocabulary** - it is fetched beside the list rather than before
  it, and awaited after both the list and, when the host mounts with a kilde already chosen, that
  kilde's own fetch. A slow or undeployed `api/explorer/kilder/egenskaper` therefore costs the two
  facets their words until it lands and nothing else: not the list, held behind "Laster kilder …",
  and not a kilde deep-linked from the host's URL, whose request would otherwise not have been made
  yet. (Fhi.Metadata-tbpbr)
- A kilde whose payload carries an explicit `"additionalProperties": null` no longer takes the
  detail view down. The curated property rows — `KildeView`'s, `VariableView`'s and the variable
  panel's — all read a bag declared non-nullable with an initialiser that `System.Text.Json`
  overwrites with null, and the resulting `NullReferenceException` was thrown while rendering — past
  the point where a host could report it as a failed load. Null is now read as "no curated
  properties", the same answer the kilde list already gave. Reads that do not go through those rows
  are their own fix: `VariableView`'s statistics table is `Fhi.Metadata-hox1c`.

### Notes for hosts

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
- **Wire `ExploreVariablesRequested` or you get no selection column.** The checkbox column, the
  count and both buttons are drawn only when that callback has a delegate, because the ticks exist
  to reach a page only you can name — a column over a button that leads nowhere would cost the
  reader the work of choosing before telling them there was nothing to choose for.
  (Fhi.Metadata-5ghur)
- **Create that callback inside an interactive component, not in a static parent.** An
  `EventCallback` does not survive being passed from a statically-rendered parent into an
  interactive island: Blazor rejects a bare delegate parameter, but `EventCallback` is a struct,
  so it is serialised as `{"HasDelegate":true}` and read back inside the circuit as empty. Putting
  `@rendermode` on the `KildeExplorer` tag does **not** fix this — that makes the mount point
  interactive while the parent creating the callback stays static. Mount the component inside a
  small wrapper component that the host renders interactively, and put the handler there; both
  sample hosts now do exactly that, and it is the arrangement helsedata's Optimizely host already
  uses. Get it wrong and the selection column is simply absent, with nothing to say why. The same
  applies to `SelectedKildeIdChanged` and to every `EventCallback` on `VariableExplorer`, where
  it has no visible symptom at all. (Fhi.Metadata-5ghur)
- **One class name to style if you are not on Stiler**: `munin-explorer-kilder__select`, on the
  checkbox column's header cell and on every row's. The declaration it needs is a **width** — a
  table shares itself out between its columns, so one holding a single checkbox otherwise takes
  the same share as Dataansvarlig and squeezes the eight columns that carry words. Both sample
  hosts' `host.css` carries `width: 1%` for it, right after the kilde list's count rule; a test in
  this repository asserts that the rule is a width and not merely a rule. The boxes themselves
  wear no class — a bare `<input type="checkbox">` is an element every stylesheet already dresses,
  the same call the facet panel makes. (Fhi.Metadata-5ghur)
- The size control adds one class name a host has to provide, `munin-explorer-pagination-size`, and
  it is the group's layout only — a host without the rule still gets a working control drawn in the
  flow beside the pager. Which size is in force is *not* drawn from that name, nor from a rule on
  `aria-pressed`: the button for the size in force wears Stiler's `button-square--secondary` and the
  other two wear `button-square--ghost`, the same filled-and-ghost pair the facet values and the
  sort buttons already use. So a host with Stiler and nothing else shows the current size correctly
  without owing this package a stylesheet, which is the whole reason the state is carried by a class
  swap rather than by an attribute selector.
- The three buttons carry Stiler's own `margin-right`, which is what keeps them apart: Razor drops
  the whitespace between elements, so without it they would touch. Both sample hosts style
  `munin-explorer-pagination-size` as a flex row and give the group's label a right margin.
- Deliberately not a `<select>`, although Runa's own control is one and it would have been less
  markup. No class name for a select can be read back off Stiler — helsedata's pager has no size
  control, so there is nothing to copy and anything chosen would be invented, and an unstyled select
  inside an otherwise styled page is the failure this package exists to avoid. Deliberately not a
  `radiogroup` either: that role's single tab stop and arrow-key navigation need script, and this
  package ships none. (Fhi.Metadata-nd2q3)
- **The datasamlinger table needs two column widths.** Without them `table-layout: auto` hands the
  width to the description — it is catalogue free text and always the longest — and Gyldighet and
  Antall variabler wrap in every row. Both sample hosts' `host.css` now set `width: 24%` on the
  third column and `width: 1%` with `white-space: nowrap` on the fourth body cell, and
  Fhi.Helsedata.Stiler carries the same two rules in
  `Static/scss/components/munin-explorer/_trail.scss`. The name column is deliberately left to wrap.
  (Fhi.Metadata-oq40w)
- **The retry buttons need a rule for `munin-explorer-retry`.** Their enabled look is
  `hd-button-square button-square--ghost`, which `Fhi.Helsedata.Stiler` already defines; their
  inert look is not covered by anything it ships. They are never `disabled` — that would drop
  focus to `<body>` at the moment they stop being useful — so `aria-disabled` is what says so, and
  the pager and the filter panel both draw that state from rules scoped to their own containers.
  The alert region these sit in deliberately carries no class, so neither rule reaches in, and
  without one of its own a button that does nothing looks exactly like one that works — which is a
  WCAG 2.1 AA problem rather than a cosmetic one. Both sample hosts' `host.css` carries the rule,
  but a sample rule only styles the samples: Stiler needs the same under
  `components/munin-explorer/`, and carries none as of 0.1.14. Tracked as `Fhi.Metadata-x6vqc`, and
  listed in README beside the other names a host has to draw itself. (Fhi.Metadata-p9c76)
- **Three class names to style if you are not on Stiler.** The delkilde tree emits
  `munin-explorer-kilde__delkilder` for the list, `munin-explorer-kilde__delkilde` for each item and
  `munin-explorer-kilde__delkilde-name` for the name heading. They are handles rather than names
  carrying meaning nothing else carries: the shape underneath is a real nested `<ul>`/`<li>`, so a
  host that supplies no rule for any of them still gets a list a browser indents by itself and a
  screen reader reads as nested. Both sample hosts' `host.css` carries rules for all three, right
  after the kilde view's own block. The delkilde's code line reuses
  `munin-explorer-kilde__identifiers`, which the kilde's own name block already emits, so there is
  no fourth name to add. (Fhi.Metadata-wtz80)
- **These three are not in Stiler yet.** Nothing in this repository can see
  `Fhi.Helsedata.Stiler` — the CI here checks the sample stylesheet and helsedata's captured class
  names, neither of which is Stiler — so green CI on this change is not evidence the tree is styled
  on helsedata.no. The rule has to land in Stiler under `components/munin-explorer/` the way the
  rest of the prefix did; until it does, a Stiler-only host gets the browser's own list indentation,
  which reads as a plain nested list rather than as nothing. (Fhi.Metadata-wtz80)
- **The datasamling table needs its first column pinned now that there is one table per level.**
  Stiler already pins the third (`24%`) and fourth (`width: 1%` + `nowrap`) and leaves Navn and
  Beskrivelse to auto-layout, which is right for one table — whatever those two settle on is at
  least self-consistent. It is not right for six: auto-layout sizes each table from its own
  content, so Tromsø's first column measured 903, 1426, 270, 1409 and 1479 pixels across five
  tables, and the wave whose beskrivelse holds a wall of text squeezed the rest to slivers. Pinning
  Navn leaves Beskrivelse as the only free column, which lines every level up. Both sample hosts do
  this now; a host writing its own rule wants the same, and so does Stiler.
- **Do not indent the top level of the delkilde list.** The `<ul>` is a SIBLING of the table holding
  the kilde's own datasamlinger, and a rule that indents it claims a parent it does not have: the
  first attempt put the top-level waves 36px in, directly under the last row of that table and with
  no gap, and every reader of the page took Tromsø4 through Tromsø7 to be children of Tromsø3. The
  markup said otherwise, and nobody can see markup. Indentation is spent on depth INSIDE the tree
  only. Both sample hosts draw each delkilde as a bordered box instead, flush with the table at the
  top level, so a nested wave is inset by its parent box's own padding rather than by a rule that
  has to know how deep it is. (Fhi.Metadata-wtz80)

## 0.1.0-alpha.5 — 2026-08-26

### Added

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
- **Kelda's kilde view has the sections Runa's has not** - opening a kilde in `KildeExplorer` now
  draws Variabler, Kriterier for tilgang til data and Priser after the catalogue's metadata, beside
  the datasamling section it already headed "Delkilder og datasamlinger". They are markup in the
  explorer's own file, handed to the shared `KildeView` through its `Sections` slot, so that view
  still cannot tell which explorer is rendering it and Runa's kilde page is unchanged. A host's own
  `Sections` are placed after Kelda's rather than instead of them. (Fhi.Metadata-2fomm.2)
- **The two static blocks say one sentence each for now** - the access criteria and the prices are
  markdown with links out to helsedata.no and fhi.no in Munin's own Kelda, and whether they belong
  at all in a component embedded on helsedata.no is still open (`Fhi.Metadata-ay3zz`). Until that is
  answered each section carries a single plain sentence, because a heading with nothing under it
  reads as a rendering fault. (Fhi.Metadata-2fomm.2)
- **Kelda's kilde list has facets** - `KildeExplorer` now draws a filter panel over kildetype,
  kategori, tilgangsnivå and databehandler, with a checkbox per value and a count beside it.
  Ticking narrows the list client-side: OR within a facet, AND across them, and AND with the
  search. Everything is computed over the one list the component already fetched, so no facet
  costs a request and none of them is a server-side filter — including kildetype, which the
  endpoint would take, because two facets behaving differently is a difference a reader can feel
  and nobody can explain. The counts are therefore not cross-filtered: an option's number is how
  many kilder in the catalogue carry that value, not how many the current selection would leave.
  (Fhi.Metadata-2fomm.3)
- **A facet with no values is not drawn at all** - no heading, no empty container. Munin's own
  Kelda renders Kategori as a heading with nothing under it, which reads as a broken panel rather
  than as a field nobody has filled in; leaving the facet out makes "is the data there?" a question
  about the catalogue, which this component then answers correctly either way. (Fhi.Metadata-2fomm.3)
- **Kategori's choices read as words rather than as EHDS tokens** - the catalogue stores a kilde's
  kategori as a CURIE — `ehds-cat:registries-quality-of-healthcare` — and the panel labels them from
  the catalogue's own vocabulary: "Kvalitetsregistre", in whichever of the two languages the reader
  is reading. The same treatment tilgangsnivå gets, and for the same reason — one panel cannot be in
  two minds about whether a reader of this catalogue is expected to read EHDS. A value that
  vocabulary does not list keeps its checkbox and its count and shows its CURIE, which is unlovely
  and still filterable. The facet groups and filters on the whole token throughout, so what a choice
  is called never changes what it selects. One category is one checkbox however the catalogue wrote
  it — an array, a bare JSON string, or text that is not JSON at all — and a JSON null is no
  category rather than a checkbox named "null". (Fhi.Metadata-2fomm.3)
- **A choice drawn in the catalogue's own Norwegian is marked as being in it** - databehandler is
  free text, and kildetype falls back to Munin's own token wherever this package has no word for
  it. Those choices carry `lang`, exactly as the same strings do in the table's cells, so an
  English page does not have a Norwegian organisation's name read out with English phonetics
  (WCAG 3.1.2). A choice this package supplied the words for carries none, because a `lang` the
  text is not in is the same failure the other way round — and so does a kategori or tilgangsnivå
  the vocabularies had no word for, because what is left on screen there is an EHDS or EU CURIE,
  English-authored and prose in no language at all. (Fhi.Metadata-2fomm.3)
- **A long free-text facet value no longer decides the layout** - databehandler is free text, and
  one value on the live catalogue runs to 212 characters. The choice is cut to 60 characters on
  screen with the whole value on its `title`, and the value it filters on is untouched. Variants
  are not merged: "FHI" and "Folkehelseinstituttet" stay two choices, because deciding they are one
  organisation is a claim about the catalogue and belongs in it (`Fhi.Metadata-4kxfv`).
  (Fhi.Metadata-2fomm.3)
- **The panel folds away on a narrow screen** - a "Vis filtre" button unfolds it, using the
  browser's own `hidden` attribute so it works on a host that styles none of this; a host with room
  for a sidebar takes the folding away in one rule, which is what both sample stylesheets now do.
  Two class names are new, and what a host has to declare for them is under Notes for hosts.
  (Fhi.Metadata-2fomm.3)
- **The kilde list's empty state names the facets as well as the search** - "Ingen kilder samsvarer
  med søket «als» og filtrene som er valgt". A reader who has narrowed the list twice was being sent
  to fix the wrong one. (Fhi.Metadata-2fomm.3)

### Fixed

- **`KildeExplorer`'s open kilde no longer keeps a heading that says it is loading after the fetch
  has finished.** The heading is what the drilldown's `aria-labelledby` points at, and it fell back
  to "Henter datakilden …" whenever the list could not supply the kilde's name — which is every time
  a host passes a `SelectedKildeId` the catalogue does not publish, and any `SelectedKildeId` at all
  when the list itself failed to load. A screen reader entering the landmark was told the source was
  still loading indefinitely, while the status line underneath said the fetch had finished and found
  nothing. It now follows the load state and says the same sentence the status line does.
  (Fhi.Metadata-2fomm.1)
- **`KildeExplorer` no longer reports a finished, empty fetch on the first render of a host-named
  kilde.** The detail fetch cannot start until the list has answered — the list is what knows the
  kilde's name — so for one render the drilldown was on screen with no name, no detail and no error:
  `aria-busy="false"`, an empty status line, and a heading, the one `aria-labelledby` points at,
  reading "Fant ingen detaljer for denne datakilden." for a request that had not been made. The view
  now reads as loading from the render it first appears in. (Fhi.Metadata-2fomm.1)
- **The kilde table's Dataansvarlig and Databehandler cells are no longer marked `lang="no"` when
  they hold the package's own "Not specified".** For a host rendering the explorer with
  `Language="en"`, an empty catalogue field produced `<td lang="no">Not specified</td>`, so a screen
  reader read an English string in a Norwegian voice (WCAG 3.1.2, Language of Parts). The cell is
  marked as the catalogue's language only when it really holds the catalogue's words.
  (Fhi.Metadata-2fomm.1)

### Notes for hosts

- **Three class names to style if you are not on Stiler.** The kilde list emits
  `munin-explorer-kilder` for its table, `munin-explorer-kilder__name` for the control that opens a
  row and `munin-explorer-kilder__count` for the three columns holding a number. A host that
  supplies no rule for any of them still gets a usable list — the shapes underneath are a `<table>`
  and a `<button>`, so the columns still line up and the name is still visibly a control — which is
  why they are handles rather than names that carry meaning nothing else carries. Both sample hosts'
  `host.css` carries rules for all three, right after the kilde view's own block.
  (Fhi.Metadata-2fomm.1)
- **`KildeExplorer` mounts the way `VariableExplorer` does**, and needs the same of the component
  that mounts it: the parent creating `SelectedKildeIdChanged` must itself be interactive, because
  an `EventCallback` serialises to an empty delegate across a static-SSR to interactive-island
  boundary. Making the mount point interactive is not enough — see the note under
  Fhi.Metadata-5ghur for what that costs and how the samples arrange it. Set `HeadingLevel` to
  whatever keeps the surrounding page's outline unbroken, and `Language` to the page's own.
  (Fhi.Metadata-2fomm.1)

## 0.1.0-alpha.4 — 2026-08-24

### Fixed

- **The pager's skip link is hidden until it is focused on a host with Stiler alone.** The anchor
  that jumps past the result list to the pager wore helsedata's `skiplink-pagination`, and no
  released Stiler had a rule that hid it — so on a host outside helsedata's estate a permanently
  visible "Hopp til paginering" sat over every multi-page result list. A skip link everyone can see is not
  a skip link. It is `munin-explorer-skiplink-pagination` now, and the rule that hides it until
  `:focus` ships unscoped in `Fhi.Helsedata.Stiler` 0.1.14. Inside helsedata nothing changes: their
  `variables.css` rule for the old name is still there, now unused. (Fhi.Metadata-ja2qu)

### Notes for hosts

- **Rename the rule if you wrote one for `skiplink-pagination`.** The class is
  `munin-explorer-skiplink-pagination`, the last borrowed name the component emitted. A host that
  styled the old one keeps a rule that no longer matches anything, and the failure reads backwards
  from an ordinary missing rule: what goes missing is the rule that *hides* the link, so it turns
  up visible above every multi-page result list rather than turning up unstyled. Both sample hosts'
  `host.css` carries the renamed rule — off-screen by default, revealed in place on `:focus`, never
  `display: none`, which would take it out of the tab order too. (Fhi.Metadata-ja2qu)
- **The Stiler floor is 0.1.14 for the pager and its skip link, 0.1.13 for everything else.**
  0.1.13 shipped before both were renamed into the `munin-explorer` prefix, so on 0.1.13 the pager
  renders at browser defaults and the skip link is permanently visible rather than hidden until it
  is focused. 0.1.14 carries both under `components/munin-explorer/`, and the skip link's rule is
  unscoped there — it matches the anchor wherever in the component's markup it is rendered.
  Checked against the published 0.1.14 package on the `Fhi.Helsedata.no` feed rather than against
  Stiler's sources: `staticwebassets/css/main.css` and `main.min.css` both carry
  `.munin-explorer-skiplink-pagination`. (Fhi.Metadata-ja2qu)

## 0.1.0-alpha.3 — 2026-08-24

### Added

- **A hierarchy trail over the results** — kilde → delkilde → datasamling → variabelgruppe, drawn
  above the list whenever any of the four is filtered on. It is the only thing on screen that says
  *where* a deep selection has put the reader: the facet panel holds the same choice as pressed
  buttons in collapsed disclosures, so a kilde chosen three levels down is otherwise visible only
  as the result count changing. Each step is a button that clears every level under it, several
  values on one level read as the first name and `(+n)`, and a `×` beside the trail empties the
  whole hierarchy while leaving every other filter — datatype, kodeverk, dates — in force.
  (Fhi.Metadata-v6681)
- Two class names go with it, both a host's to draw: `variable-explorer-breadcrumb` with its
  `__clear` for the trail's own shape, and the existing `variable-explorer-crumb` for the steps,
  which is the same name the variable panel's kilde trail already uses. A host that draws neither
  gets a numbered list of buttons in the right order with the right names, which is the
  information without the shape that says "path".

### Fixed

- **A `Language` carrying a region now resolves to its language rather than falling back to
  Norwegian** - `en-GB` and `en-US` read as English, `nb-NO` as Norwegian, and the match is on the
  primary subtag throughout. helsedata's CMS reports the short branch name (`no` / `en`), but the
  same solution builds full cultures elsewhere, and an exact match on `en` handed an English page
  Norwegian labels, dates and filter names with nothing thrown and no test failing.
  (Fhi.Metadata-l9l2n.16)
- **The filter panel asks the API for the language the rest of the component is rendering in**,
  rather than passing the host's raw token through as `Accept-Language`. The datatype facet's names
  are resolved server side, so a token the API did not recognise left that one block Norwegian on an
  otherwise English page. The header carries the API's own spelling of Norwegian, `nb`, rather than
  helsedata's `no`: `no` has no parent culture the API's request localization can fall back from,
  so it would quietly resolve to the API's default language instead. (Fhi.Metadata-l9l2n.16)
- **A host built with `InvariantGlobalization` no longer takes the property rows down.** Dates and
  the catalogue's sort order fall back to the invariant culture where `nb-NO` is unavailable,
  rather than throwing mid-render — and, for the sort order, throwing once from a static
  initialiser that cannot be retried. Both cultures resolve once at type load rather than per call,
  so such a host does not construct and catch an exception for every date it draws.
  (Fhi.Metadata-l9l2n.16)
- **Eleven class names that no stylesheet defines.** Nine block headings wore `headline-sm`, a typo
  for `headline-s`; the kildetype badge wore `tag`, and a tab wrapper wore `variable-meta__body`.
  None of the three is defined by helsedata's stylesheets or by Stiler, so each rendered unstyled
  inside helsedata.
- **A check that catches the next one.** The package's CSS checks only verified the names it
  invents; borrowed names had nothing watching them. `HostClassNames` renders each view and asserts
  every class in the DOM is one some stylesheet actually defines, against a capture of the 2,400
  class names helsedata's own bundles carry.

### Notes for hosts

- The XML doc comments still told hosts that `variables.css` is a page-specific stylesheet only
  helsedata's variable page carries, and that a host mounting the component elsewhere has to supply
  three pager names. Both halves were wrong. `variables.css` is served on every page of
  helsedata.no — `/no/`, `/no/variabler/` and `/no/datakilder/` load an identical seven bundles —
  so a host inside their estate has the result vocabulary wherever the component is mounted, not
  only on the variable page; and a host outside has to supply the whole of that vocabulary, the
  rows and the opened panel and the column picker as well as the pager. (Fhi.Metadata-h7yla)
- **The pager wears our own class names now, like the rest of the component.**
  `variables-pagination` and `variables-pagination-content` became `munin-explorer-pagination` and
  `munin-explorer-pagination-content`. They were the last part the component *drew* with names taken
  from helsedata's page-specific `variables.css`, and two of the three names of the 95 it emits that
  `Fhi.Helsedata.Stiler` 0.1.13 has no rule for — Stiler carries no pagination rule of any kind. A
  host with Stiler alone drew the pager at browser defaults while the rest of the component came
  out right, which is the failure the `munin-explorer` rename exists to end. The third name is the
  pager's skip link, which went the same way under `Fhi.Metadata-ja2qu` — see that entry.
  (Fhi.Metadata-hyyxl)
- **The rules for them ship in Stiler, under `components/munin-explorer/` with the rest of the
  prefix — in 0.1.14, not in 0.1.13, which predates this rename.** Until you are on 0.1.14 the
  pager renders at browser defaults, exactly as it did before the rename.
  Two of its rules are worth supplying yourself in the meantime whatever else you do about the
  look: an outline on `.munin-explorer-pagination:focus`, which is the only sign a sighted keyboard
  user gets that the skip link moved focus, and an unavailable state drawn from
  `.munin-explorer-pagination-content [aria-disabled="true"]` rather than from `:disabled`, because
  the buttons at the ends of the list are never `disabled`. Both sample hosts' `host.css` shows the
  shape. (Fhi.Metadata-hyyxl)
- **The third name was the skip link, and this rename did not close that gap.**
  `skiplink-pagination`, on the link that jumps past the result list to the pager, stayed
  helsedata's here: what it needs is not a look but a single visually-hidden-until-focused rule,
  and `variables.css` — served on every page of helsedata.no, despite the name — has it, while no
  released Stiler had a rule that hid the link. `Fhi.Metadata-ja2qu` is where it is closed.
  (Fhi.Metadata-hyyxl)
- **Inside helsedata.no nothing changes.** Their `variables-pagination` rules are still in
  `variables.css` on every page; the component simply no longer asks for them. (Fhi.Metadata-hyyxl)
- **The component now writes `munin-explorer-*` class names instead of helsedata's own.** It used to
  borrow `variable-explorer`, `variable-data-list`, `variable-dataitem` and `variable-meta`, and
  inherit their rules for free from the variable page's stylesheet — the page it exists to replace.
  **Hosts need `Fhi.Helsedata.Stiler` 0.1.13 or later**, which is where those rules now live; on an
  older Stiler the component renders at browser defaults.
- **A host outside helsedata.no can style every name from Stiler.** 92 of the 95 class names the
  component emits were in Stiler 0.1.13. Two of the three that were not were the pager's and the
  third was its skip link; `Fhi.Metadata-hyyxl` and `Fhi.Metadata-ja2qu` renamed all three into
  the prefix with the rest, and their rules ship in Stiler 0.1.14 — see those entries.
- **Design-system names are unaffected.** `hd-button-square`, `searchbox__freetext`, `headline`,
  `caption`, `infobox` and the rest are Stiler's, are still borrowed deliberately, and are not part
  of this rename.

## 0.1.0-alpha.2 — 2026-08-21

### Added

- The whole-variable view now shows the variable's version history: one row per version with its
  name, status and validity period, each expanding to that version's description and dates. It is
  built from the detail payload the host already has, so it costs no extra request and needs no
  host wiring — mount `VariableView` as before and the section appears when the variable has
  versions.

### Changed

- **One package instead of three.** `Fhi.Munin.Explorer` now carries the component, the client that
  feeds it and the types they share. Replace references to `Fhi.Munin.Explorer.Blazor` and
  `Fhi.Munin.Explorer.Client` with the single package; namespaces are unchanged, so no `using` has
  to move.
- **Supplying your own `IMuninExplorerClient` still works** — the interface is unchanged, and a host
  that registers its own implementation never touches the built-in one. What went away is the
  version matrix and the half-installed state where the component rendered with nothing behind it.

### Fixed

- The variable view's Kildenavn and Kortnavn are now marked `lang="no"` for English readers, as the
  kilde view's equivalents already were. Hosts styling or scripting on the `lang` attribute will see
  it on two `dd` elements that previously carried none.
- **A variable's datatype no longer appears twice, saying two different things** - once in the
  sidebar and once in the metadata, where the catalogue's Norwegian label for that field is an
  English word. (Fhi.Metadata-xbynn)

## 0.1.0-alpha.1 — 2026-08-21

### Added

- **The panel's third group, Egenskaper, showing the catalogue's own properties** - Opprinnelse,
  Kommentar, Datatype, Identifiseringsgrad, Databasereferanse, Erstatter and Synlig, which is Runa's
  set. Coded values are resolved to words: "Opprinnelse: 5" now reads "Direkte fra skjema", and
  "Synlig: 1" reads "Ja".
- **Nothing about those properties is known to this package** - which keys exist, what they are
  called, what order they come in and what their codes mean all arrive with the payload, in the
  reader's language. A property added or renamed in Munin appears here without this package being
  touched, and no vocabulary is copied into it — a copy would freeze editable master data in one
  language and drift the first time someone edited a definition. A key the catalogue no longer
  describes is skipped rather than drawn under its raw name, and a malformed vocabulary costs that
  one field its label rather than taking the panel down. (Fhi.Metadata-88tyl)
- `AddMuninExplorer(...)` registers the data client; the host supplies `ApiBaseUrl`, or sets
  `MuninExplorer:ApiBaseUrl` in configuration.
- **Sorting and paging are now two-way**, joining search, filter and selection, so a host can mirror
  the whole view into its URL and restore it from one. The component never touches the address bar
  itself - the host owns the URL.
- **A shared link that outlived its result set lands on the last real page** instead of an empty one,
  and the URL corrects itself so the next person it is sent to gets a working link.
- **LegacyHost shows how**, in one small wrapper component helsedata can copy. (Fhi.Metadata-eujqw)
- The rest of the Explorer API is now on `IMuninExplorerClient`: `GetFiltersAsync`,
  `GetKilderAsync`, `GetKildeAsync`, `GetKildeHierarchyAsync`, `GetDatasamlingAsync`,
  `GetVariableAsync` and `GetVariableTimelineAsync`, with contracts to match. A resource that
  does not exist answers `null`, or an empty collection, instead of throwing.
- `VariableSummary` gained `PresentationOrder`, `DataType` and `VersionId` — the API was
  already returning all three.
- **The reader chooses which columns the result list shows** - a Kolonner picker above the list,
  offering Runa's seven optional columns: Kode, Kilde, Datasamling, Variabelgruppe, Datatype, Status
  and Dataperiode. Navn is always there, because it is also the button that opens a row, and the
  last remaining column refuses to be hidden rather than leaving a list of nothing but names. The
  choice lasts as long as the page and is deliberately neither stored nor put in the host's URL,
  which is what Runa does today. (Fhi.Metadata-35oil)
- **Dataperiode is a column as well as a panel field** - the same two dates the open panel draws
  above its bar, so the column set is Runa's full seven. It is text rather than helsedata's bar,
  which is drawn entirely by rules this package does not ship. (Fhi.Metadata-35oil)
- **Status can now be shown even with historical variables filtered out** - the filter still decides
  where the column starts, and from the first press the reader's choice is what counts. Where Status
  is the only column left, turning "Vis historiske" back off no longer takes it away as well, so no
  combination of picker and filter can reduce a row to nothing but its name. (Fhi.Metadata-35oil)
- **The Data tab groups the kodeverk by kind and can show their codes** - Runa's arrangement: a
  heading per Kildekodeverk / Administrativt kodeverk / Helsefaglig kodeverk, one line per link
  under it, and a "Vis koder" control on every link the API serves codes for. Pressing it fetches
  the code list and draws Verdi, Navn, Gyldig fra and Gyldig til. Codes are asked for only when a
  reader presses, and kept once fetched, so collapsing and re-opening a list costs no second
  request — Kommunenummer alone is 885 codes and most readers open none of them.
- **A kodeverk the API resolved no name for says so, instead of showing its reference as its name**
  - the panel used to fall back to the reference, so a variable whose only link had no resolved name
  read "Kildekodeverk: 2336". It now reads "Ukjent navn" with "Referanse: 2336" underneath, and the
  reference is on every line, named or not, because it is what a reader can look the kodeverk up by.
- **`IMuninExplorerClient.GetKodeverkCodesAsync(variableId, kodeverkType, kodeverkReference)`** -
  new, with `KodeverkCodes` and `KodeverkCode` in `Fhi.Munin.Explorer.Contracts`. A host that
  implements the interface itself has one more member to supply. It answers null where the
  catalogue publishes no codes — every `HelsefagligKodeverk` link, and any reference the upstream
  register does not know — and throws on a fault, the same split the rest of the interface follows.
  A type or reference with a part that is nothing but dots is refused with an `ArgumentException`
  instead of being sent: no escaping survives `..`, because `Uri` unescapes `%2E` before it removes
  dot segments, so the value would resolve against the base address as a different endpoint
  entirely. The rule covers any all-dot part rather than just the `.` and `..` that normalise, since
  no real reference is all dots.
- **Two more DOM handles, and the package's first `<table>`** - `variable-explorer-kodeverk` (with
  `__item`, `__name`, `__reference`) and `variable-explorer-codes` (with `__table`). Neither Stiler
  nor helsedata's variable page has a kodeverk section to borrow names from, so a host mounting the
  component supplies the arrangement itself; `samples/LegacyHost` has a worked stand-in. The table
  is a real `<table>` because four columns of code values have no honest alternative shape — an
  unstyled table still aligns its columns, which is what makes an element safe where an invented
  class name is not. (Fhi.Metadata-jtjfm)
- **`VariableExplorer` can now page through the whole result** - Forrige / Neste buttons below
  the results, with "Side 2 av 13" between them, so the 18 000 variables behind the first 25 are
  reachable. Changing the search or the ordering starts again at page one, and turning a page
  keeps both. There is no infinite scrolling and no page-size picker: the host still sets
  `PageSize`, and the doc comment on that parameter says why the reader is not offered a
  choice. A `munin-explorer-skiplink-pagination` anchor above the results jumps a keyboard user
  straight to the controls instead of making them tab through every card. The pager stays on
  screen when a page turn fails, so the button that was just pressed is never removed under the
  reader's finger, and a page that comes back empty — an index that shrank between two requests,
  or an API that answers an out-of-range page with 404 — steps back to a page that has rows
  instead of reporting that nothing matched, keeping the pager even when that step back lands on
  a result with a single page. The position, both buttons and the row range all count from the
  page the server actually answered, so an API that clamps page 12 to page 8 cannot leave the
  caption describing different rows than the ones on screen; and if the step back fails in turn,
  the reader is put back on the page they turned from rather than left on the empty one.
  (Fhi.Metadata-l9l2n.12)
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
- **A variable's full detail now opens inside its own result card** - "Vis detaljer" under any row
  discloses the description, the period, the kilde trail (kildetype › kilde › datasamling), every
  variabelgruppe the variable belongs to and the kodeverk its values are drawn from, fetched from
  `GetVariableAsync`. There is no navigation behind it and no `@page` — the panel is drawn in the
  row it belongs to, which is what lets a CMS host that owns its own routing offer variable detail
  at all. One row is open at a time; a fetch that fails or a variable that is not published says so
  inside the panel and leaves the rows alone. (Fhi.Metadata-l9l2n.14)
- **The open panel is part of the component's parameter surface** - `SelectedVariableId` and
  `SelectedVariableIdChanged` give a host `@bind-SelectedVariableId`, so a reader's place in the
  catalogue can be deep-linked the same way the search text and the filters already are. The
  selection is always a row on screen: an id the result does not hold is dropped rather than
  fetched, and a new search, filter, ordering or page that leaves the open row behind closes the
  panel and reports it, so a host's URL cannot come to name a variable the page is not showing.
  (Fhi.Metadata-l9l2n.14)
- **The kilde and the datasamling a variable belongs to now open from inside its result card** -
  "Vis datakilde" and "Vis datasamling" under an open variable panel disclose the owner's own
  record, fetched from `GetKildeAsync` and `GetDatasamlingAsync`. The kilde says what kind of data
  source it is, who controls and processes the data, at what level of personal identification, on
  what legal basis, over what period, and how many datasamlinger and variables it holds; the
  datasamling says the same for itself plus its inclusion and exclusion criteria, its frequency and
  what one row of it counts. As with the variable panel there is no navigation behind it — the
  owner is drawn inside the card, so a CMS host that owns its own routing can offer kilde and
  datasamling detail at all. (Fhi.Metadata-l9l2n.15)
- **The datasamling reads its inherited values rather than its own** - Munin lets a datasamling take
  its data controller, data processor, identification level, legal basis and validity from its
  delkilde or its kilde, leaving its own fields empty. The panel shows what actually applies, so a
  datasamling whose controller is recorded one level up no longer reads as "Ikke oppgitt".
  (Fhi.Metadata-l9l2n.15)
- **One owner at a time, and never outliving the variable it hangs in** - opening the datasamling
  replaces the kilde rather than stacking beside it, and closing the variable panel, opening another
  row, searching, filtering, reordering or turning a page takes the owner panel with it. A fetch
  that fails, or a kilde the catalogue does not publish, says so inside the owner panel and leaves
  both the variable above it and the rows around it alone. (Fhi.Metadata-l9l2n.15)
- **Two fields the Explorer API had already started sending** - `FilterOptions.DataCategories`
  (`datakategorier`), the EHDS datakategori facet with its counts, and `PropertyMetadataEntry.Options`
  (`options`), the allowed values of a `SingleSelect` or `MultiSelect` property already parsed and
  already resolved to the request's language. A host rendering those values no longer has to parse
  `OptionsJson` itself, which is what this package used to tell it to do. Both were found by the new
  nightly contract check on its first run against the live API — the API and this package release
  separately, so nothing here had noticed either one. (Fhi.Metadata-l9l2n.20)
- `VariableExplorer` gained a `HeadingLevel` parameter (1–6, default `2`) that sets the
  level of its own title. Pass the level that follows on from the heading above the mount point:
  a component that emits an `h2` on a page whose last heading was an `h4` breaks the outline
  screen-reader users navigate by. Values outside 1–6 are clamped.
- Tag-triggered publishing to `Fhi.Helsedata.no`, helsedata's internal Azure Artifacts feed:
  push a `v*` tag and the package is built, tested, asserted and pushed. Nothing goes to
  nuget.org. The workflow refuses a tag that is not on `main`, a malformed version, and a build
  whose packed version disagrees with the tag. It also refuses a version that is already on the
  feed: the feed does allow one to be deleted, but whoever restored it keeps what they got, so a
  version number that has gone out is spent.
- `scripts/assert-package-contents.sh` checks the package has exactly the intended contents —
  no more and no less — and runs on every PR as well as before publishing. It is what would
  catch a stylesheet appearing in the RCL, which is supposed to carry no CSS at all.
- **Sorting in `VariableExplorer`** - results can now be ordered by data source, data collection
  or variable group, in either direction, on top of the API's own default order the list starts in.
  Choosing the active field again reverses it, choosing another starts it ascending, and any change
  goes back to the first page — the same rules Runa's sortable column headers follow. There are no
  column headers here, so the ordering is a control of its own above the list, and the chosen order
  is spoken through the status line the component already had rather than through `aria-sort`, which
  does not exist without a header to put it on. The default order's button reads "Standard" rather
  than "Navn": the API's `name` sort groups by data source first and only then follows the
  catalogue's own sequence, so a name label would describe an order the list is not in.
  `IMuninExplorerClient.SearchVariablesAsync` takes the new `SortField` and `SortDirection` and sends
  the API's own `sort`/`sortDir`; the Explorer API already ordered on both, with the variable code
  as a secondary key, so nothing changed there. (Fhi.Metadata-tfiui)
- First component: `VariableExplorer` — search and browse published variables from the Munin
  Explorer API. Takes `Search`, `SearchChanged`, `PageSize` and `Language` (`"no"` / `"en"`).
- **The kilde view in Runa's shape**, shared with the coming kildeutforsker rather than built twice:
  name, code and short name, kildetype and description; the catalogue's metadata in its own groups;
  the source's datasamlinger; and a sidebar of source information and statistics.
- **Metadata groups come from the payload**, so a group added or renamed in Munin appears without
  this package being touched. (Fhi.Metadata-vigv6)
- **The whole variable, as a view of its own** - name, description, the catalogue's metadata in its
  groups, kodeverk, statistics, and a sidebar saying where the variable lives. It opens in place of
  the list rather than at a route, because the package has no router; a host that mirrors
  `SelectedVariableId` into its URL already has a shareable link to it.
- **Statistics in Runa's shape**: year, minimum, maximum, mean and standard deviation, under a
  heading naming the kind of statistics. (Fhi.Metadata-xbynn)

### Changed

- **Split the explorer component into files by responsibility** - the facet sidebar, selection and
  detail loading, querying, the detail panel, the drill-in view and the translations each moved to
  their own file, leaving the component itself at a third of its former size. Pure move; the test
  suite is the contract and passed unchanged.
- **Lifted the translations out of the component** so the kildeutforsker shipping from this same
  package can share them rather than keeping a second copy that would drift. (Fhi.Metadata-7hu8p)
- **`dotnet format` now gives the same answer on Windows as on CI**, so a local check is worth
  running. `.gitattributes` forces LF in the working tree to match `.editorconfig`.
- **A kilde or datasamling opens as its own view, not a panel inside a panel inside a row** - it was
  three levels deep and cramped; it now takes over the component's area and offers a way back, which
  is as close to Runa's dedicated page as a component with no router gets. The search, filters, page
  and open row are all still there on return, because none of it is torn down — only hidden. It
  stays a named region so a screen reader moving by landmark still finds it.
- **The datatype column shows a name instead of a code** - "Integer" rather than "2". Resolved from
  the facets the filter panel already loads, so it costs no extra request and no lookup table lives
  in the package. `Accept-Language` now carries the component's own language, since the API resolves
  these names per request culture — without it a component rendering in English would have been
  served Norwegian labels, or the other language's cached body. (Fhi.Metadata-7mqzs)
- **The detail panel has Runa's two tabs** - Detaljer and Data, in helsedata's `variable-meta__tabs`
  vocabulary, with `role="tablist"`, correct `aria-selected` and arrow-key movement. Only the
  selected tab is in the tab order, so the tablist costs one tab stop rather than one per tab, which
  is what makes the arrow keys necessary rather than decorative. The tab returns to Detaljer when a
  different row is opened.
- **The panel's fields are grouped and laid out in lanes** - Identifikasjon and Plassering, Runa's
  groups, with the group heading as a small uppercase eyebrow rather than a heading-sized heading.
  The fields sit side by side in helsedata's `variable-meta__grid` (two lanes above 1280px, one
  below) instead of stacking. Runa uses three lanes; two of helsedata's beats three of ours.
- **The data period is drawn as a bar** - Runa's rule, taken from her implementation rather than
  guessed: the fill is the share of the variable's own lifetime that its data covers, floored at 5%
  so a short period still marks, and a period with no end date is drawn full and in a different
  colour, because "no end" means still running rather than unknown.
- **The kilde in the trail opens the kilde** - Runa links it to her own kilde route; this component
  has no routes, so the same affordance discloses the kilde in place. It shares `aria-expanded` and
  `aria-controls` with the existing button, so it reads as one control in two places.
- **Three fields take Runa's names** - the trail is a `Kildesti`, not a Datakilde; the panel's period
  is the `Dataperiode`; and the column header is plain `Kilde`. (Fhi.Metadata-7mqzs)
- **The status line now says which rows are on screen, not just how many** - "Viser 25 av 312
  variabler funnet" becomes "Viser 26–50 av 312 variabler funnet". It was only ever true of the
  first page, and it is also the results list's accessible name and the live announcement, so
  it is what tells a screen-reader user that a page turned. Hosts asserting on that sentence
  need to update. (Fhi.Metadata-l9l2n.12)
- **`PageSize` is clamped to 1–100** - the range the Explorer API itself accepts. A value
  outside it was previously passed through and silently changed by the server, which left the
  page count on this side describing a page size that was never used. (Fhi.Metadata-l9l2n.12)
- **`IMuninExplorerClient` takes a `VariableFilter`** - on `SearchVariablesAsync`, which gains it as
  a second parameter, and on `GetFiltersAsync`, where it replaces the `kildeType` parameter with the
  whole selection. Both are breaking: existing calls that pass positional arguments after the search
  term stop compiling, and a caller passing `kildeType` must wrap it as
  `new VariableFilter { KildeType = ... }`. The filter covers everything the API filters on,
  including datasamling and EHDS datakategori, which the filters endpoint reports no facet for and
  the panel therefore does not draw. A filter that narrows nothing adds nothing to the URL, so an
  unfiltered search is byte-identical to what it was before. (Fhi.Metadata-l9l2n.13)
- `VariableExplorer` now emits `Fhi.Helsedata.Stiler`'s own class names instead of invented
  `variable-explorer-*` ones, and lists results as `datasourcecard`s rather than in a table —
  the shape helsedata's datakildeutforsker already uses. On helsedata.no the component is
  styled by the site it is embedded in; nothing has to be added to Stiler for it. Hosts outside
  that estate must provide `form-element__label`, `searchbox__freetext*`, `hd-button-square` /
  `button-square--primary`, `headline`, `caption`, `infobox` and `datasourcecard*`; the two
  sample hosts show a working approximation.
- **The public API is now English throughout** - the package started out following Munin's own
  Norwegian identifiers, and this renames the lot before the first publish to the feed, while it
  still costs nothing. The component is `VariableExplorer` with `Search`, `SearchChanged`,
  `PageSize` and `Language` parameters; `IMuninExplorerClient` answers `SearchVariablesAsync`,
  `GetFiltersAsync`, `GetKilderAsync`, `GetKildeAsync`, `GetKildeHierarchyAsync`,
  `GetDatasamlingAsync`, `GetVariableAsync` and `GetVariableTimelineAsync`;
  `IMuninExplorerTokenProvider` answers `GetTokenAsync`; and the contracts are `Page<T>`,
  `VariableSummary`, `VariableDetail`, `VariableVersion`, `KildeSummary`, `KildeDetail`,
  `KildeHierarchy`, `DatasamlingDetail`, `FilterOptions`, `PropertyMetadataEntry` and the `*Facet`
  records. DTO properties follow — `Navn` is `Name`, `Beskrivelse` is `Description`,
  `GyldigFra`/`GyldigTil` are `ValidFrom`/`ValidTo`, `Dataansvarlig`/`Databehandler` are
  `DataController`/`DataProcessor`, and so on. **The JSON contract is unchanged**: every
  property carries an explicit `[JsonPropertyName]`, so the wire still spells everything
  Munin's way. Domain terms with no honest translation stay Norwegian inside otherwise-English
  names — `KildeId`, `DatasamlingCount`, `GetKildeHierarchyAsync` — and so do their Norwegian
  plurals, because those are the API's own field names. `AGENTS.md` records where the line sits
  and why. (Fhi.Metadata-osxfx)
- **The root element's class is now `variable-explorer`** - it carries no styling in Stiler or
  in this package and exists only so the component can be found in the DOM of a CMS page. A
  host with its own selector for the old `variabelutforsker` has to update it. User-facing
  Norwegian is untouched: every label, status message and error string reads exactly as before.
  (Fhi.Metadata-osxfx)
- **The package is published to helsedata's internal feed** rather than nuget.org. It is the feed
  their Optimizely project already restores from, and where their own packages live, so consuming
  the explorer needs no change to their configuration.
- **The package now carries the metadata FHI requires of an FHI package** - a copyright line, a
  pointer to the changelog as release notes, and a CONTRIBUTIONS file naming who builds this.
  (Fhi.Metadata-l9l2n.5)
- **The ordering moved into a column header, and the "Sorter etter" fieldset is gone** - helsedata
  and Runa both put sorting in the header, which is where a reader looks for it; keeping the
  fieldset as well would offer the same choice twice. The header is their own shape: a row wearing
  `variable-data-list__item__row--header`, with one `sortable-header` cell per column and Stiler's
  `hd-button-reset` on the buttons. Four of the five columns map to a real `SortField`; Periode has
  none, so its header is plain text rather than a button promising an ordering the API cannot do.
  The header renders whether or not the search found anything — it carries the ordering now, and
  taking it off screen mid-interaction would drop focus to `<body>`.
- **The columns each carry a per-column modifier, which is what a cell lines up by** - the widths
  hang off those names rather than off source order. The column SET is Runa's and is described in
  its own entry; this change is about the header they line up under. (Fhi.Metadata-zs56s,
  Fhi.Metadata-35oil)
- **Row cells no longer repeat the column name** - every cell said "Datakilde: Als registeret" because
  there was no header row to name the field. There is one now, and repeating the name in all
  twenty-five rows is exactly what a header exists to stop. The label is still emitted for assistive
  technology, in Stiler's `screenreader-only` span beside the value — deliberately not as an
  `aria-label`, which would REPLACE the value it labels and have a screen reader read the field name
  in place of the data.
- **The Status column is drawn only when historical variables can be in the list** - the API computes
  `VersjonStatus` from `GyldigTil` and filters expired versions out unless `IncludeHistorical` is
  asked for, so in the default view every row reads "Active". Verified against the live API: 100 rows
  sampled across five pages of the catalogue, all Active. A column that says the same word on every
  row is furniture, so it appears with the historical filter and not before. (Fhi.Metadata-zs56s)
- **Column widths follow Runa's proportions, and a code never wraps** - Kode is the widest column,
  which looks wrong until you notice a variable code is one unbreakable token: broken across two
  lines it stops being readable and stops being copyable. A name has spaces, so the name is the
  column that gives way. Widths are Runa's, measured off it — Navn 210, Kode 246, Kilde 96,
  Datasamling 212, Variabelgruppe 160, Datatype 114, Status 98 — expressed as flex ratios so they
  hold at any width. The code column truncates with an ellipsis rather than wrapping, and every
  cell carries its full value as a tooltip.
- **The Kilde column shows the short name** - "ALS" rather than "Als registeret", with the full name
  on hover, exactly as Runa does. A kilde name is long and repeats down every row of one register's
  variables. It falls back to the full name where a kilde has no short one.
- **Field names are read to assistive technology without being shown** - each cell carries its label
  in Stiler's `screenreader-only` span. A screen reader moving down a column has no header to glance
  up at, so the name has to travel with the value. (Fhi.Metadata-zs56s)
- **Rows line up with their column headers** - three things were pulling them apart. The name was
  wrapped in a heading, which made the heading the flex item rather than the button, so
  `.variable-dataitem-main__name` sized nothing and the column collapsed to its content. Each row
  also carried a description paragraph, which neither reference has — helsedata's rows are
  explicitly one line (`height: 3.5rem; overflow: hidden`) and Runa's are table rows. And the
  sample's own generic column rule sat after the per-column widths, silently overriding them.
  Header and row cells now land on the same pixel across every column.
- **The first column header says Navn, not "Standard (stigende)"** - it was rendering the sort
  field's label instead of the column's name. A header names its column; the ordering is shown by
  an arrow beside it and announced through `aria-sort` on the active column. Runa calls this column
  Navn, so it does too. (Fhi.Metadata-zs56s)
- **The result columns are Runa's, not the page being replaced** - Navn, Kode, Kilde, Datasamling,
  Variabelgruppe, Datatype and Status, which is Runa's column set. Runa is what helsedata's variable
  page is being replaced *with*, so it decides what a row says; helsedata decides what a row looks
  like. Taking the column set from the page being retired would have been copying the thing we are
  replacing. Four of the seven have a width modifier in helsedata's stylesheet; Kode, Datatype and
  Status do not, so they wear the bare `variable-dataitem-main__column` and size by content under
  their flex layout — using a class of theirs without a modifier, rather than inventing
  `__code`/`__dataType`/`__status`, which would be names with no rule behind them. Those three
  modifiers are worth asking for in the SCSS file helsedata offered. Periode is not a Runa column
  and is no longer a row column; it remains in the panel. (Fhi.Metadata-zs56s)
- **The results now wear helsedata's variable-page vocabulary instead of their datakilde cards** -
  the component was built from `datasourcecard*`, which is their *datakilde* explorer. We replace
  the *variable* explorer, and that page has its own: `variable-data-list__item` rows inside
  `variable-explorer-container`, with `variable-meta` for the opened panel. The switch is not a
  rename — 132 of the 292 selectors in that family are descendant selectors, so the nesting has to
  match or roughly half the styling silently does not apply. (Fhi.Metadata-zs56s)
- **A result row is opened by its own name, and the dead click target is gone** - the variable's
  name is now the disclosure button, which is helsedata's pattern and the APG accordion pattern.
  The old card advertised a click it did not have: `.datasourcecard` carries a pointer cursor
  because on their datakilde page the whole card is a link, and ours never was. There is no heading
  around the button: their row is a flex container and the name cell is sized by
  `variable-dataitem-main__name`, so a heading in between becomes the flex item and the column stops
  lining up with its header. Results are a list of list items, each with a named disclosure carrying
  `aria-expanded`. (Fhi.Metadata-ywnbs)

### Fixed

- Accessibility pass over `VariableExplorer`. The result summary now names the search it
  describes and says when only the first page is shown; failures are announced assertively
  through a `role="alert"` region instead of politely alongside the count; the result list has
  an accessible name; the Søk button is no longer disabled mid-search, which used to drop focus
  to `<body>`; a missing value reads as "Ikke oppgitt" rather than as an em dash; and Munin's
  own metadata is marked `lang="no"` so Norwegian variable names are not read by an English
  synthesiser.

### Notes for hosts

- Every request the client makes carries `X-Munin-Explorer-Client: blazor/<version>`. Munin's API
  is anonymous, and this is how it tells embedded-component traffic apart from anything else.
- A host that implements `IMuninExplorerClient` itself has seven new members to fill in. While on
  `0.x` the interface still moves; a component only calls what it needs, so unimplemented members
  can throw.
- The column picker adds eight class names a host outside helsedata's estate has to provide, all
  eight helsedata's own, from the `variables.css` their variable page carries —
  `variable-explorer-header` with `__actions` and `__actions-button`, the bare `dropdown` and
  `variable-explorer__dropdown` together on the disclosure, and `dropdown-choicepicker` with
  `--right` and `__item`. The two on the disclosure do different jobs and both are theirs:
  `.variable-explorer-header__actions .dropdown { width: 100% }` is what widens the trigger to its
  row, and `.variable-explorer__dropdown { z-index: 99 }` is what lifts the open list over the rows
  below it. All of them were read back off the compiled stylesheets rather than off a list of names,
  and each toggle's label is the button's own text so that no ninth name is needed to style it.
  `sortable-dropdown` is deliberately *not* among them, although it looks like the obvious fit: it
  is helsedata's mobile sort control, `display: none` above 1280px, so a picker wearing it would be
  invisible on every desktop.
- The open list is `position: absolute`, and the wrapper carries an inline `position: relative` so
  it anchors to the picker rather than to whatever the host page happens to have positioned above
  it. That is what helsedata's own markup does inline too. A host that styles none of these names
  still gets a working picker — it is a `<details>`, a `<ul>` and buttons in two states, the same
  three shapes the filter panel leans on — drawn in the flow instead of over the list.
- The picker's trigger is a `<summary>` dressed as the ghost square button, and a `<summary>` is
  `display: list-item` by default, so a host owes it two rules —
  `.variable-explorer__dropdown > summary { list-style: none }` and
  `.variable-explorer__dropdown > summary::-webkit-details-marker { display: none }`. Without them
  the button draws a stray browser disclosure triangle beside "Kolonner" that their own button does
  not have. helsedata's own control is a `<button>`, so nothing in their `variables.css` has a
  reason to suppress a marker here: this pair is owed by the primary host as well as by hosts
  outside their estate. Both sample hosts carry exactly these two. The filter panel's `<details>`
  needs nothing of the kind — its summary is not dressed as a button, so its marker is wanted.
- `screenreader-only` is now load-bearing in one more place: it hides the sentence explaining why
  the last remaining column will not turn off. Without the rule, that sentence is on screen for
  everyone.
- The new Dataperiode column needs `variable-dataitem-main__period`, alongside the `__code`,
  `__dataType` and `__status` width modifiers already outstanding with helsedata.
  `variable-dataitem-header__period` they already have. Both sample hosts show a working
  approximation. (Fhi.Metadata-35oil)
- There is now a worked example of calling Munin as the signed-in user from a Blazor Server
  host, in `samples/LegacyHost/Authentication/`. It exists because the two obvious
  implementations are both wrong and both fail silently: `IHttpContextAccessor` is null during
  circuit activity, and the provider is a singleton so it cannot hold a user without eventually
  handing one person's token to another. The sample resolves the circuit per call instead, and
  a test covers the property that cannot be checked by reading — two circuits running
  concurrently never see each other's token.
- The accounting of the `variable-explorer*` prefix is now complete and split by what a host loses
  by ignoring a name. Six of them are helsedata's, from the `variables.css` their variable page
  carries: `variable-explorer-container`, `variable-explorer-results`, `variable-explorer-header`
  with `__actions` and `__actions-button`, and `variable-explorer__dropdown`. Everything else in
  the prefix is this package's, and no helsedata stylesheet has a rule for any of it. Most are
  handles the element does not need — the root `variable-explorer`, `variable-explorer-filters`,
  `-detail`, `-drilldown`, `-kodeverk*`, `-codes*`, `-group` and the nine `variable-explorer-kilde*`
  names in `KildeView` — because a Stiler class or a browser default already dresses it. The group
  headings, for one, are sized by the `headline headline-xxs` they also wear, so leaving
  `variable-explorer-group` undefined costs the eyebrow's look and nothing more. Two are not
  handles: `variable-explorer-crumb` is the link affordance on the kilde step of the trail (which is
  a `<button>`), and `variable-explorer-period__track` / `__fill` / `__track--ongoing` are the
  period bar itself — only its width comes from an inline style, so an undrawn bar renders as
  nothing at all. Earlier notes listed six invented names and said a host that defined none of them
  lost nothing visual; both halves were wrong. (Fhi.Metadata-e4bj2)
- `variable-explorer-source` is an element id prefix, not a class. The drill-in it names wears
  `variable-explorer-drilldown`, so a host or a test reaching for `.variable-explorer-source` finds
  nothing. Both sample hosts had rules written against it that had been dead since the kilde panel
  became a drill-in; they now select the drill-in.
- The package emits two `<table>`s, not one: the kodeverk code list in an opened panel, and the
  datasamlinger of a kilde in `KildeView`. The results list is neither — it is helsedata's
  `variable-data-list`, a `<ul>` with a header row of `<div>`s.
- The XML doc comments that ship with the package, which are what IntelliSense shows a consuming
  developer, still described the `datasourcecard` result shape that `Fhi.Metadata-zs56s` replaced.
  They now describe the DOM the components actually emit.
- `KildeView`'s four block headings wore `headline-sm`, a name neither Stiler nor helsedata's
  `variables.css` defines and the only class name in the package that appeared nowhere else — so
  the headings fell back to the browser's own `<h*>` size inside an otherwise styled page. They now
  wear `headline-s`, the same size the view's own name wears, because Stiler's scale has nothing
  verified between it and the `headline-xxs` the field labels wear.
- Both sample hosts now carry the Data tab's kodeverk rules. `ModernHost`'s `host.css` was missing
  the whole block, so opening a variable's Data tab there showed an unstyled kodeverk list and an
  uncapped code table — Kommunenummer's 885 rows pushed the rest of the page out of reach — and it
  read as a difference in the hosting model rather than the missing 76 lines it was.
- **The kilde view's nine invented class names now have example rules in both sample hosts.** They
  had none. The view arrived with `variable-explorer-kilde` and eight `__`-suffixed names of its
  own — a header block, identifiers, kildetype, description, a body split into `__main` and
  `__aside`, and the `__datasamlinger` table — and neither sample styled any of them, so both drew
  the view at raw browser defaults: the sidebar stacked under the main column, the kildetype tag
  reading as a paragraph. These are names Stiler has never heard of and helsedata's `variables.css`
  has no kilde section to borrow from, so a host outside their estate owes rules for all nine; the
  samples now show a working approximation of each. The layout is two columns above 1024px and one
  below, the same threshold the filter panel already uses.
- `variable-explorer-period` — the wrapper around the period bar, as distinct from its `__range`,
  `__track` and `__fill` — was in the same position and is styled now too.
- **The sample stylesheets ask for palette tokens bare**, as `var(--grey30)` rather than
  `var(--grey30, #e6e6ed)`. The declarations are in the same file, so a fallback could never fire
  and could only disagree — and four of the six did. One of them, `var(--grey70, #5a5f78)`, named a
  token nothing declares, so five rules painted a colour that is not in the Stiler palette the file
  claims to reproduce. Those ask for `--grey60` now. Nothing a host has to copy changed; what
  changed is that the file no longer misstates its own colours to whoever reads it as a reference.
- `scripts/assert-sample-css-in-step.sh` checks both halves of the sample-stylesheet invariant now:
  that the two copies are byte-identical, and that between them they style every
  `variable-explorer*` name the package invents. The second is what would have caught the kilde gap
  — two copies can agree perfectly about a block neither of them has. (Fhi.Metadata-ktixw)
- **The pager and its skip link both wear our own class names, and Stiler carries their rules from
  0.1.14** - `munin-explorer-pagination*` and `munin-explorer-skiplink-pagination`. Neither was in
  Stiler to begin with: it has no pagination rule of its own — no `pagination`, `pager`, `paging`,
  `page-link` or `page-item` — while helsedata's own variable page styles both from a
  `variables.css` the site-wide stylesheet does not carry. The skip link's rule is the one to
  supply first on an older Stiler, because it is what keeps the link out of sight until it is
  focused rather than what gives it a look. Both sample hosts show a working approximation.
  (Fhi.Metadata-l9l2n.12)
- **The pager's buttons are never `disabled`** - at the first and last page they carry
  `aria-disabled="true"` and do nothing when pressed. A host stylesheet has to draw the
  unavailable state from that attribute rather than from `:disabled`, or the ends of the list
  look no different from the middle. The reason is focus: pressing Neste until the last page is
  the ordinary way to reach it, and disabling the element that currently has focus drops focus to
  `<body>`, which would leave a keyboard user tabbing from the top of the host's page.
  (Fhi.Metadata-l9l2n.12)
- **The filter panel introduces no new class names, and needs base element styling instead** - it
  is built from `<details>`, `<summary>` and nested `<ul>`s, with Stiler's `form-fieldset`,
  `form-element__label`, `caption` and the same `hd-button-square` / `button-square--secondary` /
  `button-square--ghost` pair the sort control already uses. That is deliberate: helsedata's own
  variable page styles its sidebar from `filter-search-explorer` in the page-specific
  `variables.css`, a rule this repository has not read back — the result vocabulary comes off that
  same stylesheet, so what is unverified is the one name and not the file — and the standing rule
  is that a class name goes into the markup only once it has been read off the host's compiled CSS.
  What a host has to supply is therefore base styling for those three elements — in particular list
  indentation, which is what shows a delkilde sitting under its kilde. Without it the panel still
  works and the hierarchy is still announced correctly; it just reads flat. Both sample hosts show a
  working approximation. (Fhi.Metadata-l9l2n.13)
- **A second name of ours appears in the DOM: `variable-explorer-filters`** - a handle, like the
  `variable-explorer` root, carrying no styling from this package or from Stiler. It is there so a
  host that can verify the sidebar names can place the panel without selecting on element position.
  (Fhi.Metadata-l9l2n.13)
- **Facet values are buttons with `aria-pressed`, not checkboxes** - so a host stylesheet has to
  draw the chosen state from `aria-pressed="true"` or from `button-square--secondary`, and the
  inert "Fjern alle filtre" button from `aria-disabled="true"` rather than from `:disabled`.
  (Fhi.Metadata-l9l2n.13)
- **The detail panel introduces one handle and no style names, and needs base element styling
  instead** - it is a `<dl>` of labels and values, an `<ol>` for the kilde trail and a `<ul>` for
  the variabelgrupper and kodeverk, wearing Stiler's `form-element__label`, `caption`, `infobox`
  and the same ghost `hd-button-square` the sort and facet buttons use. Stiler has no definition
  list, no breadcrumb and no key/value block that can be read back off its compiled stylesheet, and
  the standing rule is that a class name goes into the markup only once it has been read off the
  host's CSS. So a host supplies base styling for those three elements — in particular the trail,
  which without a rule renders as a numbered list rather than as a path. `variable-explorer-detail`
  is the third handle of ours in the DOM, alongside `variable-explorer` and
  `variable-explorer-filters`, and carries no styling. Both sample hosts show a working
  approximation. (Fhi.Metadata-l9l2n.14)
- **A result card now contains a button** - the disclosure that opens the panel, one per row, and
  never `disabled` — including while its own fetch runs, for the same focus reason the pager's
  buttons carry `aria-disabled`. A host stylesheet that assumed a card held no interactive element
  should check its `:hover` and `:focus-within` rules; Stiler's `datasourcecard` already has both.
  (Fhi.Metadata-l9l2n.14)
- **The kilde and datasamling panel adds a fourth handle and no style names** -
  `variable-explorer-source` joins `variable-explorer`, `variable-explorer-filters` and
  `variable-explorer-detail`, and carries no styling in this package or in Stiler. The panel itself
  is a heading and a `<dl>` wearing Stiler's own `datasourcecard__heading` and
  `form-element__label`, opened by the same ghost `hd-button-square` the sort, facet and detail
  controls already use — so a host that has styled the variable detail panel has very nearly styled
  this one. What is worth adding is the inset that says the kilde sits *inside* the variable rather
  than beside it; both sample hosts show one. (Fhi.Metadata-l9l2n.15)
- **A result card can now hold a heading below the card's own** - the owner panel is headed at one
  level below the result card, which is two below the component's `HeadingLevel`. A host that sets
  `HeadingLevel` correctly gets an unbroken outline for free; a host that styles headings by element
  rather than by class should check that level. (Fhi.Metadata-l9l2n.15)
- The host no longer needs a visually-hidden rule. Stiler has no global screen-reader-only
  helper, so nothing in the markup depends on one: the results list is named with `aria-label`
  rather than a clipped `<caption>`, and a missing value is written out as "Ikke oppgitt" for
  everyone rather than shown as an em dash and whispered to assistive technology. What is still
  the host's to get right is a visible focus indicator on the search field and the Søk button
  (WCAG 2.4.7) and text and non-text contrast (WCAG 1.4.3, 1.4.11) — the package ships no CSS,
  so it cannot supply either. Both are listed on the doc comment on `VariableExplorer`.
- The package now ships its XML documentation, so the rules that matter show up in IntelliSense
  at the call site rather than only in this repository — including that an
  `IMuninExplorerTokenProvider` must be singleton-safe and must not reach for
  `IHttpContextAccessor`.
- The package carries a real description on the feed rather than the packer's placeholder, so it
  says what it is to someone deciding whether to install it.
- The sort control adds five class names to the list a host outside helsedata's estate has to
  provide: `form-fieldset`, `button-square--secondary` and `button-square--ghost` (the two states
  of a sort button, alongside the `hd-button-square` base the Søk button already needed), and
  Stiler's `margin-right` / `margin-bottom` modifiers, which only apply on a square button. All
  five were read back off helsedata.no's compiled stylesheet, not off a list of names — Stiler's
  own sort-header rules are scoped under `article.registerOwnerListPage` and are unreachable from
  an embedded component, which is why the ordering is buttons above the list rather than clickable
  column headers. Both sample hosts show a working approximation.
- The component ships **no CSS**. Styling comes from the host — on helsedata.no that is
  `Fhi.Helsedata.Stiler`, and the class names the markup emits are Stiler's own, so nothing
  has to be added there for the component to look like the page it sits on.
- The component sets no render mode. The host decides at the mount site — `render-mode="Server"`
  on the `<component>` tag helper in a legacy Blazor Server host, or `@rendermode` in a modern
  Blazor Web App.
- A host mounting it must contain at least one `.razor` file (an `_Imports.razor` is enough), or
  the Blazor framework script is not served to the project and the circuit never starts.
- **The sample page now fills the window and sits on helsedata's own ground colour** - it capped
  itself at 1600px and painted the body pure white, so the sample looked narrower and flatter than
  the page it stands in for. helsedata sets no width cap at all, and its body is `#f6f7fc`, a faint
  blue-grey that is the reason their white cards read as raised rather than as part of the page.
  The tint was already in this stylesheet as `--grey10` and simply was not being used for the body.
  Nothing in the package changed; both are the host's ground to set.
- **The sample hosts now show the filter panel as a sidebar, and record what that costs** - the
  filters used to stack above the results, which meant the sample opened on four thousand pixels
  of facets with the first result below all of them. The layout is Runa's, measured off it: a
  384px filter column, a 24px gutter, and scrolling that starts only above 1024px. Nothing in the
  package changed — the component already put the filter panel and the results list as siblings
  under one root, so a host reaches this with a grid rule and no markup change. Three details are
  worth copying rather than rediscovering: the panel is a `<fieldset>` and so needs
  `min-inline-size: 0` before it will shrink into a column at all; it needs to span every results
  row, with `span 99` rather than `-1`, because the results rows are implicit; and Stiler's
  buttons are `white-space: nowrap`, so a facet named "Nasjonalt kvalitetsregister for ..." asks
  for 565px in a 384px column until the label is allowed to wrap.
- The search field talks to the server only when the search is submitted — Enter or the Søk
  button — not on every keystroke. `SearchChanged` fires once per search, not once per character.
- A host can now call Munin on behalf of its signed-in user: register an
  `IMuninExplorerTokenProvider` **before** `AddMuninExplorer`, and every request carries that
  token as `Authorization: Bearer`. With no provider registered nothing changes — calls stay
  anonymous, which is all public metadata browsing needs.
- Implementations must be resolvable from a singleton and must fetch the token *per call*.
  `IHttpClientFactory` caches the handler pipeline across callers for minutes, so a captured
  scoped dependency would serve a stale token, or one user's token to the next. In an
  interactive Blazor Server host that also rules out `IHttpContextAccessor`: there is no
  `HttpContext` during circuit activity.
