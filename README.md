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
style **every** `variable-explorer*` class name the package invents. Those names are inert until
some host supplies a rule, so a name with no rule renders at raw browser defaults in both samples
at once — which reads as a bug in the component. Agreeing and being right are different claims,
and the second clause is the one that checks the second.

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
  - `variables.css`, the second stylesheet, for the whole result vocabulary. Since
    `Fhi.Metadata-zs56s` the component renders helsedata's own variable page's DOM rather than a
    shape of its own: rows are `variable-data-list` / `variable-data-list__item*` /
    `variable-dataitem-main*`, the opened panel is `variable-meta*`, the list around them is
    `variable-explorer-container` / `variable-explorer-results` / `variable-explorer-header*`, the
    pager is `variables-pagination*` plus `skiplink-pagination`, and the column picker is
    `variable-explorer-header__actions*` with `dropdown-choicepicker*`. Despite the name it is
    served on every page of helsedata.no — verified on `/no/`, `/no/variabler/` and
    `/no/datakilder/`, which load an identical seven bundles — so a host in their estate has it
    wherever the component is mounted. A host outside has to supply all of it, including the rule
    that keeps `skiplink-pagination` out of sight until it is focused.

  A name neither stylesheet has heard of renders as a raw browser default inside an otherwise
  styled page, which defeats the point of shipping this as a component at all. So where neither
  has a rule for a shape, change the shape rather than adding a stylesheet: the filter panel is
  `<details>` plus a nested `<ul>` rather than an accordion and a tree, and the detail panel is a
  `<dl>` with an `<ol>` for the kilde trail, because neither stylesheet names any of those. What a
  host supplies for them is base element styling — list indentation in particular, which is what
  shows a delkilde sitting under its kilde. The package emits two `<table>`s, for the same reason:
  the kodeverk code list in an opened panel, and the datasamlinger of a kilde in `KildeView`. An
  element degrades to its own browser default, where an unknown class name degrades to nothing.

  A `variable-explorer` prefix does not mean the name is ours. Six of them are helsedata's, every
  one read back off their compiled `variables.css`: `variable-explorer-container`,
  `variable-explorer-results`, `variable-explorer-header` with its `__actions` and
  `__actions-button`, and `variable-explorer__dropdown`. Every other class name in the prefix is
  ours, and the one thing true of all of them is narrow: **neither helsedata stylesheet has a rule
  for any of them**. A host in their estate gets nothing for free here, and a host outside has to
  draw whatever it wants drawn. What differs between them is how much drawing nothing costs.

  - Handles, where something else already dresses the element — a Stiler class it also wears, or
    its own browser default — and the name is there so a host or a test can find that part of the
    component in the page: `variable-explorer` (the root `<section>`), `variable-explorer-filters`,
    `variable-explorer-detail`, `variable-explorer-drilldown`, `variable-explorer-kodeverk*`,
    `variable-explorer-codes*`, `variable-explorer-group`, and the nine `variable-explorer-kilde*`
    names in `KildeView`. What the samples do with them is uneven, and worth knowing before
    following the pointer below: they style the panel handles for arrangement — the root as a grid
    at desktop width, `-filters`, `-detail`, `-drilldown`, `-kodeverk*` and `-codes*` for spacing,
    indentation and a rule between rows — and they draw `variable-explorer-group` as Runa's blue
    uppercase eyebrow. The nine `KildeView` names have no rule in either sample; that view is left
    to its base element styling and the Stiler class each of its parts also wears. A host that
    defines none of them loses no information: the group headings, for instance, are already sized
    by the `headline headline-xxs` they wear, so what an undefined `variable-explorer-group` costs
    is the eyebrow's look, not the fact that it is a heading.
  - Names a host outside helsedata has to draw itself, because nothing else draws them:
    `variable-explorer-crumb` carries the link affordance for a trail step, which is a `<button>`
    — the kilde step of the panel's kilde trail, and every step of the hierarchy trail over the
    results — and without it a trail reads as plain text with no sign it can be pressed;
    `variable-explorer-breadcrumb` with its `__clear` is that hierarchy trail's own wrapper, where
    the chevrons between the steps come from and where the × that empties the hierarchy sits, and
    an undrawn one is a numbered list with a stray × after it; and inside the
    `variable-explorer-period*` wrapper, `__track`, `__fill` and
    `__track--ongoing` are the period bar itself — only its width comes from an inline style, so an
    undrawn bar renders as nothing at all. The period is still legible without it, because the
    dates are next to it in words, in `__range`.

  Ids are a separate family, each suffixed with a per-instance discriminator so two mounts on one
  page cannot collide: `variable-explorer-title-*`, `-search-*`, `-heading-*`, `-toggle-*`,
  `-detail-*`, `-tab-*`, `-source-*` and the rest. `variable-explorer-source-*` is worth naming,
  because it reads like a class and is not one: the drill-in region it identifies wears the class
  `variable-explorer-drilldown`, so a host or a test reaching for `.variable-explorer-source` comes
  up empty.

  `Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles` pins that prefix for a closed
  result list, spelling out which of its eight names are ours and which are theirs; the panel,
  drill-in and kilde names are past its reach, because nothing is expanded there. For seeing the
  whole thing dressed, the sample hosts' `host.css` stands in for both stylesheets, divided by
  comment into which rules stand in for which.
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

One package. The component, the client that feeds it and the types they share all ship together.

```bash
dotnet add package Fhi.Munin.Explorer
```

It was three for a while — component, client and contracts — so that the component need not
depend on an HTTP stack and a host could substitute its own `IMuninExplorerClient`. That seam is
still here: `IMuninExplorerClient` is an interface, and a host that registers its own
implementation never touches ours. What went away is the part nobody used — three versions that
had to move in lockstep, and a state where the component was installed and the client was not, so
it rendered with nothing behind it.

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
the package shape and pushes all three packages in dependency order to
`Fhi.Helsedata.no`, the Azure Artifacts feed helsedata's own projects already restore from. The
packages are internal, not public: nothing goes to nuget.org.

The workflow refuses to publish a tag whose commit is not on `main`, a tag that is not a clean
`vMAJOR.MINOR.PATCH`, and a build whose packed version disagrees with the tag. The feed does allow
a version to be deleted, but that is not a way back: anyone who restored it keeps what they got,
so a version number that has gone out is spent whether or not the artefact is still there.

If a push fails partway through, **re-run the workflow** — it asks the feed what already went out
and pushes only what is missing. It stops only if *every* package is already published, which
means the tag is being reused rather than a run needing to finish.

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
