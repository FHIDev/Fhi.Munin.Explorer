---
name: fhi-style
description: >
  The conventions in Fhi.Munin.Explorer that no compiler checks and that fail silently when
  broken — English identifiers with a short list of Norwegian domain terms, the three-line
  comment ceiling, borrowed versus munin-explorer class names, no CSS and nothing host-specific
  in the package, and accessible markup. Use when writing or changing any C#, Razor, or
  sample-host file in this repository.
allowed-tools: "Read, Grep, Glob"
version: "1.0.0"
license: "MIT"
---

# Explorer style — the rules a build cannot catch

`AGENTS.md` is canonical for every rule below and carries the reasoning. This file is the
short form you consult while writing, plus the shapes each rule fails in. When the two ever
disagree, `AGENTS.md` wins and this file is the bug.

Nothing here is a preference. Each rule is here because it has already been broken and the break
was invisible: no build error, no failing test, just something wrong that somebody else found
later.

## Identifiers are English

Types, members, parameters, locals, test helpers, file names.

```csharp
// yes
public Task<Page<VariableSummary>> SearchVariablesAsync(string? search, ...)

// no
public Task<Side<VariabelSammendrag>> SokVariablerAsync(string? sok, ...)
```

This is narrow to this package rather than a general FHI preference — Munin's own code is
Norwegian and copying that here was a reasonable mistake to make. The difference is the audience:
this is a library on `Fhi.Helsedata.no` whose public surface is the API other teams program
against, and `SokVariablerAsync(sok:)` is not a signature a consumer can guess at. The Norwegian
names were all renamed under `Fhi.Metadata-osxfx` before the first publish, so **there is no
Norwegian half left to add to**.

### The exception is a closed list of domain terms

`kilde`, `datasamling`, `delkilde`, `variabelgruppe`, `kildetype`, `kodeverk` and its kinds. They
stay Norwegian inside otherwise-English identifiers — `KildeId`, `DatasamlingCount`,
`GetKildeHierarchyAsync` — and keep their Norwegian plurals, because that is what the API calls
the collections: `Kilder`, `Delkilder`, `Datasamlinger`, `Variabelgrupper`, `KildeTyper`.

The list is short on purpose. Anything that looks Norwegian-only at a glance and has an honest
English equivalent uses it: `dataansvarlig` → `DataController`, `databehandler` → `DataProcessor`,
`lovverk` → `LegalBasis`, `kortNavn` → `ShortName`. Do not extend the list without reading the
section in `AGENTS.md` first — a "translation" like `SourceCollection` invents a term nobody uses
and breaks the link to the API's own field names.

### Renaming a DTO property is not a contract change

Every one carries an explicit `[JsonPropertyName]`, so the wire name is free to keep Munin's
spelling while the C# name reads English:

```csharp
[JsonPropertyName("sistOppdatert")] public DateTimeOffset LastUpdated { get; init; }
```

Norwegian is also correct — and required — in user-facing strings. Those live in the `Texts`
record with both languages present; see "Text" below.

## Comments

**`AGENTS.md` under "Comments" is the rule. Read it there.** It is one screen long and it is
where the ceiling, the bead-instead-of-narrative rule and the public-XML-docs exception are
written down. Restating them here would produce two ceilings that drift apart.

What this file adds is the shape of the miss:

```csharp
// no — the incident, not the invariant
// This retries once. We found this on 2026-08-14 when the ModernHost sample started
// returning 429s during the demo; Robin was on the call and we could see in the pod logs
// that the limiter was counting per-address, which meant every reader behind the office
// NAT shared one bucket. The API team confirmed the window is 60s. We tried backing off
// 5s first but that was still inside the window, so it is 60s now. If the limiter ever
// moves to per-user this can go.
```

```csharp
// yes — the invariant, with the archive one identifier away
// One retry after the full window: the limiter counts per address, so a shared NAT
// exhausts the bucket for everyone behind it and a shorter back-off lands inside the
// same window. (Fhi.Metadata-l9l2n.30)
```

The second is not shorter because brevity is a virtue. It is shorter because everything cut was
recoverable from the bead, and the reader of the line below needs none of it to read the line.

The public API surface of `src/Fhi.Munin.Explorer` is the standing exception, because
`GenerateDocumentationFile` is on and the XML ships inside the package: its reader has the
package and its IntelliSense and cannot open this file. Internal types have no such reader.

