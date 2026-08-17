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

## Rules the components follow

These are not style preferences — each one is a host that breaks otherwise.

- **No `@page`.** There is no router in the Optimizely host; the CMS owns routing. The explorer
  is a single parameterised root component.
- **No `@rendermode`.** The host decides, at the mount site. This is what lets one package serve
  both a legacy and a modern host.
- **No CSS, no `wwwroot`, no `.razor.css`.** Styling comes from the host's
  `Fhi.Helsedata.Stiler` stylesheet — and the class names the markup emits are Stiler's own
  (`searchbox__freetext`, `hd-button-square`, `datasourcecard*`, …), not names of our own
  invention. A name Stiler has never heard of renders as a raw browser default inside an
  otherwise styled page, which defeats the point of shipping this as a component at all. Where
  Stiler has no rule for a shape, change the shape rather than adding a stylesheet: results are
  a `datasourcecard` list, not a table, because Stiler styles no table this package could use.
- **No `HeadOutlet`.** Not available in the Optimizely host — the component cannot set the page
  title or inject meta tags.
- **Nothing host-specific.** `IHttpContextAccessor`, `Microsoft.AspNetCore.Components.Server.*`,
  EF Core, `EPiServer.*` / `Optimizely.*` and `System.IO` file access are **build errors** in the
  RCL, enforced by `BannedSymbols.txt` and `Microsoft.CodeAnalysis.BannedApiAnalyzers`.

If a callback parameter is added, note that an `EventCallback` silently serialises to an empty
delegate across a static-SSR to interactive-island boundary — such a mount point has to be fully
interactive.

## Build

```bash
dotnet build
dotnet test
```

Requires the .NET 10 SDK. The target framework is set once in `Directory.Build.props`, never in
the individual project files.

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
services.AddSingleton<IMuninExplorerTokenProvider, MinTokenProvider>();
services.AddMuninExplorer(o => o.ApiBaseUrl = "https://munin.skytest.fhi.no");
```

Leave the provider out entirely and calls are anonymous, which is all public metadata browsing
needs.

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

Requires the repository secret `NUGET_API_KEY` — an API key from nuget.org scoped to the
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
