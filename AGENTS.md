# Conventions

Rules for anyone working in this repository, human or otherwise. The architectural rules the
components must follow — no `@page`, no `@rendermode`, no CSS, nothing host-specific — live in
[`README.md`](README.md) under "Rules the components follow" and are enforced by
`BannedSymbols.txt`. This file covers the conventions a compiler cannot check.

## Identifiers are English

**Code identifiers in this repository are English.** Types, members, parameters, locals, test
helpers, file names.

```csharp
// yes
public Task<Page<VariableSummary>> SearchVariablesAsync(string? search, ...)
private async Task SortAsync(SortField field)

// no
public Task<Side<VariabelSammendrag>> SokVariablerAsync(string? sok, ...)
private async Task SorterAsync(Sorteringsfelt felt)
```

Early work here used Norwegian identifiers, following Munin's own client code —
`Variabelutforsker`, `SokVariablerAsync`, `HentTokenAsync`, `Side<VariabelSammendrag>`. They
were all renamed under `Fhi.Metadata-osxfx`, before the first publish to the feed, because
several of them were public API: renaming was free then and a breaking change for helsedata
afterwards. **There is no Norwegian half left to add to.**

The reason is narrow and specific to this package rather than a general preference. This is a
library published to `Fhi.Helsedata.no`, helsedata's Azure Artifacts feed, and consumed by
teams outside FHI's Norwegian-speaking core — its public surface is the API other people
program against, and `SokVariablerAsync(sok:)` is not a signature a consumer can guess at.
Munin's own code has no such audience, which is why the convention differs there and why
copying it across was a reasonable mistake to make.

## Norwegian stays where it is read, not called

Norwegian is correct, and required, for:

- **User-facing strings** — labels, status messages, error text. The component is bilingual
  (`nb`/`en`); Norwegian copy belongs in the text records, not spread through markup.
- **Domain terms with no honest translation** — `kilde`, `datasamling`, `delkilde`,
  `variabelgruppe`, `kildetype`, plus `kodeverk` and the kinds it comes in
  (`helsefagligKodeverk`, `administrativtKodeverk`, `kildekodeverk`). These are names of things
  in the Norwegian health-metadata catalogue, not English concepts wearing a Norwegian coat.
  Keep them as they are, inside otherwise-English identifiers: `KildeId`, `DatasamlingCount`,
  `GetKildeHierarchyAsync`, `SearchByVariabelgruppeAsync`. A "translation" like
  `SourceCollection` invents a term nobody uses and breaks the link to the API's own field
  names.

  Their Norwegian plurals come along with them, because that is what the API calls the
  collections: `Kilder`, `Delkilder`, `Datasamlinger`, `Variabelgrupper`, `KildeTyper` — not
  `Kildes`.

  The list is short on purpose. Everything else has an honest English equivalent and uses it,
  including the ones that look Norwegian-only at a glance: `dataansvarlig` is `DataController`
  and `databehandler` is `DataProcessor` (the GDPR terms), `lovverk` is `LegalBasis`,
  `gradAvPersonidentifikasjon` is `PersonIdentificationLevel`, `kortNavn` is `ShortName`.

Note that a DTO property's C# name is free to differ from its wire name, because every one of
them carries an explicit `[JsonPropertyName]`. Renaming a property is therefore not a contract
change — `[JsonPropertyName("sistOppdatert")] public DateTimeOffset LastUpdated` is the normal
shape here, and the JSON side must keep spelling it Munin's way.

Changelog fragments are **English only** — see [`changelog.d/README.md`](changelog.d/README.md),
which explains why this repository deliberately differs from Munin's bilingual pair.

## What an explicit null does

Munin sets no `DefaultIgnoreCondition`, so it writes `"key": null` rather than leaving the key
out — `healthDcatScore` arrives that way on every row of `GET /api/explorer/kilder`. A contract
property therefore has to answer a null, and which answer it gets is decided by how it is
declared. `ExplicitNullTest` sweeps every deserialised property and fails on one that fits none
of these.

