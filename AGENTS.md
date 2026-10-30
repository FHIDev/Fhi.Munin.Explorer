# AGENTS.md: FHIDev/Fhi.Munin.Explorer

<!-- team-doc:begin sha256=8c5fcf69e3f9adfbf42c85fdb23416208bf54a0a5b6b3544fabfbc57c7b29481 source=FHIDev/Fhi.Munin.DevInfra/docs/team/working-together.md -->
## The team's rules, in short

Full text: FHIDev/Fhi.Munin.DevInfra `docs/team/working-together.md`. This block is a synced copy; edit the
canonical doc, not this block.

- **FHI's guidelines apply:** RL-KI-001 «Bruk av KI-agenter i programvareutvikling» and RL-KI-002 «Marketplace for
  KI-agenter og skills» (utvikling.sky.fhi.no/KI/). Each shared skill has a named owner (see the full doc).
- **What reaches the model providers** (Anthropic; GitHub Copilot for the PR diffs it reviews): code, bead and PR
  text (which carry team members' names) and team chat.
  Never paste personal data about citizens, patients or research participants, production data or secrets into
  what an agent reads or sends. Details and open questions in the full doc.
- **Beads are written only through Varde** (the `beads_*` tools), never with `bd`. When Varde cannot write, tell
  your coordinator; a single bd write then happens only on the owner's word.
- **Every bead has a type and a parent epic.** Order inside an epic is a `blocks` dependency; a real wait is a
  `venter:<what or whom>:<YYYY-MM-DD>` label. Notes are append-only.
- **Decisions go to your owner through your coordinator**, with a recommendation, the way your owner's lanes file
  says. A question left only in a bead note has not been asked.
- **Every PR body** has one `Pre-PR review:` line, `Testing:`, `Risk:` (matching the `risk:` label) and
  `How to revert:` lines, and `Closes-Bead:` or `Bead:`. Run the pre-PR review before opening it.
- **Open PRs without asking.** A PR merges when CI is green and its risk tier's review requirements are met
  (DevInfra `docs/review-risk-tiers.md`); who approves and who merges is each owner's own arrangement, in their
  lanes file. Never touch someone else's PR or branch.
- **Work hours** are each owner's, set on Varde. Outside them messages wait in the inbox; where Varde's relay runs,
  it wakes no session (only a stop) and wakes each one once when the hours open.
- **Never print the environment** or a secret, never roll, deploy or touch prod yourself, and do not work around a
  permission denial: report it.
<!-- team-doc:end -->

The Munin variable explorer, packaged as a Blazor Razor Class Library for helsedata.no (the NuGet package
`Fhi.Munin.Explorer`). This file is the one set of instructions for every agent in this repo, and it covers the
conventions a compiler cannot check; the architectural rules the components follow are in `README.md` under «Rules
the components follow». `CLAUDE.md` imports this file; Copilot's review reads `.github/copilot-instructions.md` and
`.github/instructions/code-review.instructions.md`.

## Before opening a PR

```bash
dotnet build && dotnet test
dotnet pack -c Release -o artifacts && ./scripts/assert-package-contents.sh artifacts
./scripts/assert-sample-css-in-step.sh          # only if you touched samples/ or a class name
./scripts/assert-sample-css-matches-stiler.sh   # same trigger; needs Stiler restored, see below
bash scripts/test-sample-css-declarations.sh    # only if you touched the comparison engine
./scripts/assert-portability-guard-armed.sh     # only if you touched Directory.Build.props
python3 scripts/comment-budget.py               # comment blocks this branch added, against origin/main
```

**Pre-PR review.** The `pre-pr-reviewer` agent on `git diff origin/main...HEAD`, which answers the warden checklist
(`python3 scripts/warden-checklist.py`; each rule ✓, ✗ with `file:line`, or n/a), and one line in the PR body in one of four forms: `Pre-PR review: ran, no findings`, `ran, N findings,
fixed`, `ran, N findings, M fixed, K left: <why>` or `skipped, <reason>` (only for a change with nothing to review;
`prove-it` section 8). The body also carries `Testing:`, `Risk: low|medium|high – <why>` (the tier the `risk:`
label gives) and `How to revert:`, each on its own line; the review-line check fails a PR where one is missing or
empty, or the tier differs from the label.

The Stiler comparison needs the package on disk, which means credentials for helsedata's Azure
Artifacts feed and `dotnet restore samples/HostileHost/HostileHost.csproj` first — see
`nuget.config`. Without them it exits 2 saying so rather than passing having read nothing; CI runs
it in the job that holds the feed secret. The engine's own test needs neither — it runs against
fixtures, and it exists because the engine's borrowed half is all deliberate asymmetries that look
like bugs and can be tidied away without a single other check going red.

- **A `src/` change needs a changelog fragment** in `changelog.d/`. CI fails without one.
  Fragments here are **English only** — deliberately unlike Munin's bilingual `.en.md`/`.nb.md`
  pair. See `changelog.d/README.md`.
- **Comments have a three-line ceiling** — the why, never the what; incident history goes in the
  bead; the package's public XML docs are the exception, because their reader has only the
  package. Full rule under «Comments» below. Prose that outgrows the ceiling belongs in
  the bead, the PR description, or this file — not in a file people reopen on every visit.
  The `comment budget` CI job enforces it on what a PR adds.
- `dotnet format` on a Windows checkout reports pre-existing whitespace noise from CRLF. Compare
  the count against untouched `main` before believing it is yours; `.gitattributes` normalises to
  LF, so CI sees clean files.

## Beads for this repository

Work is tracked in the **Munin** bead pool, not this repository's issues. Epics: `Fhi.Metadata-l9l2n` (v1
read-only), `Fhi.Metadata-dfy9u` (v2 login and variable lists), `Fhi.Metadata-2fomm` (Kelda).

