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
not this repository. `VariableExplorer.SearchChanged` spending a `<remarks>` on how an
`EventCallback` serialises to an empty delegate across a static-SSR boundary is length earned: a
consumer cannot reconstruct it from the signature and cannot open the file to find out. Being long
is not what the exception licenses; having a reader who has only the package is. Internal types
have no such reader and stay under the ceiling.

**No `<summary>` / `<param>` / `<returns>` that restates the name.** Document constraints only —
units, null semantics, what a host must do, what a null means.

## Tests

- Test method names are English and follow `Method_WhenCondition_ThenOutcome`.
- Test helpers and fixtures are English too, same as production code.
- Comments explain *why a test exists* — what breaks if it is deleted — not what the code does.

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

**It scans states, not only pages.** A page in its default state is not the page a reader uses,
and for a while the default state was the whole of this check: the level lines shipped at 1.16:1
against WCAG 1.4.11's 3:1, invisible on a desktop, with this job green — because the lines only
exist once `Nivålinjer` has been pressed and axe never saw them (`Fhi.Metadata-wcbxi`).
`scripts/axe-states.mjs` now drives the sample into named states before axe looks: the filter tree
unfolded with the guide lines on, a variable row opened, and a kilde opened in the kildeutforsker.
The states it does **not** enter are listed above `TARGETS` in `check-accessibility.sh`, and that
list is the honest bound on a green run — extend the two together, never one alone.

So the gate catches regressions in the subset it can see. Read a green run as "no detected
regression", and never write it down as more than that. That sentence is the whole reason the
script and the CI job carry no argument of their own: this is where it is written down.

**What exists to help.** `AccessibleName` in the test project resolves what a screen reader would
announce a control as, and deliberately refuses to count `placeholder` or `title` — both satisfy a
naive "has a naming attribute" check and neither is a name. Use it for anything a reader operates.
`KildeExplorer` is the worked example of a data table done right: a real `<table>`, scoring 100.

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

A rule is not the same as a host being told. **Adding a `munin-explorer*` name means adding a row
to the README's inventory table**, between the `<!-- class-names:start -->` markers, with the kind
that says what an undefined one costs a host — `handle`, `meaning`, `id` or `prose`.
`scripts/assert-class-names-listed.sh` reconciles that table against `src/` in both directions and
runs in CI. It exists because the older check diffs the branch and so can only ever ask about names
that are new on it: three hand-written counts had gone stale and eight `munin-explorer-whole*`
names were in no markdown file at all, none of them visible through that window. Do not replace the
table with a count — a count is what drifted.