| Declared as | An explicit null reads as | Mechanism |
| --- | --- | --- |
| `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `IReadOnlyDictionary<string, T>` | the empty collection | `NullAsEmptyCollections` |
| `string` | `""` | `NullAsEmptyStrings` |
| `string?`, `T?` | `null` | the annotation |
| `int`, `bool`, `Guid`, an enum | **nothing — the whole read throws** | `System.Text.Json` |

**The last row is a decision, not an omission.** `""` and `[]` render as the absence they are, so
tolerating a null there costs a reader one blank cell. `0`, `false`, `Guid.Empty` and the zeroth
enum member render as facts the payload never stated — a kilde with `0` datasamlinger that has
fourteen, a row whose link goes nowhere — and a reader believes those. It is the sentinel
`Fhi.Metadata-6r6rf` spent a fix removing, in a new coat. So a null count costs the whole list and
the reader is told "Kunne ikke laste kilder nå", which is at least true.

The API side backs that up: every one of those is a primary key, a `NOT NULL` column or a
`Count()` aggregate, so a null in one is a broken payload rather than a shape Munin can produce.
The nullable columns it does have — `kortNavn`, `gyldigFra`, the timestamps
(`Fhi.Metadata-se0by`), `kildetype` and every `effectiveKildetype` projected from it
(`Fhi.Metadata-l9l2n.61`) — are already annotated `?` here, and that is when to reach for **must be
nullable**: because Munin's column is, not to make a value type tolerate a null.

The strings are the ones worth machinery, because they are the only shape that fails *silently*.
`System.Text.Json` writes the null over the `= ""` initialiser, the fetch's try/catch sees
nothing, and the first read throws while rendering — which on a Blazor Server host takes the
circuit and the page this package is mounted in. Same likelihood as the others, a different blast
radius. `RespectNullableAnnotations` is the other answer .NET offers and was rejected: it makes a
null string throw, which trades the dead page for the whole list disappearing over one blank name.
(`Fhi.Metadata-o355u`)

## The API names a datatype, not this package

**The datatype vocabulary belongs to the API.** The codes are editable master data on Munin's
side, so a table of names written here freezes a snapshot in one language and drifts the moment
someone edits a definition, in a package other people ship and cannot patch. Every surface that
shows a datatype word — the result rows in `VariableSearch` and `VariableListView`, the datatype
facet in the filter panel — therefore renders the name the filters endpoint sent, and the shipped
`Texts.DataTypeNames` table is asked for one thing only.

That one thing is a **legacy stored spelling**. Variables predating the codes hold words rather
than numbers — `"String"`, `"tekst"`, `"Integer"` — and the filters endpoint echoes such a word
back as the facet's `displayName` whatever `Accept-Language` asked for, so code `1` arrives named
"String" on a Norwegian call. `Texts.DataTypeAliases` maps those spellings, in either language, to
their code; `Texts.CanonicalDataTypeCode` is how a stored value finds its facet, and
`Texts.NormalizeDataTypeDisplayName` is how such a word becomes the shipped table's name for that
code **in the reader's own language** — "Streng" under `no`, "String" under `en`. A name the alias
table has never heard of reaches the page exactly as the API sent it.

The rule that keeps the surfaces agreeing: **canonicalise a stored value before looking it up, and
normalise every name after.** Skipping the first step is what made a row read "String" beside a
facet reading "Streng"; skipping the second is what made the facet read a bare code. A datatype
word rendered any other way is a bug.

`Texts.DataTypeLabel` is the one exception, and it is **every** surface's fallback for a code no
API name reached — `VariableView`'s panel, which holds the stored code alone; a facet from an API
predating `displayName`; and either view's rows, whose names arrive from the filters endpoint and
so are missing whenever that call hangs, fails, or answers without one. The fallback has to be the
same on all of them: a row falling back to the bare code beside a facet falling back to the table
put "1" and "Streng" on one screen for one datatype, which is the defect this whole section exists
to prevent. Never fall back to the raw stored value — canonicalise first, so a legacy spelling and
its code land on the same word.

One surface still escapes the rule: `VariableSearch`'s expanded row panel draws `DataType` out of
`AdditionalProperties` through `CatalogueProperties`, in the catalogue's own vocabulary, so a
Norwegian reader can see "Heltall" in the row and "Datatype: Integer" in the panel below it.
`VariableView` excludes the key for exactly this reason (`DrawnInTheSidebar`); the search panel does
not, and closing that is its own bead. (`Fhi.Metadata-l9l2n.49`)

**Kildetype is the same vocabulary problem with a smaller table, and the same answer.**
`/api/explorer/filters` resolves `kildeTyper[].displayName` from the Kilde-scoped Kildetype
PropertyDefinition and follows `Accept-Language` (`Fhi.Metadata-0mjhi`), so that is the authority
and `Texts.KildeTypeNames` is the fallback rather than the source.
`Texts.KildeTypeNameFromApi(value, apiName)` is the one place the preference is written down —
named apart from its neighbour `Texts.KildeTypeLabel`, which prefers the shipped table, because a
caller picking between two public members by name alone is how the two-word panel happened. Every
kildetype word inside `VariableSearch` goes through it: the facet button from its own facet, and
the kilde group heading and the open row's kilde trail through
`VariableSearch.KildeTypeNameFromApi(facets, value)`, which finds the facet by value. That helper
takes the payload rather than reading `_facets`, so a heading resolves out of the very
`FilterOptions` the button above it was built from; the trail is the one caller with no facets in
scope and passes the field explicitly. The table answers two payloads and no others: an API that
predates the resolved label and echoes the enum name — `SentraltHelseregister`, the value again bar
its casing — and one that sends no `displayName` at all. Under the table a kildetype that has a
token keeps it rather than reading "Ikke oppgitt", so a kildetype nothing has a word for reads as
its own name on the facet, the heading and the trail alike — the three cannot fall back apart,
which is the defect the bead was opened for. A kilde carrying no kildetype at all has no token to
keep, and its group heading is the one "Ikke oppgitt" in this vocabulary. Checked
against runa on 2026-09-10 — `api/explorer/kilder/egenskaper`, whose `Kildetype` `optionsJson` is
the seed itself — the two agree word for word on all eight values in both languages, so the switch
changed no visible text; what it bought is an edit to the master data reaching the page, and the
first kildetype Munin adds reading the same on the facet button and the heading beneath it
(`Fhi.Metadata-1b0ag`).

The components a host mounts on their own — `KildeView`, `VariableView`, `DatasamlingView` — and
the kildeutforsker's own facets are outside this: none of them fetches the filters endpoint, so
`Texts.KildeTypeLabel` by value is all they have. Handing them a vocabulary is a public API change
— a parameter each host must fill, or a filters call a mounting host did not ask to pay for — so it
is filed as `Fhi.Metadata-vcxoc` rather than left as a sentence here, and it waits on whatever
`Fhi.Metadata-6qy6l` settles for datatyper.

## Comments

Comment the **why**, never the **what**. If a reader can derive it from the signature, delete it.

**The ceiling is three lines.** Going past it needs knowledge a reader cannot recover from the
code itself: a race, a non-obvious invariant, a workaround for behaviour outside this repository.
Four lines is already over budget, and length is earned by unrecoverable knowledge rather than by
thoroughness.

**Incident history goes in the bead, not the file.** A bead id — `(Fhi.Metadata-3b1l4)` — beats
twenty lines of narrative that rots the moment the thing it describes changes, and no reader of
the line below it needs the history to read the line. The bead and the PR description are the
archive. A file people open on every visit is not. When the knowledge is something *everyone*
needs rather than everyone touching one file, it belongs in this document instead — that is what
these sections are for.

**The published API surface is the exception.** XML docs on the public types and members of
`src/Fhi.Munin.Explorer` — `<summary>`, `<remarks>`, `<param>` and `<returns>` alike, since
`GenerateDocumentationFile` is on and `lib/<tfm>/Fhi.Munin.Explorer.xml` ships inside the package —
are read by host developers who have the package from `Fhi.Helsedata.no` and its IntelliSense, and
not this repository. `VariableSearch.SearchChanged` spending a `<remarks>` on how an
`EventCallback` serialises to an empty delegate across a static-SSR boundary is length earned: a
consumer cannot reconstruct it from the signature and cannot open the file to find out. Being long
is not what the exception licenses; having a reader who has only the package is. Internal types
have no such reader and stay under the ceiling.

**No `<summary>` / `<param>` / `<returns>` that restates the name.** Document constraints only —
units, null semantics, what a host must do, what a null means.

## Claiming a bead names whose agent it is

**Claim with `bd update <id> --claim`. Never `--assignee Claude`.**

`--claim` takes the assignee from the actor `bd` already resolves — `$BEADS_ACTOR`, then
`git user.name`, then `$USER` — and sets `in_progress` in the same step. Each box sets
`BEADS_ACTOR` to `claude/<person>`: `claude/robin`, `claude/sophie`.

The assignee exists to say *whose* agent holds the bead, and this repository's beads live in
Munin's pool, which several people's agents write to. A literal `Claude` is the same string in
all of them, so `in_progress / Claude` reads identically whether the work is live or abandoned —
which is how PR #145 came to be opened for `Fhi.Metadata-l9l2n.33` three and a half hours after
that bead had merged (`Fhi.Metadata-e97p0`).

## Tests

- Test method names are English and follow `Method_WhenCondition_ThenOutcome`.
- Test helpers and fixtures are English too, same as production code.
- Comments explain *why a test exists* — what breaks if it is deleted — not what the code does.

**bUnit cannot see the browser's own state, and one line of the component depends on it.** A
browser flips a checkbox itself, before any handler runs, and a Blazor render that equals the render
before it writes nothing back to the DOM — so a press the component refuses leaves a visibly ticked
box over a filter that is off, or a column that is still drawn. The one line that unsticks it is
`builder.SetUpdatesAttributeName("checked")`, and deleting it from the column picker or the facet
panel left the whole suite green either way: bUnit renders a render tree, where the flip never
happened, so there is nothing to disagree with. `scripts/check-component-state.sh` is the one thing
here that drives a real browser and asserts state rather than boxes or the accessibility
tree; `scripts/state-assertions.mjs` says which presses it stages and, at length, which it does not
(`Fhi.Metadata-1s7z1`). A refusal test in `test/` is worth writing anyway — it pins the component's
own rule — but it is not evidence about the DOM, and it should not be written as though it were.

## Accessibility is a requirement, not a preference

This package ships into helsedata.no, a public-sector site. **WCAG 2.1 AA applies by law** —
forskrift om universell utforming av IKT, enforced by Tilsynet for universell utforming. A defect
we ship becomes theirs to answer for, on their domain, under their name.

That has two consequences worth stating, because neither is obvious from inside a component.

**The component cannot see the stylesheet it will be judged on.** We emit class names; the rules
live in `Fhi.Helsedata.Stiler`. Contrast, focus visibility and — the one that bites — whether an
element keeps its semantics are decided there. `display: flex` and `display: grid` strip table
semantics from a native `<table>` and from ARIA table roles alike. So markup can carry every
correct role, pass every check we can run here, and still be silent to a screen reader inside
helsedata.no. When a change depends on a rule, follow it into Stiler.

**A green check is not a claim of accessibility.** `scripts/check-accessibility.sh` runs axe over
the sample host and CI runs it per pull request, but automated checking finds on the order of a
third of WCAG issues. It is blind to the absence of structure in particular: a list built from
roleless divs breaks 1.3.1 and axe reports nothing, because nothing is malformed — there is merely
nothing there. That is not hypothetical. `VariableExplorer` scored 95 while being unnavigable by
column, and only a human looking found it (`Fhi.Metadata-3b1l4`).

What it scans is the sample host, and so the sample stylesheet — the copy
`scripts/assert-sample-css-in-step.sh` keeps in step — not the Stiler rules the component actually
ships into, which puts the paragraph above outside the gate entirely.

**And axe judges the accessibility tree, not the boxes.** On 2026-09-03 four layout defects shipped
to a branch having passed 1317 unit tests and all eight axe states: a tablist rendered under
helsedata's header, both tab panels drawn at once because their `div { display: block }` beats the
browser's `[hidden] { display: none }`, a nested view wearing the page shell class and laid out as
a page grid, and facets left on the wrong tab. axe was right to be green through every one — none
is a rule violation. They were found with `getBoundingClientRect`, by a human. `samples/HostileHost`
is that condition made reproducible: the real `Fhi.Helsedata.Stiler` package for its CSS, their
header positioned over the top of document flow, and `scripts/check-hostile-host.sh` measuring
geometry before it runs axe over the same page. `scripts/geometry-assertions.mjs` says which of its
assertions are general invariants and which are replays of those four, and why that distinction is
the difference between a suite and a changelog with an exit code. It found two further defects on
its first run (`Fhi.Metadata-l9l2n.41`, `Fhi.Metadata-l9l2n.42`), both in Stiler rather than here,
and both invisible to everything else we run.

**And `check-accessibility.sh` measures one width axe never looks at.** WCAG 1.4.10 Reflow is
stated at 320px, and nothing in this repository measured any page there: `scripts/geometry-scan.mjs`
drives six widths and the narrowest is 843. The script now ends by measuring ModernHost's `/kilder`
at 320 in two states — `kilder-list`, and `kilder-ticked`, which ticks a row so the selection ribbon
carries its longest label with the reset beside it. That is the rendered form of a gap unit tests
could only pin as text — the explore button's width floor overflowed the page by 87px before
`Fhi.Metadata-l9l2n.65`, and `KildeSelectionTest` can say the declaration is there and not that the
page fits.

It runs three of the ten assertions, by name through `GEOMETRY_ASSERTIONS`: `no horizontal
overflow`, `hidden means hidden`, and `text a reader is meant to see has a box to see it in`. The
other seven were **measured there and then excluded**, which is a different claim from "they are
written for HostileHost" and the only one the numbers support:

- `the tablist clears the header`, `exactly one tab panel has content` and `no page shell class
  inside a tab panel` are scoped to the two `explorer-*` states, so on either `kilder` state they
  print `n/a` and measure nothing.
- `nothing the reader can press is under the host header` reports `no .main-header on the page —
  the host chrome did not render`. ModernHost draws none; that finding is about the fixture.
- `the component stays inside the box the host gave it` fails at 320 on a real defect the fix for
  which is not in this repository: the column picker's open list is 304px wide against a 226px
  mount and hangs off the left edge of the viewport, `Fhi.Metadata-abmom`.
- the two `kilder` pins hold at 320, and are left out because what they exist to catch is a Stiler
  rule going missing, which the sample stylesheet can only stand in for. `check-hostile-host.sh`
  measures them against the real one at six widths.

Read the run for exactly what it is: three assertions, one page, two states, against the sample
stylesheet. The pinned-Stiler pages are still unmeasured at 320. `Fhi.Metadata-hfzsu` — 82px of
helsedata's own site chrome overflowing there on every page of theirs — is fixed on Stiler's main
and closed, so what adding 320 to `GEOMETRY_WIDTHS` now waits on is that fix being released and
`samples/HostileHost` moving off its `0.1.42` pin, which is `Fhi.Metadata-kpmt3`. Until then a gate
that included those pages would be red on every pull request for a defect this repository cannot
fix, and a gate nobody can get green is one somebody deletes.

**It scans states, not only pages.** A page in its default state is not the page a reader uses,
and for a while the default state was the whole of this check: the level lines shipped at 1.16:1
against WCAG 1.4.11's 3:1, invisible on a desktop, with this job green — because the lines only
existed once `Nivålinjer` had been pressed and axe never saw them (`Fhi.Metadata-wcbxi`).
`scripts/axe-states.mjs` now drives the sample into named states before axe looks: the filter tree
unfolded with the guide lines on, a variable row opened, and a kilde opened in the kildeutforsker.
The lines are on at first render since `Fhi.Metadata-dfygj`, which is why that state now only
unfolds and no longer presses the button — a press there would turn them off and hand this check
back the blind spot it was written to close.
The states it does **not** enter are listed above `TARGETS` in `check-accessibility.sh`, and that
list is the honest bound on a green run — extend the two together, never one alone.

**And it scans data, not an empty shell.** The sample host reads `scripts/axe-stub-api.mjs`, which
serves the contract-drift fixtures, because `runa.munin.skytest.fhi.no` is geo-filtered and a
GitHub runner sits outside it — the same finding that moved the contract-drift check off CI and
onto the devbox (#127). Until this was noticed the gate had spent its whole life scanning two
pages with nothing on them, and axe reports no violations in nothing: on CI, "no violations" meant
"no content". Every state now waits for a row before axe looks, the list pages included, so an
empty page fails as TOOLING rather than passing as clean.

So the gate catches regressions in the subset it can see. Read a green run as "no detected
regression", and never write it down as more than that. That sentence is the whole reason the
script and the CI job carry no argument of their own: this is where it is written down.

**What exists to help.** `AccessibleName` in the test project resolves what a screen reader would
announce a control as, and deliberately refuses to count `placeholder` or `title` — both satisfy a
naive "has a naming attribute" check and neither is a name. Use it for anything a reader operates.
`KildeSearch` is the worked example of a data table done right: a real `<table>`, scoring 100.

## Components are sealed

**Every component this package publishes is `sealed`, roots included.** A host can still mount
`VariableSearch` or `VariableListView` itself, and sealing changes nothing about that — it is
derivation the rule is about, not composition. `VariableExplorer` and
`KildeExplorer` — the two roots that were still unsealed — were open by silence rather
than by decision: nothing in either file said why, and neither had ever carried the keyword
(`Fhi.Metadata-l9l2n.43`).

The argument is asymmetry, not taste. Unsealing later is invisible to every consumer; sealing later
is a binary break for anyone who derived in the meantime. The package is `0.1.0-alpha` and helsedata
mounts by **type name** out of a CMS field rather than by inheritance, so the open door served no
consumer that exists — which makes now the only cheap moment, the same reasoning the Norwegian
rename used before the first publish.

The rule is here, once, rather than as a comment on every component. `SealedComponentsTest` is what
keeps it true: a component added unsealed is not a compile error and its audience is a host, after
publication. If a future extension route is genuinely wanted, unseal that one type and say in its
own remarks what it is for — an exception with a reason is fine, silence is what this replaced.

## A swallowed exception is written down before it is swallowed

**Every `catch (Exception)` in `src/` either logs the exception or lets it travel on.** Swallowing
is right here and stays: an unhandled exception inside a Blazor circuit tears down the whole CMS
page on helsedata's host, so the browsing surfaces catch everything and say one sentence in the
alert region. What was wrong is that the exception then went nowhere. Thirty sites threw one away,
two of them under a comment saying the detail belonged in the host's logs, while nothing in the
package had ever written to a log (`Fhi.Metadata-l9l2n.47`).

The shape, which is helsedata's own newer code and not an invention of ours:

```csharp
Log?.LogError(ex, "could not load kilde {KildeId}", id);
```

- **The exception is the first argument, never a template argument.** `LogError("… {ex}", ex)` is
  the same line minus the stack, and their older sites do it that way — do not copy those.
- **No component name in the template.** `Log` is `ILogger<KildeSearch>`, so the category already
  is the type; every sink renders it, and `"KildeSearch: could not load …"` writes it twice.
- **Named PascalCase placeholders, never interpolation.** No `LoggerMessage` source generator, no
  `EventId`, no `BeginScope`: their solution has none of the three and this is not the place to
  introduce one.
- **`Error` for a failure, `Warning` for an outcome that is expected and handled** — a 429, a 401,
  a vocabulary that only costs labels. The `MuninExplorerRateLimitedException` branch stays a
  branch of its own: telling throttling apart from failure from outside is what ruled rate limiting
  out of the incident above. A catch that folds the two for the sake of one shared sentence still
  splits the level, the way `FetchRowsAsync` does — the guard reads the clause's type and refuses
  anything but `Warning` on one of ours. `MuninExplorerUnauthorizedException` is the same rule and
  is easier to miss, because it reaches every one of the my/lists paths and no other: a host can
  declare `IsAuthenticated` true while its token provider sends nothing the API accepts, and
  reading that as `Error` fills the channel with an outcome the reader was already told about.
  Nine of those paths recorded it at `Error` while the save button beside them recorded it at
  `Warning`, which is how easily a rule stated once drifts (`Fhi.Metadata-l9l2n.47`).
- **A folded split is a test, because the guard cannot read one.** For a clause typed `Exception`
  the guard accepts any level, so inverting the `if` or deleting it leaves the suite green while a
  429 is reported as a fault. Each of the eight folded sites has a throttled test of its own in
  `ExceptionLoggingTest`; a new one owes the same.
- **Log where the failure is, not where the method is.** `KildeHierarchyView` cancels its own
  calls on every new `KildeId`, and a superseded one arrives as a `TaskCanceledException` that
  nothing failed: log inside the `IsCancellationRequested` guard rather than above it. A blanket
  `OperationCanceledException` filter would be wrong — `HttpClient`'s own timeout is that type too,
  and it is a fault.
- **Nothing a log must not carry.** No request URI, no query string, no response body, no bearer
  token, no text the reader typed — a kilde, variable or list id and a page number say which call
  it was without any of that.

`Log` is `ExplorerLog.For<T>(services)`, which is `GetService` and can answer null, because
`[Inject]` on a non-nullable `ILogger<T>` **throws at render** in a host that registered no logging
— turning a silent data error into a dead component, which is worse than the blindness this
replaced. `AddMuninExplorer` calls `AddLogging` so the ordinary host has one; the nullable
resolution is what covers the host that never called it. `services` is the `[Inject]
IServiceProvider` three components already carried before any of this — every container
self-registers it, so it is the one seam that costs a host nothing. Both halves are tested, and a
change that only proves the first passes CI and breaks a hostile host.

What comes back is **wrapped**, and that is the second half of the same argument. `Logger<T>.Log`
does not swallow a provider's failure — it rethrows it as an `AggregateException` — and every call
here is the first statement of a catch written so that nothing escapes and takes the circuit with
it. A host on a full disk would otherwise lose the page, and skip the sentence on screen too.
`ExplorerLog` is the one file in `src/` allowed to swallow: an exception thrown by logging has
nowhere left to be written down, and the guard names that file and asserts it is the only one.

It lives in `Logging/`, in `Fhi.Munin.Explorer.Logging`, and not at the root of `src/` and not
under `Blazor/`. Three layers read it — the components, `MuninExplorerClient` and
`VariableListState` — so putting it under any one of them would have a `Client/` file depending on
the `Blazor` namespace, which is the inversion the folder split exists to prevent; and a root-level
implementation type is an invitation to treat the root namespace as a place to put things. One
folder, one namespace, as everywhere else here.

The one call the logger is **passed** to rather than read off the component is `RaiseAsync`, which
is static and takes it as an argument at fourteen sites in three files. Pass `Log`, never `_log` —
the backing field is null until the property has resolved it once, and that once is the mount.
`SwallowedExceptionGuardTest` reads the call sites for exactly this, because the helper's own catch
satisfies every other check whatever its callers hand it.

`SwallowedExceptionGuardTest` is what keeps this true, since the next site added is not a compile
error and its cost only shows up on somebody else's server.

## Class names in markup

Two kinds of name reach the DOM, and the rule differs between them.

**Borrowed names are not ours to choose.** Where a part of the component is ordinary page
furniture — the search field, the buttons, the headings, the infobox, the choicepicker — it wears
`Fhi.Helsedata.Stiler`'s own name. Verify every one against Stiler's compiled `main.css` before
using it, rather than against a list or another component's markup. A name Stiler has never heard
of renders as a raw browser default inside an otherwise styled page, which is the failure the
whole approach exists to avoid — see the history behind `Fhi.Metadata-l9l2n.29`, and `headline-sm`,
which read as a borrowed name for months and was a typo for `headline-s`.

**The explorer's own vocabulary is ours, under the `munin-explorer` prefix.** Structure, results,
panel, drill-in, pager and kilde view all live there, and the package owns the whole prefix. It did not
always: the component used to write helsedata's own `variable-explorer*`, `variable-data-list*`,
`variable-dataitem*` and `variable-meta*` and inherit their rules for free from `variables.css` —
the stylesheet of the very page it replaces — which meant it only looked right inside helsedata's
estate. The rules ship in `Fhi.Helsedata.Stiler` under `components/munin-explorer/` — most of them
in 0.1.13 and later, the pager's and its skip link's in 0.1.14, since 0.1.13 shipped before those
names were renamed — so any host with Stiler can style the component. Do not move a name back into
the old prefix: Stiler still defines `.variable-explorer-header`, so `variable-*` is helsedata's
namespace and writing in it is either borrowing or colliding.

A name under our prefix is still inert until some stylesheet supplies a rule that declares
something for it — an empty block draws what no block draws — and the two
sample hosts carry no Stiler — they are that stylesheet here, and they are one file copied:
`samples/ModernHost/wwwroot/host.css` and `samples/LegacyHost/wwwroot/css/host.css` must stay
**byte-identical**, and between them must style every `munin-explorer*` name the package invents.
Both halves fail silently — a block landing in one copy only shows the component broken in that
sample alone, and a name with no rule anywhere shows it broken in both — so
`scripts/assert-sample-css-in-step.sh` checks each and runs in CI. Edit one copy, copy it over
the other, and run the script.

**That check asks whether a name declares SOMETHING, not whether it declares the right thing**, and
the gap between those two is wide enough to have held around forty divergences while it stayed
green. A rule carrying half of Stiler's declarations passes it; so does the right property carrying
the wrong value. `scripts/assert-sample-css-matches-stiler.sh` closes it by comparing
**declarations** — property and value — for every selector under the prefix, and for every
**borrowed** selector the sample writes a rule for, against the published
`Fhi.Helsedata.Stiler` `main.css` that `samples/HostileHost` pins. The published package rather
than Stiler's `main` on purpose: it is the Stiler a host actually restores, so a green run means
"the stand-in matches what helsedata will have" rather than "it matches unreleased work". It runs
in the `layout in helsedata's stylesheet` job, which is the one job holding the private-feed
credential, and skips itself where that secret is absent. **A green tick on that job is therefore
not proof the comparison ran** — a fork pull request gets no secret, the job skips, and the summary
counts a skip as fine. That is the bound `check-hostile-host.sh` beside it has always had, and this
guard inherits it rather than closing it; read the job, not the tick.