- **Every bead for this repository gets the labels `helsedata` and `rcl`** (`beads_create` with
  `labels: ["helsedata", "rcl"]`, or `beads_label` later), and `repo:explorer` so Varde files it under Explorer;
  add `kelda` for kildeutforsker work. Among hundreds of Munin beads the label is the only thing that finds this
  repository's work: eleven unlabelled beads were invisible to that filter by 19 August 2026.
- Close a bead with a **`Closes-Bead: Fhi.Metadata-abc12`** line in the PR body; the bead-closer reads merged PRs
  here. `Bead: <id>` when the PR only partly delivers it. `Closes FHIDev/Munin#N` closes no bead.
- Varde's reads come from a snapshot up to five minutes old: the claim (`beads_claim`) or the write itself is the
  check, not an earlier read.
- Work in a git worktree; work is not done until it is pushed.

## Gates by kind of change

- **If the change touches markup, run `./scripts/check-accessibility.sh`.** WCAG 2.1 AA applies
  to this package by law, and green means no detected regression rather than accessible — what the
  gate cannot see is under "Accessibility is a requirement, not a preference". CI runs
  the same script, so a red check there is never a surprise. It takes
  `PLAYWRIGHT_BROWSER_CHANNEL=msedge` on the same terms as the layout gate below, for the same
  Node 26 reason — a different engine build from CI's, so the run says which it used. On a Linux
  box with no root and no browser libraries, the Forge's, every browser gate unpacks them into a
  user prefix itself; see `docs/running-locally.md`.
- **If the change touches layout, run `./scripts/check-hostile-host.sh` as well.** It renders the
  component in helsedata's real stylesheet under their top-anchored header and measures boxes, which
  is the only thing here that would have caught the four defects of 2026-09-03 — axe was green
  through every one. It does **not** need Azure Artifacts credentials any more:
  `STILER_FROM_SOURCE=1 ./scripts/check-hostile-host.sh` builds Stiler from a checkout beside
  this repository instead of restoring the pinned package. Add
  `PLAYWRIGHT_BROWSER_CHANNEL=msedge` if `playwright install chromium` will not complete — on
  Node 26 it cannot. Both flags change what is being measured and CI sets neither; the run prints
  which it used. See `docs/running-locally.md`.
- **If the change touches a control the reader presses, run `./scripts/check-component-state.sh`.**
  It drives a real browser and asks whether the DOM and the component still agree after a press the
  component refused — the one thing no test in `test/` can see, because the browser flips a checkbox
  itself before any handler runs and bUnit renders a render tree where that never happened.
  Deleting `SetUpdatesAttributeName("checked")` from the column picker or the facet panel leaves the
  suite green and fails this, each removal failing only its own assertion. No feed credentials, and
  `PLAYWRIGHT_BROWSER_CHANNEL=msedge` on the terms above.
