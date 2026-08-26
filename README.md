# Fhi.Munin.Explorer

The Munin **variabelutforsker** (variable explorer) as a Blazor Razor Class Library, so a host
application can embed Norwegian health-metadata browsing on its own pages.

Built for [helsedata.no](https://helsedata.no) as the first consumer — its Optimizely CMS drops
the component into a page — but the package has no helsedata-specific code and any Blazor host
can consume it.

Data comes from the public Munin Explorer API. **The components are read-only and anonymous**;
everything they render is public metadata and needs no token.

The client reaches one step further than they do. `IMuninExplorerClient` also carries the seven
`api/explorer/my/lists` calls — the signed-in user's saved variable lists — which are the only part
of it that is authenticated, and therefore the only part that needs a host-supplied
`IMuninExplorerTokenProvider` registered *before* `AddMuninExplorer`. Without one they answer 401,
which arrives as a thrown `HttpRequestException` rather than as an empty list. Nothing in this
package calls them yet: they are here so a host can build the list UI on top.

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
    `munin-explorer-pagination` / `munin-explorer-pagination-content`, and the link that jumps past
    the results to it is `munin-explorer-skiplink-pagination`.

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
  The package emits two `<table>`s, for the same reason: the kodeverk code list in an opened panel,
  and the datasamlinger of a kilde in `KildeView`. An element degrades to its own browser default,
  where an unknown class name degrades to nothing.

  Every name in the `munin-explorer` prefix is ours. That is worth saying because it used not to
  be: under the old prefix six names were helsedata's — the container, the results column, the
  header with its `__actions` and `__actions-button`, and the dropdown — and the prefix itself was
  no guide to which was which, so a reader had to check each one against a list. There is no longer
  a category to check against. The `THEIRS` allowlist in `scripts/assert-sample-css-in-step.sh` is
  empty by construction, and what these names cost a host is now the same question everywhere: a
  host on Stiler 0.1.13 or later has rules for them — 0.1.14 for the pager and its skip link, which
  were renamed after 0.1.13 shipped — any other host draws whatever it wants drawn, and the
  sub-lists below are about how much drawing nothing costs.

  - Handles, where something else already dresses the element — a Stiler class it also wears, or
    its own browser default — and the name is there so a host or a test can find that part of the
    component in the page: `munin-explorer` (the root `<section>`), `munin-explorer-filters`,
    `munin-explorer-detail`, `munin-explorer-drilldown`, `munin-explorer-kodeverk*`,
    `munin-explorer-codes*`, `munin-explorer-group`, the nine `munin-explorer-kilde*` names in
    `KildeView`, and the three `munin-explorer-kilder*` names in `KildeExplorer` — the kilde list's
    table, the button that opens a row and the three columns that hold a number. The samples style
    them for arrangement — the root as a grid at desktop width, `-filters`, `-detail`,
    `-drilldown`, `-kodeverk*` and `-codes*` for spacing, indentation and a rule between rows, the
    kilde view's name block, main column and sidebar as a page layout, the kilde list as a table
    with its counts right-aligned — and they draw `munin-explorer-group` as Runa's blue uppercase
    eyebrow. A host that defines none of them loses no information: the group headings, for
    instance, are already sized by the `headline headline-xxs` they wear, so what an undefined
    `munin-explorer-group` costs is the eyebrow's look, not the fact that it is a heading. The
    kilde list is the same bargain twice over, which is why it is a `<table>` of `<button>`s — an
    undrawn table still lines its columns up and an undrawn button is still visibly a control.
    Kelda's facet panel adds two more, `munin-explorer-filters__toggle` and
    `munin-explorer-filters__facets`, and they are handles for the same reason: the folding itself
    is the browser's `hidden` attribute, so a host that defines neither gets a panel that opens and
    closes at every width. What the rules buy is the sidebar — at desktop the samples take the
    folding away and put the toggle off screen, because a button offering to unfold a panel that is
    already open is a control that does nothing.
  - Names that carry meaning nothing else carries, so a host without Stiler's rules has to draw
    them itself: `munin-explorer-crumb` carries the link affordance for a trail step, which is a
    `<button>` — the kilde step of the panel's kilde trail, and every step of the hierarchy trail
    over the results — and without it a trail reads as plain text with no sign it can be pressed;
    `munin-explorer-breadcrumb` with its `__clear` is that hierarchy trail's own wrapper, where the
    chevrons between the steps come from and where the × that empties the hierarchy sits, and an
    undrawn one is a numbered list with a stray × after it; and inside the `munin-explorer-period*`
    wrapper, `__track`, `__fill` and `__track--ongoing` are the period bar itself — only its width
    comes from an inline style, so an undrawn bar renders as nothing at all. The period is still
    legible without it, because the dates are next to it in words, in `__range`.

  Ids are a separate family, each suffixed with a per-instance discriminator so two mounts on one
  page cannot collide: `munin-explorer-title-*`, `-search-*`, `-heading-*`, `-toggle-*`,
  `-detail-*`, `-tab-*`, `-source-*` and the rest. `munin-explorer-source-*` is worth naming,
  because it reads like a class and is not one: the drill-in region it identifies wears the class
  `munin-explorer-drilldown`, so a host or a test reaching for `.munin-explorer-source` comes up
  empty.

  `Render_Always_ThenNoClassNamesAreInventedApartFromTheDomHandles` pins that prefix for a closed
  result list, spelling out its eight names exactly; the panel, drill-in and kilde names are past
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
services.AddMuninExplorer(o => o.ApiBaseUrl = "https://munin.skytest.fhi.no");
```

Leave the provider out entirely and calls are anonymous, which is all public metadata browsing
needs — and all the components this package ships ever do. The variable-list methods
(`GetMyListsAsync` and the six beside it) are the exception: they call an endpoint the API gates
behind a signed-in explorer user, so with no provider registered every one of them throws on the
401 rather than reporting the user as having nothing saved.

Two things about those seven are worth knowing before writing against them. A call naming a list
the user does not have answers `false` — or `null`, for the paged read — because the API cannot
tell "deleted in another tab" from "somebody else's" and deliberately does not try. And the two
batch endpoints take at most `IMuninExplorerClient.MaxVariablesPerBatch` ids, which the client
refuses above rather than splitting: split them yourself with
`ids.Chunk(IMuninExplorerClient.MaxVariablesPerBatch)`, so a failure part-way through leaves you
knowing how far it got.

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
