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

## Layout

| Project | What it is |
| --- | --- |
| `src/Fhi.Munin.Explorer` | The one package. Three folders, one per namespace. |
| `src/Fhi.Munin.Explorer/Blazor` | The components a host renders. |
| `src/Fhi.Munin.Explorer/Contracts` | DTOs and the client interface. |
| `src/Fhi.Munin.Explorer/Client` | Typed `HttpClient` implementation + `AddMuninExplorer()`. |
| `samples/ModernHost` | Blazor Web App — the everyday development host. |
| `samples/LegacyHost` | Legacy Blazor Server + MVC — mirrors helsedata's Optimizely host. |
| `test/Fhi.Munin.Explorer.Tests` | bUnit + xUnit. |

Both sample hosts exist on purpose. helsedata's production site runs **legacy** Blazor Server
(`AddServerSideBlazor()` + `MapBlazorHub()`), mounting components inside MVC views with the
`<component>` tag helper. A component that only ever ran in a modern Blazor Web App can break
there in ways that never show up in development.

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
    `munin-explorer-pagination-content`, and their rules join the rest of the prefix in Stiler
    under `components/munin-explorer/`. They are not in 0.1.13, which shipped before this rename:
    they ship in **0.1.14**, and on 0.1.13 itself the pager renders at browser defaults exactly as
    it did before. Inside helsedata nothing changes either way — their `variables-pagination` rules
    are still in `variables.css`, now unused.

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
    `skiplink-pagination` is `munin-explorer-skiplink-pagination` now, and Stiler **0.1.14**
    carries its rule unscoped. A Stiler-only host is down to no rules of its own, not to one.

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

  Every name in the `munin-explorer` prefix is ours. That is worth saying because it used not to
  be: under the old prefix six names were helsedata's — the container, the results column, the
  header with its `__actions` and `__actions-button`, and the dropdown — and the prefix itself was
  no guide to which was which, so a reader had to check each one against a list. There is no longer
  a category to check against. The `THEIRS` allowlist in `scripts/assert-sample-css-in-step.sh` is
  empty by construction, and what these names cost a host is now the same question everywhere: a
  host on Stiler 0.1.13 or later has rules for them — 0.1.14 for the pager and its skip link, which
  were renamed after 0.1.13 shipped, and none at all yet for `munin-explorer-retry` — any other host
  draws whatever it wants drawn, and the sub-lists below are about how much drawing nothing costs.

  - Handles, where something else already dresses the element — a Stiler class it also wears, or
    its own browser default — and the name is there so a host or a test can find that part of the
    component in the page: `munin-explorer` (the root `<section>`), `munin-explorer-filters`,
    `munin-explorer-detail`, `munin-explorer-drilldown`, `munin-explorer-kodeverk*`,
    `munin-explorer-codes*`, `munin-explorer-group`, the `munin-explorer-kilde*` names in
    `KildeView`, the `munin-explorer-datasamling*` ones in `DatasamlingView`, the
    `munin-explorer-whole*` ones in `VariableView`, and the `munin-explorer-kilder*` names in
    `KildeExplorer` — the kilde list's table, the checkbox column in front of it, the button that
    opens a row and the columns that hold a number. The samples style them for arrangement — the
    root as a grid at desktop width, `-filters`, `-detail`, `-drilldown`, `-kodeverk*` and
    `-codes*` for spacing, indentation and a rule between rows, the kilde, datasamling and variable
    views' name block, main column and sidebar as one page layout under three prefixes, the kilde
    list as a table with its counts right-aligned — and `munin-explorer-group` is now the space
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
    `gap`, so nothing trails the last button, and buttons that shrink and wrap their own labels
    rather than the row breaking apart at the next longer translation. Both sample stylesheets carry
    it, and it is in `Fhi.Helsedata.Stiler` from the release that follows PR 39046.
  - Names that carry meaning nothing else carries, so a host without Stiler's rules has to draw
    them itself: `munin-explorer-crumb` carries the link affordance for a trail step, which is a
    `<button>` — the kilde step of the panel's kilde trail, and every step of the hierarchy trail
    over the results — and without it a trail reads as plain text with no sign it can be pressed;
    `munin-explorer-breadcrumb` with its `__clear` is that hierarchy trail's own wrapper, where the
    chevrons between the steps come from and where the × that empties the hierarchy sits, and an
    undrawn one is a numbered list with a stray × after it; and inside the `munin-explorer-period*`
    wrapper, `__track`, `__fill` and `__track--ongoing` are the period bar itself — only its width
    comes from an inline style, so an undrawn bar renders as nothing at all. The period is still
    legible without it, because the dates are next to it in words, in `__range`. Last in this list
    is `munin-explorer-retry`, on the two retry buttons in the alert region: it draws their inert
    state, and it is the one name here that **no Stiler version carries yet, 0.1.14 included** —
    tracked as `Fhi.Metadata-x6vqc`. The buttons are never `disabled`, because that would drop the
    focus of the reader who just pressed one to `<body>`, so `aria-disabled` is what says the offer
    is spent; the alert region deliberately carries no class, so neither the pager's nor the filter
    panel's `[aria-disabled]` rule reaches in, and without one of its own a button that does nothing
    looks exactly like one that works. That is a WCAG 2.1 AA problem rather than a cosmetic one, and
    it is the `skiplink-pagination` shape: both sample stylesheets have the rule, so the guard is
    green while the host the prefix exists for gets nothing.

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

  Four kinds, and every name is exactly one of them:

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

  <!-- class-names:start -->
  | Class name | Kind |
  | --- | --- |
  | `munin-explorer` | handle |
  | `munin-explorer-account-link` | handle |
  | `munin-explorer-account-link__actions` | handle |
  | `munin-explorer-alert` | handle |
  | `munin-explorer-breadcrumb` | meaning |
  | `munin-explorer-breadcrumb__clear` | meaning |
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
  | `munin-explorer-filters__count` | handle |
  | `munin-explorer-filters__facets` | handle |
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
  | `munin-explorer-kilder__count` | handle |
  | `munin-explorer-kilder__name` | handle |
  | `munin-explorer-kilder__select` | handle |
  | `munin-explorer-kodeverk` | handle |
  | `munin-explorer-kodeverk__item` | handle |
  | `munin-explorer-kodeverk__name` | handle |
  | `munin-explorer-kodeverk__reference` | handle |
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
  | `munin-explorer-pagination-pages` | handle |
  | `munin-explorer-pagination-size` | handle |
  | `munin-explorer-period` | handle |
  | `munin-explorer-period__fill` | meaning |
  | `munin-explorer-period__range` | handle |
  | `munin-explorer-period__track` | meaning |
  | `munin-explorer-period__track--ongoing` | meaning |
  | `munin-explorer-results` | handle |
  | `munin-explorer-retry` | meaning |
  | `munin-explorer-search` | handle |
  | `munin-explorer-search__clear` | handle |
  | `munin-explorer-selection` | handle |
  | `munin-explorer-selection__explore` | handle |
  | `munin-explorer-skiplink-pagination` | handle |
  | `munin-explorer-source` | id |
  | `munin-explorer-statistics` | handle |
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