- **A new or renamed `munin-explorer*` name needs a rule in `Fhi.Helsedata.Stiler`, filed as its
  own bead before this PR merges** — `beads_create` with the labels `stiler`, `rcl` and `helsedata` —
  not as a clause in the RCL bead's criteria, which is in nobody's `beads_ready` and cannot be
  claimed by anyone. Green here is still not evidence the element is styled on helsedata.no —
  nothing in this repository reads Stiler — but you can go and look, and
  `docs/running-locally.md` says how without needing feed credentials. Do that before asserting
  what Stiler does or does not have. Reasoning under «Class names in markup» below.
- **Working in Stiler is Azure DevOps, not GitHub.** Clone it from
  `https://dev.azure.com/fhi/Fhi.Helsedata/_git/Fhi.Helsedata.Stiler` wherever you keep checkouts;
  it is not a submodule of this one and this file deliberately does not name a path, because the
  last one it named had rotted. It needs a bearer header rather than a credential helper:
  `git -c http.extraHeader="Authorization: Bearer $(az account get-access-token --resource
  499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv)" clone …`, and fetches need it
  too. Rules go under `Static/scss/components/munin-explorer/`, one file per area (`_trail.scss`,
  `_filters.scss`, `_results.scss`, and so on). No `gh`, no Copilot review, and no
  `Closes-Bead` either: nothing reads Azure DevOps, so close a Stiler bead by hand once its PR
  merges. And `az repos pr create --description` truncates at the first newline and turns æøå
  into question marks: create the PR, then PATCH title and description over the REST API with
  explicit UTF-8 bytes, and read it back.

## The rules that bite

Five things here fail *silently* when broken — no build error, no failing test, just wrong
behaviour found later by someone else.

Identifiers are English, and borrowed class names belong to `Fhi.Helsedata.Stiler` while the `munin-explorer`
prefix is ours: both have full sections below. Two things about the class names that the section takes for granted:

- **Know what the guards here can see.** Both guards (`HostClassNames` in the tests and
  `scripts/assert-sample-css-in-step.sh`) ask whether a name has a rule that declares something, in
  `test/host-class-names.txt` (a capture of the live helsedata page, its page-specific stylesheets included) or in
  the sample stylesheet. Neither reads Stiler at all, and neither can say which declarations a rule has to carry.
  `skiplink-pagination` was in both sources the whole time it was broken, because helsedata styles it and so do the
  samples, while a Stiler-only host got nothing: a rule somewhere is not a rule where it is needed, and that host is the one the prefix
  exists for.
- **Read the selector, not just the name.** The first Stiler rule for that link, on the unpublished
  `feature/munin-explorer-scss` branch, was scoped `.munin-explorer-header .skiplink-pagination` and could never have
  matched, since that header opens and closes entirely inside `ColumnPicker()`.

The other three:

3. **The package ships no CSS.** No `.razor.css`, and nothing in `wwwroot` but the one JavaScript
   module `explorer-interop.js` (Fhi.Metadata-35w0p.14) and `version.json`, which the build writes
   from the package version and git ignores (Fhi.Metadata-l9l2n.123). Sample hosts carry their own styling
   because they have no Stiler; the package must not.
   `scripts/assert-package-contents.sh` enforces it and runs in CI on every PR — by naming every
   packed entry, so a second module fails there exactly as a stylesheet does. Adding a file to
   `wwwroot` and adding its entry to that script are one commit: either alone is red.

4. **The two sample stylesheets are one file, copied — and they style every name we invent.**
   `samples/ModernHost/wwwroot/host.css` and `samples/LegacyHost/wwwroot/css/host.css` must be
   byte-identical, and between them must have a rule for every `munin-explorer*` class the
   package invents. The samples carry no Stiler, so they are the only stylesheet those names have
   here. Edit one, copy it over the other. Both halves have already failed quietly: the kodeverk
   block reached LegacyHost only, and the kilde view shipped with rules for none of its nine names
   in either copy. `scripts/assert-sample-css-in-step.sh` enforces both and runs in CI on every
   PR. Two families are exempt, each by exact match in `HostClassNames.OrphansIn`: the
   `munin-explorer-kilder-scroll--cols-N` count modifier and the eighteen
   `munin-explorer-kilder-header__<key>` handles, which only Stiler's sticky-head thresholds select on.