The borrowed half is asymmetric on purpose: a Stiler selector the sample does not write at all is
not reported, because the sample stands in for the parts of the design system the component touches
and no further — but where it *does* write the rule, it owes what Stiler declares and owes nothing
more. That last clause is the one that was missing until `Fhi.Metadata-l9l2n.105`. The sample had
invented `white-space: nowrap` on `.dropdown-choicepicker__item`, the comparison never looked at a
borrowed name, and a 55px overhang measured against the sample was filed as a P2 defect in the
component.

**"Where it does write the rule" means where Stiler spells the selector the same way**, which is
narrower than it sounds and the borrowed half's real bound. Rules are keyed on selector text, so
the sample's `.hd-button-square.button-square--primary` finds nothing under Stiler's
`.button-square--primary` and is compared in neither direction — and `missing-selector`, the alarm
that catches exactly that under the prefix, is deliberately off here. What replaces it is a count:
the run prints how many borrowed rules the sample writes matched no Stiler selector, and that
number is the size of the blind spot for that run. It is not zero. Read it.

The 214 divergences standing today are listed in `test/sample-css-known-divergences.txt`. **Nothing
writes that file.** A divergence not listed fails the build, and a listed line that no longer
diverges also fails it with an instruction to delete the line, so the count can only go down.
Adding a line is a hand edit that needs a reason; do not add one to get a branch green. What the
comparison does **not** see is source order and specificity — two rules can both exist, both
declare the property, and still draw differently (`Fhi.Metadata-cuo0e`) — and shorthands, which are
compared as written rather than expanded. `font-family`, the `font` shorthand and `src` are not
compared as values, because Stiler ships a typeface this repository cannot redistribute — but the
size and line-height a Stiler `font` carries are read back out of it on a borrowed selector, so a
sample longhand beside one is compared rather than dropped. That exception exists because dropping
them was the one place the comparison erred towards silence, and they are geometry.