### Shareable URLs

Mount `VariableExplorerWithUrlState` or `KildeExplorerWithUrlState` in place of the explorer itself
and a link carries the view: opening one restores the search, the facets, the sort, the page, the
open variable and the open kilde, and every change the reader makes updates the address bar. There
is no glue to write — no wrapper component, no query parsing, no `history.replaceState`. The
explorer's own parameters — `Language`, `IsAuthenticated`, `HeadingLevel` — are set on the wrapper
exactly as they would be on the explorer itself.

```html
<component type="typeof(VariableExplorerWithUrlState)" render-mode="Server" param-Language="@("no")" />
```

Three things are worth knowing before mounting one.

- **The render mode has to be interactive** — `render-mode="Server"`, never `ServerPrerendered`;
  in a modern host, `@rendermode` with `prerender: false`. Both components throw on initialisation
  otherwise, because the failure they replace is invisible: prerendered, the page renders and the
  URL simply never follows the view.
- **Your own parameters are safe.** Each component reads and rewrites only the keys it owns —
  `ExplorerUrlState.QueryKeys` for the variable explorer, `?kilde=` for the kildeutforsker — and
  carries everything else through untouched. `DeclinedKeys` keeps one of ours as well, for a page
  that already means something else by `?page=`; a declined key is left where it is rather than
  overwritten.
- **`KildeExplorerWithUrlState` needs `VariableExplorerPath`** to offer the handover to the variable
  explorer, because only the host knows where it mounted one. Leave it out and the selection column
  is not drawn at all. It is relative to your application rather than to the domain — `"variabler"`
  and `"/variabler"` mean the same page, and a path base is kept either way — and a full URL is
  taken as given. A path rather than a callback on purpose: an `EventCallback` handed to an
  interactive component by a statically rendered parent serialises to an empty delegate.

Owning the address bar yourself is still supported and unchanged: mount `VariableExplorer` directly
and build the query with `ExplorerUrlState.Parse` / `.ToQueryString`. `ExplorerUrlState.QueryKeys`
names every parameter it reads and writes, the filter's own included, so you can tell ours from
yours. Do that and three details are yours to get right — the interactive render mode above, a path
built from `PathBase + Path` rather than a literal (identical locally, wrong behind a reverse
proxy), and `replaceState` rather than `pushState`.

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

`.github/workflows/release.yml` derives the version from the tag, builds, tests, packs, asserts
the package shape and pushes the one package, `Fhi.Munin.Explorer`, to `Fhi.Helsedata.no`, the
Azure Artifacts feed helsedata's own projects already restore from. The package is internal, not
public: nothing goes to nuget.org.

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

## Issue tracking

Work is tracked in the Munin beads workspace, not in this repository's issues — epic
`Fhi.Metadata-l9l2n`. Pull requests close their bead with the cross-repository form,
e.g. `Closes FHIDev/Munin#1234`.

GitHub Issues here are open for external consumers to report problems.

## Licence

MIT.