5. **A rule that declares *something* is not a rule that declares the right thing.** The check
   above stayed green while around forty divergences from `Fhi.Helsedata.Stiler` stood in the
   samples. `scripts/assert-sample-css-matches-stiler.sh` compares declarations — property and
   value — against the published package `samples/HostileHost` pins, for the `munin-explorer`
   names and for the borrowed ones the sample writes a rule for, and the ones standing today
   are listed in `test/sample-css-known-divergences.txt`. **Never add a line to that file to get a
   branch green**: a new divergence and a stale line both fail the build, so the count can only go
   down. Adding one is a hand edit that needs a reason in the PR.

---

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

**The datatype vocabulary belongs to the API, and this package carries no table of its own.**
The codes are editable master data on Munin's side, so a table of names written here freezes a
snapshot in one language and drifts the moment someone edits a definition, in a package other
people ship and cannot patch. Since `Fhi.Metadata-6qy6l` the vocabulary carries a reader's word
beside the specification's term — `displayLabel`/`displayLabelEn`, "Tekst" beside "String" — and
the explorer endpoints name a datatype with it. The shipped `Texts.DataTypeNames` table, which put
"Streng" beside the API's "Tekst", was removed under `Fhi.Metadata-0mohg`.

Each surface takes the name from the source that follows the **reader's** language:

- **The datatype facet** renders `datatyper[].displayName`. `GetFiltersAsync` sends the language.
- **The result rows** in `VariableSearch` and `VariableListView` render the facet's name for the
  row's code. Not the row's own `dataTypeDisplayName`: the search and list calls send no
  `Accept-Language`, so that name is in the API's default language whoever is reading.
- **The detail block** in `VariableView`, and `VariableSearch`'s expanded row panel, render the
  DataType property's options through `CatalogueProperties.Option`, which prefers `displayLabel`
  over `label` with the same fallback as Munin's `PropertyOptionLabels`. The options carry both
  languages, so the language the detail was fetched in does not matter.

**The fallback is the canonical code, on every surface** — a facet with a blank name, a row whose
facet has not landed or never will, a detail whose vocabulary does not list the code. The same
fallback everywhere is what keeps one datatype from reading two ways on one screen. Never fall back
to the raw stored value.

`Texts.DataTypeAliases` and `Texts.CanonicalDataTypeCode` stay, for matching rather than display.
Values stored before the codes are words — `"String"`, `"tekst"`, `"Integer"` — and a stored value
has to become its code before any lookup keyed by the code. Munin normalises on read, so the
explorer endpoints send codes; a shared list's snapshot keeps whatever spelling was stored when it
was taken, and so does a host that hands `VariableView` a detail of its own.

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

The components a host mounts on their own — `KildeView`, `VariableView`, `DatasamlingView` — and the
kildeutforsker's `KildeSearch` hold no filters payload, so each fetches the filters itself: once per
mounted component and language, through `KildeTypeVocabulary`, and names the kildetype with
`Texts.KildeTypeNameFromApi` from what came back. That is the decision of `Fhi.Metadata-vcxoc`,
chosen over a parameter every host would have to fill. Mounted inside `VariableSearch`,
`VariableListView` or `KildeSearch`, a view takes the explorer's vocabulary as a private cascading
parameter instead — seeded from the facets the explorer already holds where it has them — so opening
a row asks nothing more and the view says the word its explorer says. The three views resolve the
client with `IServiceProvider.GetService` rather than a required `[Inject]`, because `VariableView`
and `DatasamlingView` needed none before; with no client, a failed call, no `kildeTyper` or a blank
or echoed `displayName`, every surface falls back to the shipped table through
`Texts.KildeTypeLabel`. A failed call is not retried until the language changes or the component is
mounted again: the fallback is the shipped word, not an error.

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