## Class names in markup

The single most expensive rule here, and the one already broken twice.

- **Borrowed names belong to `Fhi.Helsedata.Stiler`.** Ordinary page furniture — search field,
  buttons, headings, infobox, choicepicker. Verify every one against Stiler's compiled `main.css`
  before using it, and read the *selector*, not just the name. A name Stiler has never heard of
  renders as a raw browser default inside an otherwise styled page.
- **`munin-explorer*` is ours** and the package owns the whole prefix. Do not write a new
  `variable-*` name: Stiler still defines `.variable-explorer-header`, so that namespace is
  helsedata's and writing in it is either borrowing or colliding.
- **Know what the guards can see.** `test/host-class-names.txt` and the sample stylesheets both
  answer "does some rule declare something for this name". Neither reads Stiler, and neither can
  say which declarations a rule must carry. `skiplink-pagination` was present in both sources for
  the whole time it was broken in a Stiler-only host.

Every new `munin-explorer*` name needs a rule in **both** sample stylesheets, which are one file
copied: `samples/ModernHost/wwwroot/host.css` and `samples/LegacyHost/wwwroot/css/host.css` must
stay byte-identical. Edit one, copy it over the other, run
`./scripts/assert-sample-css-in-step.sh`.

## The package ships no CSS and nothing host-specific

No `wwwroot`, no `.razor.css` — the samples carry styling because they have no Stiler; the
package must not. `scripts/assert-package-contents.sh` enforces it.

The component renders in helsedata's **legacy** Blazor Server as well as a modern Blazor Web App,
so: no `@page`, no `@rendermode`, no `HeadOutlet`, nothing host-specific. `BannedSymbols.txt`
turns that into a build error, and `scripts/assert-portability-guard-armed.sh` exists because
that wiring was quietly stale once and every build stayed green.

There is **no `HttpContext` during circuit activity**. Anything reaching for
`IHttpContextAccessor` finds nothing and fails quietly. See `samples/LegacyHost/Authentication/`
for the pattern that works, and why a token provider must be singleton-safe and resolve the user
per call.

## Text

**Not `IStringLocalizer`.** The legacy host does not call `AddLocalization()`, so injecting one
throws at render time. Both languages live in the `Texts` record (`Blazor/Texts.cs`), and
`ReaderLanguage` is the single place the `Language` parameter is interpreted — the words, the
`lang` marking and date formats, and the `Accept-Language` on outgoing calls all read it, so that
a page cannot end up English in its labels and Norwegian in its dates.

Adding a string means adding both halves. A missing English half is not a compile error.

## Accessibility

WCAG 2.1 AA applies to this package by law. Two things to carry while writing markup:

- `KildeExplorer` is the worked example of a data table done right — a real `<table>`, scoring
  100. Copy its shape rather than inventing one.
- Use `AccessibleName` in the test project for anything a reader operates. It resolves what a
  screen reader would announce and deliberately refuses to count `placeholder` or `title`, both
  of which satisfy a naive "has a naming attribute" check and neither of which is a name.

A green `check-accessibility.sh` is "no detected regression" and never more than that. The full
statement of what the gate cannot see is in `AGENTS.md` under "Accessibility is a requirement,
not a preference" — read it before quoting a pass.

## C# idioms

- **Primary constructors** for services and handlers:
  `internal sealed class MuninExplorerClient(HttpClient httpClient) : IMuninExplorerClient`.
- **Collection expressions**: `private IReadOnlyList<KildeSummary> _kilder = [];`, not
  `new List<KildeSummary>()`.
- `TreatWarningsAsErrors` is on repo-wide. A new warning is a red build, including the banned-API
  guard's `RS0030`.
- **No licence-restricted packages.** AutoMapper and FluentValidation are out — and more so here
  than in Munin, since a dependency this package takes is a dependency it hands every consuming
  host.

## Tests

- xunit + bunit. Method names are English, `Method_WhenCondition_ThenOutcome`.
- Test helpers and fixtures are English too.
- A test comment explains *why the test exists* — what breaks if it is deleted — not what the
  code does.

## Anti-patterns

- Adding a Norwegian identifier "to match Munin". There is no Norwegian half left.
- A new class name verified against another component's markup instead of against Stiler.
- A rule added to one sample stylesheet.
- Narrative incident history in a source file when a bead id would do.
- Reading a passing accessibility scan as a claim of accessibility.
