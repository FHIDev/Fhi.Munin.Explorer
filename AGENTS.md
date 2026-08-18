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

This is a change of direction. Early work here used Norwegian identifiers, following Munin's
own client code, and a good deal of the package still does — `Variabelutforsker`,
`SokVariablerAsync`, `HentTokenAsync`, `Sorteringsfelt`. Those are being renamed under
`Fhi.Metadata-osxfx`, which has to land **before the first nuget.org publish** — several of
them are public API, so renaming is free now and a breaking change for helsedata afterwards.
Until it lands you will see both, and **new code should not add to the Norwegian side**.

The reason is narrow and specific to this package rather than a general preference. This is a
library published to nuget.org and consumed by teams outside FHI's Norwegian-speaking core —
its public surface is the API other people program against, and `SokVariablerAsync(sok:)` is
not a signature a consumer can guess at. Munin's own code has no such audience, which is why
the convention differs there and why copying it across was a reasonable mistake to make.

## Norwegian stays where it is read, not called

Norwegian is correct, and required, for:

- **User-facing strings** — labels, status messages, error text. The component is bilingual
  (`nb`/`en`); Norwegian copy belongs in the text records, not spread through markup.
- **Changelog fragments** — `changelog.d/*.nb.md` alongside the English ones.
- **Domain terms with no honest translation** — `kilde`, `datasamling`, `delkilde`,
  `variabelgruppe`, `kildetype`. These are names of things in the Norwegian health-metadata
  catalogue, not English concepts wearing a Norwegian coat. Keep them as they are, in English
  identifiers: `KildeId`, `DatasamlingCount`, `SearchByVariabelgruppeAsync`. A "translation"
  like `SourceCollection` invents a term nobody uses and breaks the link to the API's own
  field names.

## Tests

- Test method names are English and follow `Method_WhenCondition_ThenOutcome`.
- Test helpers and fixtures are English too, same as production code.
- Comments explain *why a test exists* — what breaks if it is deleted — not what the code does.

## Class names in markup

Class names emitted by the components are **not** ours to choose: they are
`Fhi.Helsedata.Stiler`'s. Verify every one against Stiler's compiled `main.css` before using
it, rather than against a list or another component's markup. A name Stiler has never heard of
renders as a raw browser default inside an otherwise styled page, which is the failure the
whole approach exists to avoid — see the history behind `Fhi.Metadata-l9l2n.29`.
