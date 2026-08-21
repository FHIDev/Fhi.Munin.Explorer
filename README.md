# Fhi.Munin.Explorer

The Munin **variabelutforsker** (variable explorer) as a Blazor Razor Class Library, so a host
application can embed Norwegian health-metadata browsing on its own pages.

Built for [helsedata.no](https://helsedata.no) as the first consumer — its Optimizely CMS drops
the component into a page — but the package has no helsedata-specific code and any Blazor host
can consume it.

Data comes from the public Munin Explorer API. **v1 is read-only and anonymous**; sign-in and
saved variable lists follow later.

## Layout

| Project | What it is |
| --- | --- |
| `src/Fhi.Munin.Explorer.Contracts` | DTOs and the client interface. No dependencies. |
| `src/Fhi.Munin.Explorer.Blazor` | The RCL — the components a host renders. |
| `src/Fhi.Munin.Explorer.Client` | Typed `HttpClient` implementation + `AddMuninExplorer()`. |
| `samples/ModernHost` | Blazor Web App — the everyday development host. |
| `samples/LegacyHost` | Legacy Blazor Server + MVC — mirrors helsedata's Optimizely host. |
| `test/Fhi.Munin.Explorer.Tests` | bUnit + xUnit. |

Both sample hosts exist on purpose. helsedata's production site runs **legacy** Blazor Server
(`AddServerSideBlazor()` + `MapBlazorHub()`), mounting components inside MVC views with the
`<component>` tag helper. A component that only ever ran in a modern Blazor Web App can break
there in ways that never show up in development.

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
- **No CSS, no `wwwroot`, no `.razor.css`.** Styling comes from the host, and the class names the
  markup emits are not ours to invent — they are read back off helsedata's own compiled
  stylesheets. Two of them, and the difference matters to a host outside helsedata:
  - `Fhi.Helsedata.Stiler`, the site-wide stylesheet, for the parts that are ordinary page
    furniture: `searchbox__freetext*`, `hd-button-square` with its `button-square--*` modifiers,
    `form-element__label`, `form-fieldset`, `headline`, `caption`, `infobox`, `hd-button-reset`,
    `screenreader-only`.
  - `variables.css`, which helsedata's own variable page carries, for the whole result vocabulary.
    Since `Fhi.Metadata-zs56s` the component renders that page's DOM rather than a shape of its
    own: rows are `variable-data-list` / `variable-data-list__item*` / `variable-dataitem-main*`,
    the opened panel is `variable-meta*`, the list around them is `variable-explorer-container` /
    `variable-explorer-results` / `variable-explorer-header*`, the pager is `variables-pagination*`
    plus `skiplink-pagination`, and the column picker is `variable-explorer-header__actions*` with
    `dropdown-choicepicker*`. Despite the name it is served on every page of helsedata.no —
    verified on `/no/`, `/no/variabler/` and `/no/datakilder/`, which load an identical seven
    bundles — so a host in their estate has it wherever the component is mounted. A host outside
    has to supply all of it, including the rule that keeps `skiplink-pagination` out of sight
    until it is focused.

  A name neither stylesheet has heard of renders as a raw browser default inside an otherwise
  styled page, which defeats the point of shipping this as a component at all. So where neither
  has a rule for a shape, change the shape rather than adding a stylesheet: the filter panel is
  `<details>` plus a nested `<ul>` rather than an accordion and a tree, and the detail panel is a
  `<dl>` with an `<ol>` for the kilde trail, because neither stylesheet names any of those. What a
  host supplies for them is base element styling — list indentation in particular, which is what
  shows a delkilde sitting under its kilde. The one `<table>` in the package is the kodeverk code
  list, for the same reason: an element degrades to its own browser default, where an unknown
  class name degrades to nothing.

  A `variable-explorer` prefix does not mean the name is ours: `variable-explorer-container`,
  `variable-explorer-results`, `variable-explorer-header*` and `variable-explorer__dropdown` are
  all helsedata's. The names this package does invent are DOM handles that carry no styling
  anywhere: `variable-explorer` and `variable-explorer-filters`, plus `variable-explorer-detail`,
  `variable-explorer-source`, `variable-explorer-kodeverk*` and `variable-explorer-codes*` inside
  an opened panel. They exist so a host — or a test — can find a part of the component in the
  page, and a host that defines none of them loses nothing visual.
  `Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles` pins that prefix for a closed
  result list, spelling out which of its eight names are ours and which are theirs; the panel
  handles are past its reach, because nothing is expanded there. For seeing the whole thing
  dressed, the sample hosts' `host.css` stands in for both stylesheets, divided by comment into
  which rules stand in for which.
- **No `HeadOutlet`.** Not available in the Optimizely host — the component cannot set the page
  title or inject meta tags.
- **Nothing host-specific.** `IHttpContextAccessor`, `Microsoft.AspNetCore.Components.Server.*`,
  EF Core, `EPiServer.*` / `Optimizely.*` and `System.IO` file access are **build errors** in the
  RCL, enforced by `BannedSymbols.txt` and `Microsoft.CodeAnalysis.BannedApiAnalyzers`.

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

A host needs **two** packages. The component and its data source are separate so a host can
supply its own `IMuninExplorerClient` — but install only the first and the component renders
with nothing behind it.

```bash
dotnet add package Fhi.Munin.Explorer.Blazor   # the component
dotnet add package Fhi.Munin.Explorer.Client   # the data client + AddMuninExplorer()
```

`Fhi.Munin.Explorer.Contracts` comes along as a dependency of both; install it directly only if
you are implementing one of the interfaces without taking the rest.

```csharp
// Registration order matters. To call Munin as the signed-in user, register the token provider
// BEFORE AddMuninExplorer — it uses TryAdd, so the anonymous default wins if it goes first and
// the explorer will quietly keep calling without a token.
services.AddSingleton<IMuninExplorerTokenProvider, MyTokenProvider>();
services.AddMuninExplorer(o => o.ApiBaseUrl = "https://munin.skytest.fhi.no");
```

Leave the provider out entirely and calls are anonymous, which is all public metadata browsing
needs.

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
the package shape and pushes all three packages to nuget.org in dependency order.

The workflow refuses to publish a tag whose commit is not on `main`, a tag that is not a clean
`vMAJOR.MINOR.PATCH`, and a build whose packed version disagrees with the tag. Those refusals
exist because nuget.org is append-only: a version can be unlisted but never replaced, and a
package id can never be reclaimed.

If a push fails partway through, **re-run the workflow** — it asks nuget.org what already went
out and pushes only what is missing. It stops only if *every* package is already published,
which means the tag is being reused rather than a run needing to finish.

Requires the secret `NUGET_ORG_FHI_PUBLISH` — the same name FHI already publishes with in
`FHIDev/Fhi.HelseId`. If that exists as an organisation secret, this repository only needs to be
granted access to it; otherwise it is an API key from nuget.org scoped to the
`Fhi.Munin.Explorer.*` ids.

To check the package shape yourself before tagging:

```bash
dotnet pack -c Release -o artifacts
./scripts/assert-package-contents.sh artifacts
```

Versions stay on `0.x` until the helsedata POC is wired up and the API surface has stopped
moving — `1.0.0` is a stability promise, and on nuget.org it cannot be walked back.

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