**CI enforces the ceiling on what a pull request adds.** The `comment budget` job runs
`scripts/comment-budget.py`, ported from Munin's: a block gaining more than three lines of comment
text fails in `src/` and warns in `test/`, and editing an existing long block warns. Tag-only lines
(`<summary>`, `</remarks>`, `<inheritdoc/>`) do not count, nor does a one-line per-member tag:
`<param>`, `<typeparam>`, `<returns>`, `<exception>`, or JSDoc's `@param`, `@returns`, `@throws`. A
`///` block on a type or member a host can see — public, in public types all the way out, in
`src/Fhi.Munin.Explorer` — is exempt, and the script decides that from modifiers and indentation
rather than from the compiler, so `scripts/test_comment_budget.py` pins the shapes it recognises.
Pre-existing blocks are trimmed opportunistically, never by the job. An earned exception goes in
`.github/comment-budget-allowlist.txt` with its reason, for a human to approve.

**No `<summary>` / `<param>` / `<returns>` that restates the name.** Document constraints only —
units, null semantics, what a host must do, what a null means.

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
we ship becomes theirs to answer for, on their domain, under their name. The axe gate holds the
explorer to **WCAG 2.2 AA**, the level helsedata's accessibility statement is written against
(`Fhi.Metadata-mhqx0`), at 1280, 375 and 320px; a finding a bead owns is listed with that bead in
`test/axe-known-violations.txt`, and a line that stops matching fails the run.

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

**Nor does either of them press Tab.** A hidden panel still carrying `tabindex="0"` is a 0px stop
under that same `div { display: block }`, and neither a box nor the accessibility tree says so,
since `hidden` keeps it out of both. `scripts/tab-stop-scan.mjs` walks the Tab order with real key
presses in `check-hostile-host.sh` and fails on a stop with no box or inside a `[hidden]` subtree
nothing un-hides on purpose; it plants one such stop and reports the walk unmeasured, exit 3, if it
never lands there (`Fhi.Metadata-w8sms`).

