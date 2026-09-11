# Fhi.Munin.Explorer

The Munin **variabelutforsker** (variable explorer) as a Blazor Razor Class Library, so a host
application can embed Norwegian health-metadata browsing on its own pages.

Built for [helsedata.no](https://helsedata.no) as the first consumer — its Optimizely CMS drops
the component into a page — but the package has no helsedata-specific code and any Blazor host
can consume it.

Data comes from the public Munin Explorer API. **The browsing components are read-only and
anonymous**; everything the variable and kilde explorers render is public metadata and needs no
token.

The client reaches one step further than those. `IMuninExplorerClient` also carries the eight
`api/explorer/my/lists` calls — the signed-in user's saved variable lists — which are the only part
of it that is authenticated, and therefore the only part that needs a host-supplied
`IMuninExplorerTokenProvider` registered *before* `AddMuninExplorer`. Without one they answer 401,
which arrives as a thrown `HttpRequestException` rather than as an empty list. `VariableListView`
is the component built on them — the one this package ships that reads and writes rather than
browses — and a host is free to build its own instead.

**Mount `VariableExplorer`.** It is the variabelutforsker whole: search and the reader's lists
behind Runa's two tabs, with the view in the address bar. `VariableSearch` and `VariableListView`
are the two halves, public for a host that wants to lay them out itself — see
[What a host mounts](#what-a-host-mounts).

## Layout

| Project | What it is |
| --- | --- |
| `src/Fhi.Munin.Explorer` | The one package. Three folders, one per namespace. |
| `src/Fhi.Munin.Explorer/Blazor` | The components a host renders. |
| `src/Fhi.Munin.Explorer/Contracts` | DTOs and the client interface. |
| `src/Fhi.Munin.Explorer/Client` | Typed `HttpClient` implementation + `AddMuninExplorer()`. |
| `samples/ModernHost` | Blazor Web App — the everyday development host. |
| `samples/LegacyHost` | Legacy Blazor Server + MVC — mirrors helsedata's Optimizely host. |
| `samples/HostileHost` | The same, in helsedata's real stylesheet under their top-anchored header. Two pages: the composed explorer on `/`, the kildeutforsker on `/kilder`. |
| `test/Fhi.Munin.Explorer.Tests` | bUnit + xUnit. |

The first two sample hosts exist on purpose. helsedata's production site runs **legacy** Blazor
Server (`AddServerSideBlazor()` + `MapBlazorHub()`), mounting components inside MVC views with the
`<component>` tag helper. A component that only ever ran in a modern Blazor Web App can break
there in ways that never show up in development.

The third answers a different question, and answers it because those two could not. `HostileHost`
has a `PackageReference` to `Fhi.Helsedata.Stiler` and wears their `.main-header`, which is
`position: absolute; top: 0` over a 64px row and so covers the page's first 64px. It is the only
place a rule of theirs can collide with our markup before the collision reaches their site;
`scripts/check-hostile-host.sh` measures it with `getBoundingClientRect` and then runs axe over the
same page. It needs credentials for helsedata's private feed and is deliberately absent from the
solution — see [`docs/running-locally.md`](docs/running-locally.md).

`/kilder` is scanned too, and it is the page most worth measuring: the kilder table is the widest
thing the package draws and the only part of it whose overflow lands on the host's page rather than
on its own box. Every assertion the scan runs is itself checked by
`scripts/geometry-negative-control.mjs`, which breaks the page at least once per assertion and
requires the matching one to say so — a green geometry run means nothing while an assertion has
quietly stopped measuring anything, and an assertion with no such case is a tooling failure. Assertions printed `n/a` are pins whose defect cannot occur on that page; they
are stated, not skipped. (`Fhi.Metadata-fih3y`)

The two hosts share one stylesheet, copied — `samples/ModernHost/wwwroot/host.css` and
`samples/LegacyHost/wwwroot/css/host.css` are byte-for-byte identical, so a difference you see
between the samples is a difference in the hosting model rather than in the CSS. Change one and
copy it over the other; `scripts/assert-sample-css-in-step.sh` fails CI when they drift.

That script also checks the thing "the two agree" does not say: that between them the samples
style **every** `munin-explorer*` class name the package invents. Neither sample carries
`Fhi.Helsedata.Stiler`, so those names are inert until this one stylesheet supplies a rule, and a
name with no rule renders at raw browser defaults in both samples at once — which reads as a bug in
the component. Agreeing and being right are different claims, and the second clause is the one that
checks the second.

## Conventions

Code identifiers here are **English**; Norwegian is for user-facing strings and domain terms
that have no honest translation (`kilde`, `datasamling`, `variabelgruppe`, `kildetype`,
`kodeverk`). See [`AGENTS.md`](AGENTS.md) — it covers the conventions the compiler cannot
check.

## Rules the components follow

These are not style preferences — each one is a host that breaks otherwise.

- **No `@page`.** There is no router in the Optimizely host; the CMS owns routing. The explorer
  is a single parameterised root component.
- **No `@rendermode`.** The host decides, at the mount site. This is what lets one package serve
  both a legacy and a modern host.
- **No CSS, no `wwwroot`, no `.razor.css`.** Styling comes from the host. The names the markup
  emits split in two, and the difference matters to whoever is writing the rules:
  - **Borrowed.** Where a part of the component is ordinary page furniture, it wears
    `Fhi.Helsedata.Stiler`'s own name, every one read back off Stiler's compiled stylesheet rather
    than guessed at: `searchbox__freetext*`, `hd-button-square` with its `button-square--*`
    modifiers, `form-element__label`, `form-fieldset`, `headline`, `caption`, `infobox`,
    `hd-button-reset`, `screenreader-only`, and `dropdown-choicepicker*` for the column picker's
    open list. These are not ours to rename: a change to one of them is a change to Stiler. Every
    borrowed name is now one Stiler really defines. Three were not — the pager's two names and its
    skip link's, all read off helsedata's own page-specific `variables.css` — and all three are
    ours now: the pager under `Fhi.Metadata-hyyxl` and the skip link into it under
    `Fhi.Metadata-ja2qu` — see below.
  - **Ours.** Everything the explorer is actually built out of — its structure and its whole result
    vocabulary — is under the `munin-explorer` prefix, which this package owns. Since
    `Fhi.Metadata-zs56s` that vocabulary is shaped like helsedata's variable page rather than like
    something of its own: rows are `munin-explorer-data-list` / `munin-explorer-data-list__item*` /
    `munin-explorer-dataitem-main*`, the opened panel is `munin-explorer-meta*`, the list around
    them is `munin-explorer-container` / `munin-explorer-results` / `munin-explorer-header*`, the
    column picker hangs in `munin-explorer-header__actions*`, the pager is
    `munin-explorer-pagination` / `munin-explorer-pagination-content`, with the numbered pages in
    `munin-explorer-pagination-pages` and the size control in `munin-explorer-pagination-size`, and
    the link that jumps past the results to it is `munin-explorer-skiplink-pagination`.

    It was not ours until recently, and the change is the reason a host outside helsedata can style
    this component at all. The package used to write helsedata's own names — `variable-data-list*`,
    `variable-dataitem*`, `variable-meta*` and six `variable-explorer-*` — and inherit their rules
    for free off `variables.css`, the stylesheet of the very page this component replaces. Free
    only inside their estate: everywhere else those names meant nothing, and there was nowhere to
    put a rule for them that would not be overwritten by the next build of somebody else's site.
    The rules ship in **`Fhi.Helsedata.Stiler` 0.1.13** and later, under
    `components/munin-explorer/`. **A host on an older Stiler renders the component at browser
    defaults**, which is why the changelog states the floor as a version rather than as advice.
    Note that the old prefix is not free either: Stiler still defines `.variable-explorer-header`,
    so writing a `variable-*` name here is either borrowing helsedata's or colliding with it.

    The pager was held back from that rename and moved under `Fhi.Metadata-hyyxl`, because the
    case for borrowing looked strongest there: Stiler has no pagination rule of any kind, while
    `variables.css` has one and loads on every page of helsedata.no. That is an argument about
    their estate and not about anyone else's — a host with Stiler alone drew 92 of the 95 names
    correctly and the pager at browser defaults — so `variables-pagination` and
    `variables-pagination-content` became `munin-explorer-pagination` and
    `munin-explorer-pagination-content`, and their rules belong with the rest of the prefix in
    Stiler under `components/munin-explorer/`. **They are not written yet.** This paragraph claimed
    they shipped in 0.1.14; checked against the `Fhi.Helsedata.Stiler` working copy on 2026-09-03,
    `origin/main` carries exactly one pagination selector — `munin-explorer-skiplink-pagination` —
    and nothing for `-pagination`, `-pagination-content`, `-pagination-size` or `-pagination-pages`.
    So the pager renders at browser defaults on **every** Stiler version, 0.1.14 included, and the
    rules are outstanding as `Fhi.Metadata-ejcbi` records. Inside helsedata nothing changes either
    way — their `variables-pagination` rules are still in `variables.css`, now unused.

    The third of those 95 names was the pager's skip link, and it went the same way under
    `Fhi.Metadata-ja2qu`. It is worth spelling out because it failed backwards from every other
    missing rule here: what was missing was the rule that **hides** the link until it is focused,
    so a Stiler-only host drew a permanently visible "Hopp til paginering" over every
    multi-page result list rather than an unstyled anything. Neither sample host showed it — both
    styled the borrowed name in their own `host.css` — and neither guard could, because neither
    guard reads Stiler. Both ask only whether a name has a rule that declares something, in the
    capture of helsedata's live page (`test/host-class-names.txt`, where `skiplink-pagination` sits
    at line 2064 because helsedata styles it) or in the sample stylesheet — and neither can say
    which declarations the rule needs to carry, which is the question this link turned on. The
    name was in both sources the whole time it was broken, and neither source says anything about
    the host that has neither of them.
    `skiplink-pagination` is `munin-explorer-skiplink-pagination` now, and Stiler carries its rule
    unscoped — the one pagination selector it does have.

    Unscoped is the load-bearing word. The first attempt at a Stiler rule for this link — on the
    `feature/munin-explorer-scss` branch, which was never released under that shape — was scoped
    `.munin-explorer-header .skiplink-pagination`, and that selector cannot match: the header opens
    and closes entirely inside the column picker, while the anchor is rendered beside the result
    list. A rule naming the right class under the wrong ancestor draws exactly nothing, which is
    the same outcome as no rule at all and reads as coverage to any check that searches for names.
    An empty block is that failure with the ancestor taken away, and it is the one the guards do
    catch: a name whose every rule declares nothing is reported, and reported apart from a name
    with no rule, so the reader is not sent looking for a rule that is sitting right there.

  A name no stylesheet has heard of renders as a raw browser default inside an otherwise styled
  page, which defeats the point of shipping this as a component at all. That is why owning the
  prefix does not mean inventing freely: where there is no rule for a shape, change the shape
  rather than adding a stylesheet. The filter panel is `<details>` plus a nested `<ul>` rather than
  an accordion and a tree, and the detail panel is a `<dl>` with an `<ol>` for the kilde trail,
  because no host stylesheet names any of those. What a host supplies for them is base element
  styling — list indentation in particular, which is what shows a delkilde sitting under its kilde.
  `KildeView`'s own delkilde tree is a nested `<ul>` for both halves of that: a browser indents it
  unasked, and the nesting is a relationship a screen reader reads rather than one CSS draws.
  The package emits `<table>`s for the same reason: the kodeverk code list in an opened panel, and
  a kilde's datasamlinger in `KildeView`, one per level of that tree. An element degrades to its
  own browser default, where an unknown class name degrades to nothing.

  The panel's `Nivålinjer` toggle is a neighbouring rule rather than that one: it puts
  `data-level-lines="true"` on `munin-explorer-filters` and draws nothing itself. The argument above
  does not apply to it and should not be borrowed for it — a class on the `<ul>` that is already
  there would render exactly as it does today, unstyled or not, because no element is being replaced.
  What a class would cost is inventory: this contract, both sample stylesheets and
  `assert-sample-css-in-step.sh` would each have to carry the name for good. A state marker owes
  none of that. Both sample stylesheets show the rule — one `border-left` on the nested lists — and
  a host that supplies none loses the lines and no information, because the indentation is what
  carries the hierarchy either way.

  **A host writing that rule owes it 3:1.** A guide line is a non-text control under WCAG 1.4.11,
  and the obvious token is the wrong one: the samples' `--grey30`, which every other border in the
  filter panel uses, measures **1.16:1** against the page ground `--grey10` and is invisible above
  about 1000px — the lines exist in the DOM and cannot be seen. `--grey40` reaches 1.82:1 and still
  fails. The samples use `--grey60`, which gives **6.76:1**. The panel sits directly on the page
  ground rather than on a card, so the ratio is against whatever the host's own body paints, and a
  host with a dark theme has to clear 3:1 there too — neither sample defines one, so a host
  redefining the token for dark is deciding that outcome alone and unverified.

  That debt is due on first paint now, not on a press. `LevelLines` defaults to **on** since
  `Fhi.Metadata-dfygj`, matching Runa, whose own Nivålinjer loads pressed — a reader who never
  finds the button has to see the tree as a hierarchy. Runa draws its rails at Tailwind
  `border-gray-200`, `#e5e7eb`, which is **1.24:1** on its white ground; the samples deliberately
  do not copy that, because it is the defect `Fhi.Metadata-wcbxi` was filed for. What the two do
  agree on is the 1px width and the extent: the line runs a group's full height and ends at the
  bottom of its last child rather than stopping short of it. The indent step differs — 20px per
  level against Runa's 16px — and 20px is a number measured in the two sample stylesheets, whose
  `.munin-explorer-filters ul` sets it, rather than one this package ships. What
  `Fhi.Helsedata.Stiler` indents by on helsedata.no is unmeasured here, because nothing in this
  repository reads Stiler.

  Every name in the `munin-explorer` prefix is ours. That is worth saying because it used not to
  be: under the old prefix six names were helsedata's — the container, the results column, the
  header with its `__actions` and `__actions-button`, and the dropdown — and the prefix itself was
  no guide to which was which, so a reader had to check each one against a list. There is no longer
  a category to check against. The `THEIRS` allowlist in `scripts/assert-sample-css-in-step.sh` is
  empty by construction, and what these names cost a host is now the same question everywhere: a
  host on Stiler 0.1.13 or later has rules for them — 0.1.14 for the pager's skip link, which was
  renamed after 0.1.13 shipped, none at all yet for the pager itself, and 0.1.39 for the single
  lane the three asides' `munin-explorer-meta__grid` needs, without which the sidebar's words push
  the whole page into horizontal scrolling above 1280px (`Fhi.Metadata-hi0po`) — any other host
  draws whatever it wants drawn, and the sub-lists below are about how much drawing nothing costs.

  - Handles, where something else already dresses the element — a Stiler class it also wears, or
    its own browser default — and the name is there so a host or a test can find that part of the
    component in the page: `munin-explorer` (the root `<section>`), `munin-explorer-filters`,
    `munin-explorer-detail`, `munin-explorer-drilldown`, `munin-explorer-kodeverk*`,
    `munin-explorer-codes*`, `munin-explorer-group`, the `munin-explorer-kilde*` names in
    `KildeView`, the `munin-explorer-datasamling*` ones in `DatasamlingView`, the
    `munin-explorer-whole*` ones in `VariableView`, and the `munin-explorer-kilder*` names in
    `KildeSearch` — the kilde list's table, the checkbox column in front of it, the button that
    opens a row and the columns that hold a number. The samples style them for arrangement — the
    root as a grid at desktop width, `-filters`, `-detail`, `-drilldown`, `-kodeverk*` and
    `-codes*` for spacing, indentation and a rule between rows, the kilde, datasamling and variable
    views' name block, main column and sidebar as one page layout under three prefixes, the kilde
    list as a table with its counts right-aligned, and a count of nought dimmed under
    `munin-explorer-kilder__count--zero` so an empty register reads as empty rather than as a
    measured value — and `munin-explorer-group` is now the space
    between one group and the next and nothing else. It used to draw Runa's 11px blue uppercase
    eyebrow over the `headline headline-xxs` the heading already wears, which is what drew a group
    heading smaller than the 16px values beneath it; the host's own heading style wins there now
    (`Fhi.Metadata-gvtt9`). A host that defines none of them loses no information: the group
    headings, for instance, are already sized by the `headline headline-xxs` they wear, so what an
    undefined `munin-explorer-group` costs is the gap between groups, not the fact that it is a
    heading. The kilde list is the same bargain twice over, which is why it is a `<table>` of
    `<button>`s — an undrawn table still lines its columns up and an undrawn button is still
    visibly a control.
    Kelda's facet panel adds two more, `munin-explorer-filters__toggle` and
    `munin-explorer-filters__facets`, and they are handles for the same reason: the folding itself
    is the browser's `hidden` attribute, so a host that defines neither gets a panel that opens and
    closes at every width. What the rules buy is the sidebar — at desktop the samples take the
    folding away and put the toggle off screen, because a button offering to unfold a panel that is
    already open is a control that does nothing.
    Both explorers' facet values add one more, `munin-explorer-filters__count`, worn by the number
    beside a value. A handle on the same terms: undefined, the count renders inline as the text it
    has always been, which is exactly what shipped before it had a name of its own. What a rule
    buys is the dimming and the tabular alignment that stop a column of numbers reading as more of
    the words in front of them. It sits inside the `<label>` on purpose — the label is what names
    the checkbox, so a count moved out of it would stop being announced with the value it counts.
    A stylesheet cannot move it out: `position`, `order` and `display: contents` change where the
    number is drawn, not what the label contains, and Chrome computes the same `Aktiv (3)` under
    all three. What does drop it from the name is `display: none` or `visibility: hidden` on the
    count, so a host that hides it visually hides it from screen readers with it.
    Kelda's facet summaries add `munin-explorer-filters__chosen`, and it is worth a sentence only
    because it looks like the name above and wants the opposite rule: this is how many of one
    facet's values are ticked, sitting beside that facet's heading, where `margin-left: auto`
    belongs to the disclosure marker rather than to the number. A handle again — the words are
    markup, so a folded facet is announced as "Kildetype 2 valgt" with no stylesheet at all, and
    what a rule buys is the dimming and the tabular figures. What a host owes the summary line
    itself is a rule of a different kind, on no name of ours: the heading in there is a block box,
    so without one laying the summary out as a row the count is drawn under the heading and the
    disclosure marker under that. Both sample stylesheets carry it, and so does
    `Fhi.Helsedata.Stiler`, where it is shared with the variabelutforsker's panel and redraws the
    marker on the trailing edge — a summary laid out as a row is no longer a list-item, and the
    browser stops drawing a marker for it.
    The variabelutforsker's Kilde facet adds `munin-explorer-filters__search`, the box that narrows
    that facet's own values. A handle: undefined, it is a browser-default search field, which is
    visible, operable and named by a `<label>` of its own, so what a rule buys is the box — full
    width in a sidebar column, 34px tall, and the panel's own type size rather than the page's.
    Two things about it are not free. The name has to sit on an `<input>`, because Stiler's rule is
    written `input.munin-explorer-filters__search`: their global `input[type="search"]` list is
    (0,1,1) and a bare class is (0,1,0), so a class on any other element loses the font-size to it
    and the field draws at 18px instead of 14px. And the rule reached `Fhi.Helsedata.Stiler`'s
    `main` after 0.1.42 was cut, so the floor is the first release that follows it — a host on
    0.1.42 or older is in the undressed case above rather than a broken one. The kildetype groups inside that
    same facet add no name at all: they are `<details>`/`<summary>` like the facets around them, so
    the marker, the open state and the focus ring come from rules a host already has, and their
    counts wear `munin-explorer-filters__chosen` from the row above.
    The saved-list view's `munin-explorer-dataitem-*__desiredData` pair is a handle on the same
    terms and worth one sentence, because the cell holds a control rather than a value: undefined,
    the annotation field is a browser-default text box, which is visible, operable and named, so
    what is lost is the column's width and the border marking a text the API refused. The refusal
    itself is a sentence in the alert region either way, so no host loses the reason — only the
    mark saying which row it was about.
    The variable explorer's own panel adds `munin-explorer-filters__toolbar`, the row holding Utvid
    alle, Skjul alle and Nivålinjer. The three buttons used to sit in inline flow carrying margins
    of their own, and the last one's trailing margin counted against the line: at the 369px an
    expanded panel leaves once it grows a scrollbar, the row needed 369.05px and Nivålinjer dropped
    onto a row by itself. A host that defines nothing for the name gets the three buttons back in
    inline flow, which is a row until a label grows; what the rule buys is `display: flex` with a
    `gap`, so nothing trails the last button, and — for the two fold buttons, which is all its
    `min-width: 0` half selects — labels that shrink and wrap rather than the row breaking apart at
    the next longer translation. Nivålinjer is deliberately outside that half (below), so it is the
    row's one member at its own natural width, and the 16px the container won back is spent on it
    and then some: the switch adds a 30px track and an 8px gap to the label it already had. What
    keeps the row whole is that no other member is fixed — the two that can shrink absorb it. That
    is reasoning rather than measurement, and the measurement is worth doing: at 369px the row was
    already 0.05px over before any of this. Both sample stylesheets carry it, and it is in
    `Fhi.Helsedata.Stiler` from the release that follows PR 39046.
    Kelda's panel wears the same name for the same row, minus Nivålinjer — its facets are not
    nested, so a level-lines toggle would draw nothing — and there the rule does one thing more: it
    pins the row to the top of the facet column, which scrolls at sidebar widths, so a control that
    is wanted after several facets are open does not scroll away with them. That half of the rule
    selects the row as a direct child of `munin-explorer-filters`, which is where both panels emit
    it, and a host that defines nothing loses the pinning rather than the buttons.
    One thing about that rule is now load-bearing elsewhere: it selects `hd-button-square`, and
    `Nivålinjer` deliberately no longer wears it. The switch carries `munin-explorer-switch` and
    nothing else, because with the house classes beside it the toolbar's `min-width: 0` and
    `overflow-wrap: anywhere` squeeze the control to one character wide, every letter on a line of
    its own. That is measured rather than feared — 4.72×304.34px with the house classes against
    114.98×32 without — but **it was not measured here**: the numbers are the Stiler half's
    (`Fhi.Metadata-l9l2n.86`), taken by injecting this markup into Runa's toolbar against Stiler
    source. This repository *can* reproduce them, and the way to is
    `STILER_FROM_SOURCE=1 ./scripts/check-hostile-host.sh`, which swaps the pinned package for a
    Stiler checkout beside this one and so measures Stiler `main`, where the switch rules already
    live. Under the pinned package that same gate measures a browser-default `<button>` instead,
    because `samples/HostileHost` restores a published Stiler and .86 shipped its SCSS without a
    version bump, so the rig has no switch rules until the release that follows PR 39257 — which is
    `Fhi.Metadata-aonvl`, and until it lands the from-source run is the only one that measures
    the control that ships. A host writing its own toolbar rule owes the switch the same exemption.
    The row of active-filter chips over the results adds three, all shared with that panel:
    `munin-explorer-filters__active` is the row, `munin-explorer-filters__chip` the capsule around
    one ticked value and `munin-explorer-filters__chip-remove` the close control inside it. Handles,
    all three, and the reason is the shape rather than the rules: the row is a heading, a run of
    `<span>`s and a `<button>`, and the close control is a bare `<button>` whose accessible name is
    written down — so a host that defines none of them gets the same words, the same controls and
    the same order, in inline flow instead of a row of capsules. What the rules buy is the capsule
    itself and a 24×24 box for the close control, which is a WCAG 2.5.5 target rather than a
    decoration. The rules are `Fhi.Helsedata.Stiler` PR 39206's, which 0.1.42 — the version pinned
    here — predates, so a host has them from whichever release takes it. The two other names in that
    row are borrowed and need nothing new — the heading is `caption margin--none` and the clear-all
    is `hd-button-square button-square--ghost`, Stiler's, worn in this component already by the
    facet panel's fold toggle.
    Both explorers add one more, `munin-explorer-results__toolbar` — the row the result count
    shares with the controls that used to take a row each: the Sorter control and the Kolonner
    picker in the kildeutforsker, and the Kolonner picker alone in the variabelutforsker, whose
    ordering is on the column headings and whose Per side stays at the pager. A handle, and the
    plainest one here: undefined, they go back to being blocks in ordinary flow, which is exactly
    what shipped before the name existed, so what a rule buys is a row of vertical space and
    nothing a reader could otherwise miss. Its rules are
    `Fhi.Helsedata.Stiler` PR 39220's, merged after 0.1.42 was cut, so a host has them from the
    release that follows the version pinned here. The name says `results` and the element sits
    above `munin-explorer-results` rather than inside it, deliberately: the results container is
    drawn only with rows on screen, and the count inside this row is the component's one polite
    live region, which has to be in the DOM before its text arrives.
  - Names that carry meaning nothing else carries, so a host without Stiler's rules has to draw
    them itself: `munin-explorer-crumb` carries the link affordance for a trail step, which is a
    `<button>` — the kilde step of the panel's kilde trail, and every step of the hierarchy trail
    over the results — and without it a trail reads as plain text with no sign it can be pressed;
    `munin-explorer-breadcrumb` is that hierarchy trail's own wrapper, the navigation landmark the
    steps sit in and where the chevrons between them come from, and an undrawn one is a plain
    numbered list; and inside the `munin-explorer-period*`
    wrapper, `__track`, `__fill` and `__track--ongoing` are the period bar itself — only its width
    comes from an inline style, so an undrawn bar renders as nothing at all. The period is still
    legible without it, because the dates are next to it in words, in `__range`.
    `munin-explorer-kilder-scroll` is the box the kilder table scrolls in, and it is the one name
    here whose cost is paid by the HOST's page rather than by the component's own. The table's
    eight columns want 779px at their narrowest — measured over 66 kilder — and helsedata's content
    box goes under that at about 827px. Undrawn, the overflow has nowhere to go but the document,
    so the whole of helsedata.no scrolls sideways under a component that is part of one page: a
    WCAG 1.4.10 failure on somebody else's site rather than a table that looks wrong on ours
    (`Fhi.Metadata-b3brc`). The markup carries `role="region"`, `tabindex="0"` and the table's own
    name, so a host that adds `overflow-x` gets a scroll box a keyboard can already reach — and
    owes it a `:focus-visible` outline as well, because a focus stop the reader cannot see is a
    WCAG 2.4.7 failure in place of the 1.4.10 one. A host running a CSS reset that strips outlines
    has to put one back; both sample stylesheets and Stiler use `outline: 2px solid <focus colour>`
    with `outline-offset: 4px`.
    `munin-explorer-list-scroll` is that same box around the saved-list table, and the one place
    this package does **not** leave the overflow to a host: the saved list is a real `<table>` now,
    nine columns wide where the flex row it replaced folded into a card under 1280px, and measured
    in `HostileHost` it put 1323px of table in an 843px page with the document scrolling. Stiler
    has no rule for the name, so `overflow-x: auto` is set inline on the box — the shape the column
    picker's own inline `position: relative` already uses, for the same reason: without it the
    markup is wrong rather than plain. `position: relative` is inline here as well, and is the
    half that is easy to miss: an `overflow` clips an absolutely positioned descendant only where
    it is that descendant's containing block, and every row's `screenreader-only` label is one, so
    without it a host using the `clip` idiom rather than a negative offset scrolls sideways through
    them anyway. The class is still the hook, and the `:focus-visible` outline under it is still
    the host's, for the reason the kilder box gives.
    `munin-explorer-pagination-pages` joined this list under `Fhi.Metadata-ejcbi`, and it is worth
    saying why it moved out of the handles: the numbered pages wear helsedata's own
    `hd-button-reset`, which strips the button chrome, so unlike every other control here nothing
    else is dressing them. Undrawn, the run is a line of bare digits with no sign that the one you
    are on is the one you are on — the rule that marks `.current` is the only thing that says so.
    The square-button pair it wore before drew that for free, which is exactly why it is tempting
    and exactly why helsedata's own pager does not use it. Next in this list
    is `munin-explorer-retry`, on the two retry buttons in the alert region: it draws their inert
    state, and it is the one name here that **no Stiler version carries and no Stiler branch has a
    rule for** — not 0.1.14, not `main`, tracked as `Fhi.Metadata-x6vqc`. That is a sharper claim
    than *unpublished*, and worth keeping distinct from it: the switch's two names below are also
    carried by no released Stiler, but their rules are written and sit on `main` awaiting a version
    (`Fhi.Metadata-aonvl`), where retry's have still to be written at all. The buttons are never
    `disabled`, because that would drop the focus of the reader who just pressed one to `<body>`,
    so `aria-disabled` is what says the offer is spent; the alert region deliberately carries no
    class, so neither the pager's nor the filter panel's `[aria-disabled]` rule reaches in, and
    without one of its own a button that does nothing looks exactly like one that works. That is a
    WCAG 2.1 AA problem rather than a cosmetic one, and it is the `skiplink-pagination` shape: both
    sample stylesheets have the rule, so the guard is green while the host the prefix exists for
    gets nothing.
    The filter panel's `Nivålinjer` switch closes the list with two, `munin-explorer-switch__track`
    and `munin-explorer-switch__thumb`, and they are here rather than among the handles for the reason
    the period bar's are: both spans are empty, so an undrawn one is nothing at all and the on/off
    state a sighted reader can see goes with it. The state itself is not lost — the control is a
    `role="switch"` carrying `aria-checked`, so a screen reader announces it either way — and the
    wrapper `munin-explorer-switch` is a handle, because undrawn it is still a browser-default
    `<button>` with its label in it. What the rules draw is the 30×18 track and the 12×12 thumb
    that travels 12px across it, and they hang on `aria-checked` rather than on a modifier class,
    so the drawn state and the announced state cannot come apart.
    **A host pinning a published Stiler must draw all three itself for now.** The rules are written
    and merged on Stiler `main` (PR 39257), but no released version carries them, so 0.1.14 gives
    you nothing here — that release is `Fhi.Metadata-aonvl`, and until it lands what a pinned host
    renders is a bare `<button>` whose appearance never changes with the state. Operable and
    correctly announced, since `aria-checked` carries the state, but with no visible on/off mark:
    a sighted reader loses what a screen reader user still hears.

  Ids are a separate family, each suffixed with a per-instance discriminator so two mounts on one
  page cannot collide: `munin-explorer-title-*`, `-search-*`, `-heading-*`, `-toggle-*`,
  `-detail-*`, `-tab-*`, `-source-*` and the rest. `munin-explorer-source-*` is worth naming,
  because it reads like a class and is not one: the drill-in region it identifies wears the class
  `munin-explorer-drilldown`, so a host or a test reaching for `.munin-explorer-source` comes up
  empty.

  One family more is written by interpolation rather than as a literal, so the table below cannot
  carry it and this paragraph has to: `RowCell.Write` dresses each result column as
  `munin-explorer-dataitem-main__column` plus `munin-explorer-dataitem-main__` finished with the
  column key. The keys are a closed set of seven — `code`, `dataCollection`, `dataType`, `period`,
  `source`, `status` and `theme` — so those seven names are as real as any row below, and
  `munin-explorer-dataitem-header__` takes the same completions on the header cells above them. The
  reconciliation reads literals out of `src/`, which is what makes it exact and is also its one
  limit; a name the package builds a piece at a time is named here instead, and adding a column key
  means adding it to this sentence.

  The saved-list view's `desiredData` column is the exception that shows where the boundary runs.
  It is an eighth column in that view and it is **not** one of those keys, because it is not drawn by
  `RowCell.Write` at all — the cell holds an editable field rather than a value, so both halves of
  it are written out as literals and both are rows in the table below. A column that goes through
  the helper belongs in the sentence above; one that does not belongs in the table, and no column
  belongs in both.

  The kilder table's scroll box carries a second interpolated family, and this one exists so a
  stylesheet can ask a question the package is the only thing able to answer. Beside
  `munin-explorer-kilder-scroll` the box wears `munin-explorer-kilder-scroll--cols-` finished with
  the number of header cells the table actually drew — four the picker cannot reach, five where the
  host wired the handover, plus whichever of the ten optional columns are on, so the range is
  `--cols-4` to `--cols-15` and the number is the header's rather than the picker's. **Undrawn it
  costs nothing**, which is the unusual part: it is a modifier on an element whose base class is
  already styled, so a stylesheet with no rule for it leaves the box exactly as it was. That is why
  it could ship before the rule that selects on it, and it is the opposite of the bargain every
  other name here makes. What a rule buys is the one thing `Fhi.Helsedata.Stiler` cannot work out
  for itself: above its 780px breakpoint the box is deliberately `overflow-x: visible`, because the
  page has to be the sticky ancestor for the table's header to pin, so a reader who turns the wide
  columns on runs the table past its box and the host's whole page into a sideways scroll. A rule
  that answers that must not reintroduce a scroll container above the breakpoint, or the header
  stops pinning and the cure is worse than the spill. The measurement, and the rule itself, are
  `Fhi.Metadata-l9l2n.50`.

  **The whole list, name by name.** The paragraphs above pick out the names worth an argument.
  They used to end in hand-written counts, and every one of them had gone stale: `kilde*` had grown
  from nine names to twelve, `kilder*` from three to four, and the eight `munin-explorer-whole*`
  names `VariableView` emits had never been written down here at all. A count is the wrong shape
  for this — it is a claim about `src/` that lives in a file nobody edits when they add a name — so
  the counts are gone and the table below is what carries the claim instead.
  `scripts/assert-class-names-listed.sh` reads every `munin-explorer*` token out of `src/` and
  fails when the two sets differ in either direction: a name emitted and not listed, or a name
  listed and no longer emitted. Adding a name to the component without adding it here is a red CI
  check, which is what a number in a sentence could never be.

  Five kinds, and every name is exactly one of them:

  - `handle` — something else already dresses the element, a Stiler class it also wears or its own
    browser default, so an undefined one costs look and not information. The large majority.
  - `meaning` — carries meaning nothing else carries, so a host without Stiler's rules has to draw
    it. The second sub-list above says what each of these costs undrawn.
  - `id` — not a class at all: the package writes the stem down and completes it with a
    per-instance discriminator at runtime, so `.munin-explorer-source` selects nothing.
  - `prose` — the package writes the name down in a comment and no element wears it.
    `munin-explorer-dataitem-period` is the whole of this kind: the cell it describes is really
    `munin-explorer-dataitem-main__period`. It is listed rather than dropped because
    `assert-sample-css-in-step.sh` reads prose too, so both samples carry a rule for it.
  - `attribute` — not a class and not an id: part of an attribute name.
    `munin-explorer-version` is the whole of this kind, read out of
    `data-munin-explorer-version` on every root element, so `.munin-explorer-version` selects
    nothing and no stylesheet can have a rule for it.

  <!-- class-names:start -->
  | Class name | Kind |
  | --- | --- |
  | `munin-explorer` | handle |
  | `munin-explorer-alert` | handle |
  | `munin-explorer-breadcrumb` | meaning |
  | `munin-explorer-codes` | handle |
  | `munin-explorer-codes__table` | handle |
  | `munin-explorer-container` | handle |
  | `munin-explorer-crumb` | meaning |
  | `munin-explorer-data-list` | handle |
  | `munin-explorer-data-list__header` | handle |
  | `munin-explorer-data-list__item` | handle |
  | `munin-explorer-data-list__item--expanded` | handle |
  | `munin-explorer-data-list__item__row` | handle |
  | `munin-explorer-data-list__item__row--header` | handle |
  | `munin-explorer-data-list__result` | handle |
  | `munin-explorer-dataitem-header` | handle |
  | `munin-explorer-dataitem-header__button` | handle |
  | `munin-explorer-dataitem-header__code` | handle |
  | `munin-explorer-dataitem-header__dataCollection` | handle |
  | `munin-explorer-dataitem-header__dataType` | handle |
  | `munin-explorer-dataitem-header__desiredData` | handle |
  | `munin-explorer-dataitem-header__name` | handle |
  | `munin-explorer-dataitem-header__period` | handle |
  | `munin-explorer-dataitem-header__source` | handle |
  | `munin-explorer-dataitem-header__theme` | handle |
  | `munin-explorer-dataitem-main` | handle |
  | `munin-explorer-dataitem-main__column` | handle |
  | `munin-explorer-dataitem-main__column__text` | handle |
  | `munin-explorer-dataitem-main__desiredData` | handle |
  | `munin-explorer-dataitem-main__expand-icon` | handle |
  | `munin-explorer-dataitem-main__name` | handle |
  | `munin-explorer-dataitem-period` | prose |
  | `munin-explorer-datasamling` | handle |
  | `munin-explorer-datasamling__aside` | handle |
  | `munin-explorer-datasamling__body` | handle |
  | `munin-explorer-datasamling__criteria` | handle |
  | `munin-explorer-datasamling__description` | handle |
  | `munin-explorer-datasamling__header` | handle |
  | `munin-explorer-datasamling__identifiers` | handle |
  | `munin-explorer-datasamling__main` | handle |
  | `munin-explorer-detail` | handle |
  | `munin-explorer-drilldown` | handle |
  | `munin-explorer-filters` | handle |
  | `munin-explorer-filters__active` | handle |
  | `munin-explorer-filters__chip` | handle |
  | `munin-explorer-filters__chip-remove` | handle |
  | `munin-explorer-filters__chosen` | handle |
  | `munin-explorer-filters__count` | handle |
  | `munin-explorer-filters__facets` | handle |
  | `munin-explorer-filters__search` | handle |
  | `munin-explorer-filters__toggle` | handle |
  | `munin-explorer-filters__toolbar` | handle |
  | `munin-explorer-frequency` | handle |
  | `munin-explorer-frequency__fill` | meaning |
  | `munin-explorer-frequency__track` | meaning |
  | `munin-explorer-group` | handle |
  | `munin-explorer-header` | handle |
  | `munin-explorer-header__actions` | handle |
  | `munin-explorer-header__actions-button` | handle |
  | `munin-explorer-kilde` | handle |
  | `munin-explorer-hierarchy` | handle |
  | `munin-explorer-hierarchy__branch` | handle |
  | `munin-explorer-hierarchy__count` | handle |
  | `munin-explorer-hierarchy__icon` | handle |
  | `munin-explorer-hierarchy__icons` | handle |
  | `munin-explorer-hierarchy__leaf` | handle |
  | `munin-explorer-hierarchy__metadata` | handle |
  | `munin-explorer-hierarchy__nodes` | handle |
  | `munin-explorer-kilde__aside` | handle |
  | `munin-explorer-kilde__body` | handle |
  | `munin-explorer-kilde__datasamlinger` | handle |
  | `munin-explorer-kilde__delkilde` | handle |
  | `munin-explorer-kilde__delkilde-description` | handle |
  | `munin-explorer-kilde__delkilde-name` | handle |
  | `munin-explorer-kilde__delkilder` | handle |
  | `munin-explorer-kilde__description` | handle |
  | `munin-explorer-kilde__header` | handle |
  | `munin-explorer-kilde__identifiers` | handle |
  | `munin-explorer-kilde__kildetype` | handle |
  | `munin-explorer-kilde__main` | handle |
  | `munin-explorer-kilder` | handle |
  | `munin-explorer-kilder-scroll` | meaning |
  | `munin-explorer-kilder__count` | handle |
  | `munin-explorer-kilder__count--zero` | handle |
  | `munin-explorer-kilder__expand` | handle |
  | `munin-explorer-kilder__expand-icon` | handle |
  | `munin-explorer-kilder__expand-toggle` | handle |
  | `munin-explorer-kilder__expanded` | handle |
  | `munin-explorer-kilder__name` | handle |
  | `munin-explorer-kilder__select` | handle |
  | `munin-explorer-kodeverk` | handle |
  | `munin-explorer-kodeverk__item` | handle |
  | `munin-explorer-kodeverk__name` | handle |
  | `munin-explorer-kodeverk__reference` | handle |
  | `munin-explorer-list-scroll` | meaning |
  | `munin-explorer-meta` | handle |
  | `munin-explorer-meta__grid` | handle |
  | `munin-explorer-meta__grid-1` | handle |
  | `munin-explorer-meta__grid-2` | handle |
  | `munin-explorer-meta__language` | handle |
  | `munin-explorer-meta__tab` | handle |
  | `munin-explorer-meta__tab--active` | handle |
  | `munin-explorer-meta__tab-content` | handle |
  | `munin-explorer-meta__tabs` | handle |
  | `munin-explorer-pagination` | handle |
  | `munin-explorer-pagination-content` | handle |
  | `munin-explorer-pagination-pages` | meaning |
  | `munin-explorer-pagination-size` | handle |
  | `munin-explorer-period` | handle |
  | `munin-explorer-period__fill` | meaning |
  | `munin-explorer-period__range` | handle |
  | `munin-explorer-period__track` | meaning |
  | `munin-explorer-period__track--ongoing` | meaning |
  | `munin-explorer-results` | handle |
  | `munin-explorer-results__toolbar` | handle |
  | `munin-explorer-retry` | meaning |
  | `munin-explorer-search__clear` | handle |
  | `munin-explorer-selection` | handle |
  | `munin-explorer-skiplink-pagination` | handle |
  | `munin-explorer-source` | id |
  | `munin-explorer-statistics` | handle |
  | `munin-explorer-switch` | handle |
  | `munin-explorer-switch__thumb` | meaning |
  | `munin-explorer-switch__track` | meaning |
  | `munin-explorer-version` | attribute |
  | `munin-explorer-versions` | handle |
  | `munin-explorer-versions__badge` | handle |
  | `munin-explorer-versions__detail` | handle |
  | `munin-explorer-versions__from` | handle |
  | `munin-explorer-versions__name` | handle |
  | `munin-explorer-versions__to` | handle |
  | `munin-explorer-versions__toggle` | handle |
  | `munin-explorer-whole` | handle |
  | `munin-explorer-whole__aside` | handle |
  | `munin-explorer-whole__body` | handle |
  | `munin-explorer-whole__code` | handle |
  | `munin-explorer-whole__description` | handle |
  | `munin-explorer-whole__header` | handle |
  | `munin-explorer-whole__list` | handle |
  | `munin-explorer-whole__main` | handle |
  | `munin-explorer__dropdown` | handle |
  <!-- class-names:end -->

  `Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles` pins that prefix for a closed
  result list, spelling that set out name by name; the panel, drill-in and kilde names are past
  its reach, because nothing is expanded there. For seeing the whole thing dressed, the sample
  hosts' `host.css` stands in for the host stylesheets, divided by comment into which rules stand
  in for which.
- **No `HeadOutlet`.** Not available in the Optimizely host — the component cannot set the page
  title or inject meta tags.
- **Nothing host-specific.** `IHttpContextAccessor`, `Microsoft.AspNetCore.Components.Server.*`,
  EF Core, `EPiServer.*` / `Optimizely.*` and `System.IO` file access are **build errors** in the
  RCL, enforced by `BannedSymbols.txt` and `Microsoft.CodeAnalysis.BannedApiAnalyzers`. That
  enforcement was silently off once, so it has a check of its own:
  `scripts/assert-portability-guard-armed.sh` builds the RCL against a banned symbol and fails
  unless RS0030 is reported. CI runs it on every PR as "portability guard armed".

If a callback parameter is added, note that an `EventCallback` silently serialises to an empty
delegate across a static-SSR to interactive-island boundary — such a mount point has to be fully
interactive.

## Running it

```bash
dotnet run --project samples/LegacyHost
```

Open <http://localhost:5113>. No API key, no database, no login — it reads the public test API
and shows the real catalogue. `samples/ModernHost` (<http://localhost:5087>) mounts the same
component the modern way; LegacyHost is the one that mirrors helsedata's host, so prefer it.

Running it inside helsedata's own site — needed only when the question is styling or
authentication — is covered in [`docs/running-locally.md`](docs/running-locally.md), along with
the two setup traps that cost the most time.

## Build

```bash
dotnet build
dotnet test
```

Requires the .NET 10 SDK. The target framework is set once in `Directory.Build.props`, never in
the individual project files.

`dotnet test` never leaves the machine. One suite is the exception and skips itself unless asked:
a nightly job round-trips live API responses through the contracts and fails on any change in
shape, because the API lives in another repository and can rename a field without anything here
noticing. Run it yourself with

```bash
MUNIN_EXPLORER_LIVE=1 dotnet test --filter Category=ContractDrift
```

See [`docs/contract-drift.md`](docs/contract-drift.md) for what it checks and what to do when it
goes red.

## Installing

One package. The component, the client that feeds it and the types they share all ship together.

It was three for a while — component, client and contracts — so that the component need not
depend on an HTTP stack and a host could substitute its own `IMuninExplorerClient`. That seam is
still here: `IMuninExplorerClient` is an interface, and a host that registers its own
implementation never touches ours. What went away is the part nobody used — three versions that
had to move in lockstep, and a state where the component was installed and the client was not, so
it rendered with nothing behind it.

### Getting it from `Fhi.Helsedata.no`

It goes to `Fhi.Helsedata.no`, helsedata's internal Azure Artifacts feed, and never to nuget.org
— so `dotnet add package` reports the package as not existing until that feed is a source the
restore can see. A host inside helsedata's estate already restores from it. Anyone else adds it to
the **consuming repository's own** `nuget.config`, beside the solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="Fhi.Helsedata.no"
         value="https://pkgs.dev.azure.com/fhi/Fhi.Helsedata/_packaging/Fhi.Helsedata.no/nuget/v3/index.json" />
  </packageSources>

  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="Fhi.Helsedata.no">
      <package pattern="Fhi.Munin.Explorer" />
      <package pattern="Fhi.Helsedata.*" />
    </packageSource>
  </packageSourceMapping>

  <auditSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </auditSources>
</configuration>
```

Then `dotnet add package Fhi.Munin.Explorer`, with credentials in place — see below.

`dotnet nuget add source` looks like the shorter way to the same place and is not. It writes the
*user-level* config, so an authenticated feed becomes a source for every build on the machine —
which [`docs/running-locally.md`](docs/running-locally.md) warns against for this same feed, and
which `scripts/push-packages.sh` goes out of its way to avoid by writing a config of its own. It
also lands in the wrong file whenever the consuming solution's `nuget.config` opens with
`<clear />`, as this repository's does: that discards every source defined further up the chain,
so the restore still fails with the same "package not found" this section exists to prevent, now
with a stale machine-wide source to explain it away.

Three traps come with the feed, and this repository's own [`nuget.config`](nuget.config) spells
all three out:

- **Pin the ids with `packageSourceMapping`.** With two unmapped sources NuGet queries both and
  takes the highest version, not the nearest source. `Fhi.Munin.Explorer` is published only
  internally, so the id is unclaimed on nuget.org — without the mapping above, anyone who
  registers it there at a higher version wins the next restore, and the same goes for
  `Fhi.Helsedata.*`.
- **Keep `<auditSources>` clamped to nuget.org.** NuGet's vulnerability audit queries every
  configured source whatever `packageSourceMapping` says, so a token-less restore against the
  private feed raises NU1900 — which `TreatWarningsAsErrors` then escalates into a build failure.
  helsedata hit exactly this.
- **Keep the token out of config files.** The feed is private, so restore needs an Azure DevOps
  personal access token for the `fhi` organisation, scoped to Packaging (Read). Supply it through
  the [Azure Artifacts Credential Provider](https://github.com/microsoft/artifacts-credprovider) —
  interactively on a developer machine, or via `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS` in CI — so it
  never reaches a file. `dotnet nuget add source --username … --password …` is the path to avoid:
  NuGet can only encrypt that password on Windows, so elsewhere `--store-password-in-clear-text`
  is mandatory and the PAT sits in plain text in a config readable by every process running as
  you, one paste away from being committed. A container build takes it as a BuildKit secret, never
  as a build argument, which persists in image history.

### Registering it

```csharp
// Registration order matters. To call Munin as the signed-in user, register the token provider
// BEFORE AddMuninExplorer — it uses TryAdd, so the anonymous default wins if it goes first and
// the explorer will quietly keep calling without a token.
services.AddSingleton<IMuninExplorerTokenProvider, MyTokenProvider>();
services.AddMuninExplorer(o => o.ApiBaseUrl = "https://runa.munin.skytest.fhi.no");
```

Leave the provider out entirely and calls are anonymous, which is all public metadata browsing
needs — and all the browsing components ever do; `VariableListView` is the one that does not,
because the lists it reads and writes are the signed-in user's own. The variable-list methods
(`GetMyListsAsync` and the seven beside it) are the exception: they call an endpoint the API gates
behind a signed-in explorer user, so with no provider registered every one of them throws on the
401 rather than reporting the user as having nothing saved.

Three things about those eight are worth knowing before writing against them. A call naming a list
the user does not have answers `false` — or `null`, for the paged read — because the API cannot
tell "deleted in another tab" from "somebody else's" and deliberately does not try. The two
batch endpoints take at most `IMuninExplorerClient.MaxVariablesPerBatch` ids, which the client
refuses above rather than splitting: split them yourself with
`ids.Chunk(IMuninExplorerClient.MaxVariablesPerBatch)`, so a failure part-way through leaves you
knowing how far it got.

And `SetMyListDesiredDataAsync` breaks the `false` pattern on purpose, because it is the one write
the API can refuse for what is *in* it: the "Ønskede data" note is capped at 500 characters server
side. It answers a `DesiredDataResult` rather than a `bool`, and a refusal carries the ceiling the
API named — so a caller can tell the reader what to shorten to, and this package never writes the
number down to drift from. A 429 is still thrown, and so is any fault.

`AddMuninExplorer` also calls `AddLogging`, and that is where the component's own diagnostics go.
The browsing surfaces never let an exception out — an unhandled one inside a Blazor circuit takes
the whole page down with it, which on helsedata's Optimizely host means the CMS page and not just
this component — so a failed call becomes a sentence in the alert region and nothing more on
screen. It used to become nothing at all anywhere else either, which made a fault on a host's own
server diagnosable only by elimination. Every one of those places now writes the exception through
`ILogger<T>` first, at `Error`, or at `Warning` where the outcome is an expected one such as a 429.
Filter on `Fhi.Munin.Explorer.Blazor.*`, `Fhi.Munin.Explorer.Client.*` and
`Fhi.Munin.Explorer.State.*`.

Two things follow that are worth stating. `AddLogging` is idempotent and `TryAdd`-based inside, so
a host that has already configured logging keeps every provider, filter and minimum level it set —
this adds a default factory for the host that has none, and takes nothing from the host that has
one. And the logger is resolved with `GetService` rather than injected, so a component mounted in a
host that never called `AddMuninExplorer` still renders: it simply writes nothing. What comes back
is wrapped, so a provider that throws — a file sink on a full disk, say — costs a log line rather
than the page, since `Logger<T>` rethrows a provider's failure and the call sites are inside the
catches that keep the circuit up. The message templates name no component, because the category
already is the component's type; they carry ids, page numbers and the like, never a URL, a token, a
response body or anything the reader typed — the user's access token reaches the wire through `BearerTokenHandler`'s
`Authorization` header, which no exception message here repeats.

### What a host mounts

`VariableExplorer` is the whole variabelutforsker: the search, the reader's own variable lists
behind the second tab, and both of them in the address bar. `Language` and `IsAuthenticated` are
all it takes.

```html
<component type="typeof(VariableExplorer)" render-mode="Server"
           param-Language="@("no")" param-IsAuthenticated="@(User.Identity?.IsAuthenticated == true)" />
```

Opening a link restores the search, the facets, the sort, the page and the open variable, and every
change the reader makes updates the address bar. There is no glue to write — no wrapper component,
no query parsing, no `history.replaceState`. Which tab is open is deliberately *not* in the link: a
shared URL that opened on the sender's Variabelliste would be an empty page for everybody else.

`KildeExplorer` is the kildeutforsker's equivalent, and `Language` is all it takes:

```html
<component type="typeof(KildeExplorer)" render-mode="Server" param-Language="@("no")" />
```

The open kilde and the order the list is in go in the address bar, and a link restores both. It is
much the smaller of the two, because Kelda carries less — no personal lists and no pager, so
`?kilde=` and `?sort=` are the whole of what it owns and the rest is component state that goes away
on refresh. `?sort=` is omitted while the list is in the order the catalogue sent, so a link made
before the list could be sorted still means what it did.

An open kilde's collection section loads its hierarchy separately: delkilder, datasamlinger and
variabelgrupper appear as nested lists with native disclosures, initially collapsed. Tab visits
each summary; Enter or Space toggles it. Descriptions and validity periods remain in a separate
disclosure below the hierarchy. `KildeView` owns this presentation, so it is the same when reached
through either explorer or mounted directly. Register the client with `AddMuninExplorer` before
mounting `KildeView`. `KildeHierarchyView` can also be mounted with `KildeId` and `Language`.
The new hierarchy class names are listed above;
their helsedata styling is tracked in `Fhi.Metadata-wihod` and is not supplied by this package.

Each row carries a node icon in front of its name — a folder on a delkilde, one glyph per
datakategori on a datasamling, and nothing on a variabelgruppe, which is the mapping Kelda's own
tree draws, legacy category slugs and all. `ShowNodeIcons` turns them off on `KildeHierarchyView`
and on `KildeView`; the variable counts are untouched either way, and the package remembers no
choice of its own for the reason `LevelLines` does not. The glyphs are inline `<svg>` marked
`aria-hidden`, so a datasamling's categories are said in words in a `screenreader-only` span
instead of twice. Each one is `1em` in `currentColor` and wears
`munin-explorer-hierarchy__icon` with a `data-node-icon` naming its datakategori — `PHDR`, `EINS`,
`other` and the rest, plus `kilde` for the folder — which is the hook a stylesheet colours them
through. Undefined, they draw at text size in the text colour, so what a host without the rules
loses is the colour that tells two categories apart at a glance and not the categories themselves.

Four things are worth knowing before mounting one.

- **The render mode has to be interactive** — `render-mode="Server"`, never `ServerPrerendered`;
  in a modern host, `@rendermode` with `prerender: false`. Both components throw on initialisation
  otherwise, because the failure they replace is invisible: prerendered, the page renders and the
  URL simply never follows the view.
- **Your own parameters are safe.** Each component reads and rewrites only the keys it owns —
  `ExplorerUrlState.QueryKeys` for the variable explorer, `?kilde=` and `?sort=` for the
  kildeutforsker — and carries everything else through untouched. `DeclinedKeys` keeps one of ours as well, for a page
  that already means something else by `?page=`; a declined key is left where it is rather than
  overwritten.
- **`KildeExplorer` needs `VariableExplorerPath`** to offer the handover to the variable
  explorer, because only the host knows where it mounted one. Leave it out and the selection column
  is not drawn at all — which is deliberate, and the right answer for a CMS host that cannot set it
  at all: a column whose button lands on a page that host may not have would be worse than no
  column. It is relative to your application rather than to the domain — `"variabler"` and
  `"/variabler"` mean the same page, and a path base is kept either way — and a full URL is taken
  as given. A path rather than a callback on purpose: an `EventCallback` handed to an interactive
  component by a statically rendered parent serialises to an empty delegate.
- **The two static blocks over an open kilde are off unless you ask for them.**
  `ShowAccessAndPrices` — declared on `KildeSearch` and on `KildeExplorer` — draws
  "Kriterier for tilgang til data" and "Priser", both of which send the reader to helsedata.no.
  That is the route a researcher browsing Munin's own catalogue needs, and a duplicate of pages
  helsedata publishes itself, so it defaults to `false`: a host embedding the explorer inside a
  site that already covers access and pricing gets neither block without doing anything. Set it
  to `true` on a host of your own. Nothing else on the kilde page moves with it — the variable
  count, the metadata, the datasamlinger and the sidebar are drawn either way.

Owning the address bar — or the page furniture — yourself is still supported: `VariableSearch`,
`VariableListView`, `VariableListFilters` and `KildeSearch` stay public underneath, so a host that
wants the two variable surfaces on separate pages, its own tabs around them, or the kilde list with
no `?kilde=` at all, mounts them itself and builds the query with `ExplorerUrlState.Parse` /
`.ToQueryString`. Mounted apart they still share the circuit's `VariableListState`, so a variable
saved on one is in the other without a refetch — and a kilde ticked in `VariableListFilters`
narrows `VariableListView` through that same holder, which is why the two need no wiring between
them but do need to be on one circuit. `ExplorerUrlState.QueryKeys`
names every parameter it reads and writes, the filter's own included, so you can tell ours from
yours. Do that and three details are yours to get right — the interactive render mode above, a path
built from `PathBase + Path` rather than a literal (identical locally, wrong behind a reverse
proxy), and `replaceState` rather than `pushState`.

### Reading which version is deployed

Every root element this package renders carries the package version as
`data-munin-explorer-version`. From the browser's console, on any page that mounts one, signed in
or not:

```js
document.querySelector("[data-munin-explorer-version]").dataset.muninExplorerVersion
// → "0.1.0-alpha.8+6e4c…"
```

The value is the assembly's `AssemblyInformationalVersion`: the released version, its prerelease
suffix, and — where the build recorded one — the commit behind the `+`.

It is an attribute rather than an endpoint, a header or a static asset because those are all
things a host has to opt into, and this exists precisely for the deployment nobody can ask
questions of. It has to be read from the rendered DOM rather than from `curl`: an interactive
mount is not prerendered, so the first HTML response carries a marker comment and nothing else.

### Writing the token provider for a Blazor Server host

Two things about Blazor Server make the obvious implementations wrong, and both fail quietly
rather than loudly:

- **`IHttpContextAccessor` returns null.** Circuit activity arrives over a WebSocket, so there is
  no `HttpContext` for anything after the connection is established. A provider written against
  it does not throw — it finds no token and calls anonymously, which reads as "Munin forgot who
  I am" rather than as a bug in the host.
- **The provider is a singleton, so it cannot hold a user.** `IHttpClientFactory` builds the
  handler pipeline in its own scope and reuses it across every caller for about two minutes.
  Whatever the provider captures at construction is shared with everyone who calls afterwards —
  which is how one person's token ends up on another person's request.

So the provider has to ask *per call* which circuit it is answering for.
[`samples/LegacyHost/Authentication/`](samples/LegacyHost/Authentication/) has a working
implementation of the documented pattern — an `AsyncLocal` holding the circuit's service
provider, set and cleared around inbound activity by a `CircuitHandler`. That sample host is a
legacy Blazor Server + MVC app on purpose, the same shape as helsedata's Optimizely CMS, so it
can be copied rather than translated.

The part that is load-bearing is `AsyncLocal` rather than a field: work forked from two circuits
runs on independent execution contexts, so neither can observe the other's token. That is what
the concurrency test covers, and swapping the `AsyncLocal` for a plain static field is what makes
it fail.

The explicit clear afterwards is deliberately *not* claimed to be doing the heavy lifting.
An `async` method runs against a copy of the `ExecutionContext`, so the value is already restored
for the caller when the call returns — removing the clear does not fail any test here. It is kept
as insurance for the day someone makes that method synchronous, which would drop the automatic
restore without any visible sign.

## Releasing

Publishing is triggered by a tag, never by a merge:

```bash
git tag v0.2.0 && git push origin v0.2.0
```

`.github/workflows/release.yml` derives the version from the tag, assembles the changelog,
builds, tests, packs, asserts the package shape and pushes the one package, `Fhi.Munin.Explorer`,
to `Fhi.Helsedata.no`, the Azure Artifacts feed helsedata's own projects already restore from. The
package is internal, not public: nothing goes to nuget.org.

The changelog comes first, and the order is load-bearing: `PackageReleaseNotes` is that version's
assembled section, and the GitHub release for the tag carries the same text, so packing before
assembling would stamp a version with notes nobody had written yet. That was the state until
`Fhi.Metadata-l9l2n.44` — assembly was a documented manual step, eight versions shipped without it
being run once, and the notes on the feed were a link to auto-generated commit titles.

The section is committed on a `changelog/v<version>` branch, because the `MainRules` ruleset
requires a pull request for `main` and has no bypass actors: an unattended push is refused whoever
makes it, and a credential that could bypass it is one this public repository deliberately does not
hold. Re-running a tag is safe — the assembler finds the section already there, writes no
duplicate, and leaves the fragments queued for the next release alone.

**Opening that pull request is a manual step, and it is one somebody has to remember.** The
workflow used to attempt it, but FHIDev withholds pull-request permission from Actions, so the call
failed on every release; it now prints the `gh pr create` command in the run summary instead of
making a call that cannot succeed. Until the branch is merged the fragments stay queued, and the
next release would publish them under its own version.

Forgetting is caught rather than trusted: the release **refuses to run** while a `changelog/v*`
branch exists whose version has no section in `CHANGELOG.md`. That check sits before the feed push,
so a failure has published nothing and the run can simply be re-run once the branch is merged.

The workflow refuses to publish a tag whose commit is not on `main`, a tag that is not a clean
`vMAJOR.MINOR.PATCH`, and a build whose packed version disagrees with the tag. The feed does allow
a version to be deleted, but that is not a way back: anyone who restored it keeps what they got,
so a version number that has gone out is spent whether or not the artefact is still there.

`scripts/push-packages.sh` retries a push that fails for reasons of its own — five attempts, then
it gives up — so **re-running the workflow** is the answer when one does. The re-run asks the feed
first whether this version is already there and refuses to push over it, so it either completes
the push that never landed or stops because the version is already out.

"Already out" is not always a reused tag, and the run cannot tell the difference. If the first
run's push landed but the job died after it — the `Create the GitHub Release` step failed, the
20-minute `timeout-minutes` fired, the runner dropped — then there is nothing left to publish, and
the re-run still stops and still says to tag a new version. Read that message rather than obeying
it: look at what is on the feed first, and spend a new version number only if what is there is not
the build you meant to ship. A push coming back "already exists" is treated as our own attempt
landing unseen and reported as a success, and the pre-flight is what keeps that from excusing a
reused tag — it refuses a version the feed says is there, unless it cannot reach the feed to ask.
A query that errors or times out answers "not published", because a failed query must never be
able to skip a push; the run then pushes, is told "already exists", and exits green. So a green
re-run is not by itself proof that *this* run's push landed — check the run log for whether the
pre-flight got an answer, and check the feed for which build is on it.

Requires the secret `ADO_PACKAGING_TOKEN`: an Azure DevOps personal access token for the `fhi`
organisation, scoped to Packaging (Read & write) and nothing more. Add it under
Settings → Secrets and variables → Actions.

A token carries the identity of whoever created it, so publishing stops when it expires or that
account closes — worth knowing when it is time to rotate. An Entra token authenticates against the
feed just as well, so this can move to federated OIDC once a service principal is a member of the
Azure DevOps organisation; the push script takes whatever credential it is given and does not
inspect it.

To check the package shape yourself before tagging:

```bash
dotnet pack -c Release -o artifacts
./scripts/assert-package-contents.sh artifacts
```

Versions stay on `0.x` until the helsedata POC is wired up and the API surface has stopped
moving — `1.0.0` is a stability promise, and a version that consumers have restored cannot be
walked back.

## Changelog

`CHANGELOG.md` is the released record. Unreleased changes live one file per change in
[`changelog.d/`](changelog.d/README.md) — a shared changelog file is a merge conflict on every
parallel branch, a new file is never one. A PR touching `src/` needs a fragment, and CI says so.

Pushing a `v*` tag folds the fragments into a version section; nobody runs the assembler by hand.
See [`changelog.d/README.md`](changelog.d/README.md) for what the tag does, what a re-run does,
and why the eight `0.1.0-alpha.*` sections were all written on one day.

## Issue tracking

Work is tracked in the Munin beads workspace, not in this repository's issues — epic
`Fhi.Metadata-l9l2n`. Pull requests close their bead with the cross-repository form,
e.g. `Closes FHIDev/Munin#1234`.

GitHub Issues here are open for external consumers to report problems.

## Licence

MIT.

### Third-party notices

The node-icon geometry in `Blazor/DataCategoryIcons.cs` is copied from
[lucide](https://lucide.dev), the icon set Kelda draws the same datakategorier with. Copied rather
than depended on because this package ships no asset bundle and takes no front-end dependency.

```
ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

Two of the glyphs copied — `database` (PHDR) and `smartphone` (WELA) — are lucide icons derived
from [Feather](https://feathericons.com), and carry its licence as well:

```
The MIT License (MIT)

Copyright (c) 2013-present Cole Bemis

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
