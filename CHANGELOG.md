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