**And `check-accessibility.sh` measures reflow at the one width WCAG names for it.** WCAG 1.4.10 Reflow is
stated at 320px, and nothing in this repository measured any page there: `scripts/geometry-scan.mjs`
drives six widths and the narrowest is 843. The script now ends by measuring ModernHost's `/kilder`
(in CI, in the first of the accessibility job's four shards only: `SCAN_SHARD`, `scripts/scan-shard.mjs`)
at 320 in three states — `kilder-list`; `kilder-ticked`, which ticks a row so the selection ribbon
carries its longest label with the reset beside it; and `kilder-every-column`, the widest table the
picker can draw, which has to scroll inside its box rather than widen the page. That is the rendered form of a gap unit tests
could only pin as text — the explore button's width floor overflowed the page by 87px before
`Fhi.Metadata-l9l2n.65`, and `KildeSelectionTest` can say the declaration is there and not that the
page fits.

The variable explorer's `/` is measured beside them twice. `variables-list` is the page at rest,
where below 1024px the filter panel is folded behind Vis filtre and draws nothing.
`filters-level-lines` presses Vis filtre and Utvid alle, and it is the one that measures the panel:
its toolbar, which grows a control at a time and whose fourth took it to 291px of content in a 226px
mount (`Fhi.Metadata-kd9ts`), and its facet values, where a variabelgruppe name with no break in it
only stays inside 320px because the label rule carries `overflow-wrap: anywhere`
(`Fhi.Metadata-7484a`). `check-hostile-host.sh` measures the unfolded panel at 320 as well, and does
not run in CI.

It runs three of the sixteen assertions, by name through `GEOMETRY_ASSERTIONS`: `no horizontal
overflow`, `hidden means hidden`, and `text a reader is meant to see has a box to see it in`. Four
of the thirteen left out were **measured there and then excluded**, which is a different claim from
"they are written for HostileHost" and the only one the numbers support; four are scoped to states
this run does not drive (the three `explorer-*` ones below and `Runa's row disclosure is big enough
to hit`), three have nothing to measure on these pages at all, and the two newest have never run here:

- `a table cell holding visible text is at least 4em wide` and `the saved list shows every column at
  767px and below` came with Stiler's 320px cards (`Fhi.Metadata-n8ygv`, `Fhi.Metadata-z4r1d`). The pin is
  scoped to `explorer-list-tab`, which this run does not drive; the invariant is measured against the
  real stylesheet by `check-hostile-host.sh`.
- `the tablist clears the header`, `exactly one tab panel has content` and `no page shell class
  inside a tab panel` are scoped to the two `explorer-*` states, which are the composed
  `VariableExplorer` on `/utforsker` and neither of the pages measured here, so they print `n/a`
  and measure nothing.
- `nothing the reader can press is under the host header` reports `no .main-header on the page —
  the host chrome did not render`. ModernHost draws none; that finding is about the fixture.
- `the component stays inside the box the host gave it` fails at 320 on the column picker: here its
  open list is 304px wide against a 226px mount and hangs 55px off the left edge of the viewport.
  Under real Stiler that is 2px, which `Fhi.Metadata-abmom` closed as accepted; the sample's
  excess is `Fhi.Metadata-w6lvr`.
- the two `kilder` pins hold at 320, and are left out because what they exist to catch is a Stiler
  rule going missing, which the sample stylesheet can only stand in for. `check-hostile-host.sh`
  measures them against the real one at six widths.
- `the detail page's main column is the wider part of its body` and `the detail page's two columns
  share a row` measure a `.munin-explorer-page__body`, the detail views' chassis, and `the detail
  page's fact list is one column with a reading measure` its `.munin-explorer-page__fields`;
  none of the five states driven here opens one. That is a statement about the states and not about the pages: both
  draw one in a detail state — `/` in `variable-whole`, `/kilder` in `kilde-drilldown`, which axe
  scans above — so adding either state to this run would measure them. They run and find nothing
  rather than failing, so they were never measured here in the sense the four above were.

Read the run for exactly what it is: three assertions, two pages, five states, against the sample
stylesheet. The pinned-Stiler pages are measured at 320 by `check-hostile-host.sh`, in a step of
its own after the six widths. It runs every assertion in every state, except that a state failing
today leaves out only its failing assertions, through `GEOMETRY_EXCEPT`, with the reason beside
it. One exception is left, and it is kept on purpose: under real Stiler the closed column picker
hangs 2px off the left edge on the kilder states, and `Fhi.Metadata-abmom`'s close reason records
why that stays.

**It scans states, not only pages.** A page in its default state is not the page a reader uses,
and for a while the default state was the whole of this check: the level lines shipped at 1.16:1
against WCAG 1.4.11's 3:1, invisible on a desktop, with this job green — because the lines only
existed once `Nivålinjer` had been pressed and axe never saw them (`Fhi.Metadata-wcbxi`).
`scripts/axe-states.mjs` now drives the sample into named states before axe looks: the filter tree
unfolded with the guide lines on, that same tree with its node icons turned off, a variable row
opened, a kilde ticked so the chip row and the hierarchy trail are drawn, and a kilde opened in the
kildeutforsker.
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
  `OperationCanceledException` filter would be wrong — the client's own timeout is that type too,
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

Three compatibility root markers need no rule: `munin-explorer-kilde`,
`munin-explorer-datasamling` and `munin-explorer-whole`. Stiler 0.1.100 removed their obsolete
heading resets; their elements also wear the shared `munin-explorer-page` chassis, which still
requires styling. Both class-name guards exempt exactly these markers, not their children or
other names under the prefix (`Fhi.Metadata-35w0p.39`).

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

The divergences standing today are listed in `test/sample-css-known-divergences.txt`. **Nothing
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

**So check Stiler itself, and nothing else.** The authoritative answer to "does Stiler carry a rule
for this name" is the `Fhi.Helsedata.Stiler` repository — `Static/scss/` for the source and
`wwwroot/css/main.css` for what the package actually ships — or the published package on
`Fhi.Helsedata.no`. **Never a deployed site.** `helsedata.no` does not run Stiler at all; the test
host does, and even there a grep answers "which version is deployed", not "what does Stiler
contain". A bundle fetched from a running site is evidence about someone's deployment, and reading
it as evidence about the package has already produced one confidently-wrong bead
(`Fhi.Metadata-u0cxn`, closed moot) and corrections to three others that had to be retracted.

**The Stiler rule therefore gets its own bead, filed before the PR that introduces the name
merges** — labels `stiler`, `rcl` and `helsedata` — and not a clause inside the RCL bead's own
acceptance criteria. A clause is not work anyone can be handed: it is in nobody's `beads_ready`, it
cannot be claimed, and the bead holding it reads as finished the moment its PR merges. #161
(`Fhi.Metadata-3osk6`) was careful about exactly this and it still was not enough — it used `Refs`
rather than `Closes` so the bead would stay open until the Stiler half landed, and the bead is open
to this day with `munin-explorer-kilde__delkilde-description` shipped unstyled and nobody staffed on
it until Robin asked. It was filed by hand afterwards as `Fhi.Metadata-8e2ev`. Everything this side
can check had passed: a rule in both sample stylesheets, a row in the README table, a host note in
`changelog.d`.

The mechanics of working in Stiler — which of the checkouts on the box is the right one, and why
`az repos` cannot be trusted with a description — are under «Gates by kind of change» above.

## Publishing

Tag-triggered: `git tag v0.2.0 && git push origin v0.2.0`. The one package, `Fhi.Munin.Explorer`,
goes to `Fhi.Helsedata.no`, the Azure Artifacts feed helsedata restores from — **not** nuget.org,
which this package has never been published to. Requires the `ADO_PACKAGING_TOKEN` secret. The
workflow refuses a tag whose commit is not on `main`, a malformed version, a packed version that
disagrees with the tag, and a version already on the feed: deleting one there does not take it
back from anyone who restored it. See "Releasing" in `README.md` for the rest.

**The tag also assembles the changelog, before it packs** — the version's `changelog.d/` fragments
become one `CHANGELOG.md` section and that section is the package's `PackageReleaseNotes`. Never
run `assemble-changelog.ps1` by hand to release: it was a documented manual step for three weeks
and eight versions shipped without it (`Fhi.Metadata-l9l2n.44`). `-DryRun`, and
`scripts/release-changelog.sh --dry-run`, are the rehearsals. The commit reaches `main` as a pull
request, since `MainRules` has no bypass actors. The workflow opens it as the fhi-munin GitHub App
(FHIDev withholds pull-request permission from `GITHUB_TOKEN`), and prints the `gh pr create`
command instead only if that fails. Merging it is the one step left to a person, and the next
release refuses to run until the section is on `main`.

## Host constraints worth remembering

The component must render inside helsedata's **legacy** Blazor Server (`AddServerSideBlazor` +
`MapBlazorHub`, mounted with the `<component>` tag helper) as well as a modern Blazor Web App.
So: no `@page`, no `@rendermode`, no `HeadOutlet`, nothing host-specific. `BannedSymbols.txt`
turns the last one into a build error — and `scripts/assert-portability-guard-armed.sh` is what
keeps that true, because it was quietly not for a while: the ItemGroup wiring the analyzer up is
conditioned on the RCL's project name, and the name it held was one project rename out of date.