A rule is not the same as a host being told. **Adding a `munin-explorer*` name means adding a row
to the README's inventory table**, between the `<!-- class-names:start -->` markers, with the kind
that says what an undefined one costs a host — `handle`, `meaning`, `id` or `prose`.
`scripts/assert-class-names-listed.sh` reconciles that table against `src/` in both directions and
runs in CI. It exists because the older check diffs the branch and so can only ever ask about names
that are new on it: three hand-written counts had gone stale and eight `munin-explorer-whole*`
names were in no markdown file at all, none of them visible through that window. Do not replace the
table with a count — a count is what drifted.

The changelog is the second place a host reads, and **what a host must style belongs in a
`Notes for hosts` fragment, not in the `Added` bullet that introduces the name**. One category per
file, per `changelog.d/README.md`: split it, they end up in different sections anyway.
`scripts/assert-fragment-names-noted-for-hosts.sh` fails when a fragment of any other category names
a `munin-explorer*` name that no `Notes for hosts` fragment — or released section — names. It has no
window either: Kelda's facet panel stated its own host requirement in an `Added` bullet in #72, and
the branch check landed in #96, so the requirement was never new on a branch that check could see.

The sample stylesheets, the README table and the changelog are all inside the window this
repository can check, and not one of them puts a rule where helsedata.no reads it. **A new or
renamed `munin-explorer*` name has no appearance there until a rule for it lands in
`Fhi.Helsedata.Stiler`** — a separate Azure DevOps repository no pipeline here can reach. So a
green Explorer pipeline is not evidence the element is styled; nothing that runs on this side has
ever looked at the stylesheet that decides it.

**The Stiler rule therefore gets its own bead, filed before the PR that introduces the name
merges** — labels `stiler`, `rcl` and `helsedata` — and not a clause inside the RCL bead's own
acceptance criteria. A clause is not work anyone can be handed: it is in nobody's `bd ready`, it
cannot be claimed, and the bead holding it reads as finished the moment its PR merges. #161
(`Fhi.Metadata-3osk6`) was careful about exactly this and it still was not enough — it used `Refs`
rather than `Closes` so the bead would stay open until the Stiler half landed, and the bead is open
to this day with `munin-explorer-kilde__delkilde-description` shipped unstyled and nobody staffed on
it until Robin asked. It was filed by hand afterwards as `Fhi.Metadata-8e2ev`. Everything this side
can check had passed: a rule in both sample stylesheets, a row in the README table, a host note in
`changelog.d`.

The mechanics of working in Stiler — which of the checkouts on the box is the right one, and why
`az repos` cannot be trusted with a description — are in `CLAUDE.md` under "Finishing".
