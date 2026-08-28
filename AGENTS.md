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

The budget is in [`CLAUDE.md`](CLAUDE.md) under "Comment budget". This is what it looks like in
practice, using two comments already in this repository rather than an invented example.

**A comment earns its length when nobody could reconstruct it from the code.** The `<remarks>` on
`IMuninExplorerTokenProvider` is twenty-odd lines and every one of them is load-bearing:
`IHttpClientFactory` builds and caches the handler pipeline in its own scope and reuses it across
callers, so an implementation cannot capture anything scoped; there is no `HttpContext` during
Blazor Server circuit activity, so `IHttpContextAccessor` finds nothing and fails *quietly*; and
the returned value is the raw token, not `Bearer `-prefixed, because the scheme is added when the
header is written. None of that is visible in `Task<string?> GetTokenAsync(...)`, the failures are
silent, and they land in a host repository we do not own. It is also on a `public` type in
`Contracts/`, where it reaches the consumer who needs it.

**A comment has outstayed its welcome when the document it duplicates already exists.** The
`<remarks>` on `VariableExplorer` runs 171 lines — every borrowed Stiler name, every
`munin-explorer*` name, the pager rename, the 0.1.13/0.1.14 Stiler split, the `skiplink-pagination`
postmortem — and closes by saying `README.md` has the full split. It does. So this is a second copy
of a document that is maintained elsewhere, kept in the one place a reader has no reason to check
and an editor has no reason to update, on a `public` type that ships all 171 lines into
IntelliSense for a component whose usage is one tag. What belongs there is a sentence and a link.

DO:

- Explain *why*, and prefer the reason the code cannot state: a race, an invariant that spans two
  files, a workaround for behaviour we do not control.
- Say what a caller must not do, and what happens if they do it anyway.
- Cite the bead — `// See Fhi.Metadata-ja2qu` — where the history is the point.
- Document the wire contract in `Contracts/`: spelling, nullability, units, error shapes.

DON'T:

- Restate the signature. A `<returns>` explaining that `AddedAt` is when it was added is noise
  wherever it appears, and noise in the nupkg when the type is public.
- Narrate an incident in a `.cs` file. It rots as soon as the code around it moves, and the bead
  it belongs in is already there.
- Copy `README.md` or `AGENTS.md` into a comment. Link them.
- Put internal-only prose on a `public` type. Whoever it was written for, a consumer reads it.

## Tests

- Test method names are English and follow `Method_WhenCondition_ThenOutcome`.
- Test helpers and fixtures are English too, same as production code.
- Comments explain *why a test exists* — what breaks if it is deleted — not what the code does.

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

A name under our prefix is still inert until some stylesheet supplies a rule for it, and the two
sample hosts carry no Stiler — they are that stylesheet here, and they are one file copied:
`samples/ModernHost/wwwroot/host.css` and `samples/LegacyHost/wwwroot/css/host.css` must stay
**byte-identical**, and between them must style every `munin-explorer*` name the package invents.
Both halves fail silently — a block landing in one copy only shows the component broken in that
sample alone, and a name with no rule anywhere shows it broken in both — so
`scripts/assert-sample-css-in-step.sh` checks each and runs in CI. Edit one copy, copy it over
the other, and run the script.