There is **no `HttpContext` during circuit activity**. Anything reaching for
`IHttpContextAccessor` finds nothing and fails quietly rather than loudly — see
`samples/LegacyHost/Authentication/` for the pattern that works, and why a token provider must
be singleton-safe and resolve the user per call.

## Review risk tier

Every pull request gets `risk:low`, `risk:medium` or `risk:high` from its paths and `.github/risk-tiers.txt`
(`risk-label.yml`). What each tier needs: FHIDev/Fhi.Munin.DevInfra `docs/review-risk-tiers.md`. A path the rules
get wrong: change the rule and add the path to `.github/risk-tiers.examples`.

<!-- team-doc:bd-fallback:begin -->
## Only where Varde's bead tools are missing

Everywhere the `beads_*` tools exist, use them and never `bd`. This section is only for a session without them: the
Forge, until it writes through Varde (DevInfra-gbx.8 step 4). There, `.beads/redirect` points `bd` at Munin's pool:

```bash
bd ready
bd show <id>
bd update <id> --claim                     # never --assignee Claude: that string is the same on every box
bd update <id> --append-notes "..."        # never --notes: it replaces every earlier note
bd create --title="..." --description="..." --label=helsedata --label=rcl
bd worktree create .worktrees/<name> --branch feature/<name>   # writes the .beads/redirect a checkout needs
bd preflight --check                       # before opening the PR
```

Never run `bd label list-all`: it once blocked the shared Dolt cluster for thirteen minutes. `bd list --json` carries
every bead's labels.
<!-- team-doc:bd-fallback:end -->
