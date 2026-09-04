# Running it locally

Two ways, and **you almost certainly want the first**.

| | What you get | What you need |
| --- | --- | --- |
| [Sample host](#1-sample-host-the-normal-way) | The component, real data, hot reload | .NET 10 SDK. Nothing else. |
| [Inside helsedata](#2-inside-helsedatas-own-site) | The component in their real chrome, real login | Their repos, Podman/Docker, Azure DevOps access |

The second exists to answer two questions the first cannot: *does it look right in their design*, and *does the signed-in user's token actually reach Munin*. For everything else the sample host is faster and has fewer ways to go wrong.

---

## 1. Sample host (the normal way)

```bash
dotnet run --project samples/LegacyHost
```

Then open **<http://localhost:5113>**. That's it — no API key, no database, no login.

`dotnet run` picks the first launch profile, which is the plain-HTTP one. For HTTPS
(<https://localhost:7294>) ask for it explicitly:

```bash
dotnet run --project samples/LegacyHost --launch-profile https
```

It talks to `https://runa.munin.skytest.fhi.no`, which is read-only, so you get the real ~20 000 variables
rather than fixtures.

> **Use the `runa` host, never the same name without a prefix.** The unprefixed host resolves to a private
> address and is reachable only from inside FHI's network. `runa` — and `kelda` — are published externally
> by the GitOps ingress, and both route `/api/explorer/*` to the same API. So either hostname serves
> *both* components: the two names brand the two UIs, not two backends.
> A host that copies the internal address works on the FHI network and fails silently once deployed
> anywhere else, which is exactly what happened to helsedata's test environment on 2026-08-28.

### Which sample?

Both mount the same component; they differ in *how*, and that difference has caught real bugs.

- **`samples/LegacyHost`** — legacy Blazor Server: `AddServerSideBlazor()` + `MapBlazorHub()`, component mounted in an MVC view with the `<component>` tag helper. **This is the one that mirrors helsedata.** Prefer it.
- **`samples/ModernHost`** — a Blazor Web App with `MapRazorComponents<App>()`. <http://localhost:5087>, or `--launch-profile https` for <https://localhost:7079>.
- **`samples/HostileHost`** — legacy Blazor Server like LegacyHost, but wearing helsedata's own
  chrome and their **real** stylesheet: a `PackageReference` to `Fhi.Helsedata.Stiler` served at
  the same `_content/…/main.css` path their layout links, and `.main-header`, which is
  `position: absolute; top: 0` over a 64px row and therefore covers the page's first 64px.
  <http://localhost:5121>. Read the next section before reaching for it.

A component that only ever ran in ModernHost can break in LegacyHost. That is why both exist.

### HostileHost needs feed credentials, and is not in the solution

It is the only project here with a `PackageReference` to `Fhi.Helsedata.Stiler`, which lives on
helsedata's private Azure Artifacts feed. So:

- **It is deliberately absent from `Fhi.Munin.Explorer.slnx`.** A root `dotnet restore` would
  otherwise drag the private feed into every build on every machine. `nuget.config`'s
  `packageSourceMapping` does the rest: NuGet consults only the sources whose patterns match a
  package id, so a restore that never asks for `Fhi.Helsedata.*` never contacts that feed and never
  needs a token. Verified by restoring the solution with credential providers disabled entirely.
- **Locally you need the Azure Artifacts Credential Provider**, the same one the "Inside
  helsedata's own site" section below describes. With it installed, `dotnet run --project
  samples/HostileHost` just works; without it the restore fails with the 401-shaped `NU1301` that
  section warns about.
- **In CI it needs the `AZURE_ARTIFACTS_PAT` repository secret**, which the `layout in helsedata's
  stylesheet` job turns into `VSS_NUGET_EXTERNAL_FEED_ENDPOINTS`. Without the secret that job skips
  itself rather than failing.

### What it is for

```bash
./scripts/check-hostile-host.sh
```

Starts the stub API and HostileHost, drives the explorer into two states, and measures it with
`getBoundingClientRect` at three widths before running axe over the same page. It exists because
four layout defects reached a branch on 2026-09-03 that 1317 unit tests and eight axe states did
not see, and two of the four were collisions with rules only Stiler has — so no hand-written
stand-in reproduces them. `scripts/geometry-assertions.mjs` says what it asks and why, including
which assertions are general invariants and which are replays.

### Pointing it somewhere else

The base URL comes from `MuninExplorer:ApiBaseUrl`, or the environment variable `MuninExplorer__ApiBaseUrl`:

```bash
MuninExplorer__ApiBaseUrl=https://localhost:7134 dotnet run --project samples/LegacyHost
```

In Development the samples fall back to the test API if you set nothing. Outside Development startup fails loudly rather than silently calling nowhere.

### What it will not tell you

LegacyHost and ModernHost carry their **own** CSS in `wwwroot`, hand-written to stand in for helsedata's stylesheet — the package itself ships no CSS at all. So those two show you *layout and behaviour*, not *what it looks like on helsedata.no*. If a change is about appearance, verify it in HostileHost, in the real host (below), or against helsedata's compiled stylesheet; a screenshot from LegacyHost or ModernHost proves nothing about their site.

HostileHost is the exception and is why it exists: its CSS is the real package. It still proves less than the real host does — it has no CMS chrome around the mount, no sign-in, and one page — but a rule of theirs that collides with our markup shows up there and in nothing else we run.

---

## 2. Inside helsedata's own site

Heavier, and only worth it when the question is styling or authentication.

### You need

- Their repositories, cloned as siblings (umbrella + `Fhi.Helsedata`, `Fhi.Helsedata.Helseid`, `Fhi.Helsedata.ServiceDefaults`). Access is via FHI's Azure DevOps.
- **Podman or Docker** for the SQL Server container.
- Access to their Azure DevOps NuGet feed — their projects restore Optimizely packages from it. Note this feed is declared in *their* repo config; do not add it to your machine-wide `NuGet.Config`, or every unrelated build on your machine starts consulting an authenticated feed and failing when the token expires.

  A stale credential fails like a missing package, not like an auth problem:

  ```
  error NU1301: Unable to load the service index for source .../Fhi.Helsedata.no/nuget/v3/index.json.
  error NU1301:   Response status code does not indicate success: 401 (Unauthorized).
  error NU1101: Unable to find package Fhi.Munin.Explorer.
  ```

  Everything then reports as `CS0246: type or namespace 'Munin' not found`, which reads like a broken
  reference. Check the 401 first. Credentials live per-source in `%APPDATA%\NuGet\NuGet.Config` under
  `packageSourceCredentials`; an az token there expires within the hour. The durable fix is the Azure
  Artifacts Credential Provider — without it, every build after expiry looks like the package vanished.

### Wiring our component in

Add a project reference from their `Fhi.Helsedata.Optimizely.csproj` to `src/Fhi.Munin.Explorer`, register it in `Startup.cs` before the CMS registrations:

> Until 2026-08-21 this was **two** references, to `src/Fhi.Munin.Explorer.Blazor` and `src/Fhi.Munin.Explorer.Client`. The three projects were merged into one that day. A checkout of theirs still carrying the old pair fails to build with `CS0234: The type or namespace name 'Munin' does not exist in the namespace 'Fhi'`, which reads like a missing package and is really a path that no longer exists.


```csharp
services.AddMuninExplorer(o => o.ApiBaseUrl = "https://runa.munin.skytest.fhi.no");
```

and mount it in a view with the tag helper — `render-mode="Server"`, **not** `ServerPrerendered`, since prerendering runs `OnInitializedAsync` twice and doubles the API calls.

> **These edits stay local. Never commit or push in helsedata's repositories.**

### Running it

```bash
SQL_SA_PASSWORD=<password> dotnet run --project Helsedata.AppHost
```

Their own docs say `aspire run`, which is the same thing when the Aspire CLI is installed. It often is not on `PATH`, and `aspire: command not found` is the whole error — use the `dotnet run` form above and nothing is lost.

The Aspire dashboard prints a URL with a one-time token in it:

```
Login to the dashboard at https://localhost:17124/login?t=<token>
```

Open **that** link, not the bare port. Without the token the dashboard just asks for one, and the token only appears in the startup output.

Two sites come up on adjacent ports, and it is easy to read the wrong one as broken:

| port | app | title |
|---|---|---|
| **`:5000`** | **Optimizely — the CMS site the component is mounted in** | "Helsedata.no - for forskning, helseanalyse …" |
| `:5001` | helseid-web, a separate app | "Finn helsedata" |

Measured 2026-08-28. An earlier version of this page had these the other way round; if `:5001` shows only
a HelseID login card and no Munin content, you are on the wrong one of the pair rather than looking at a
failure. The backend API is on `:5064`/`:7245` (Scalar) and `:7150` answers 401 by design. Read the
dashboard's resource list rather than guessing, since Aspire assigns these.

It does not come up quickly: Optimizely's CMS boot plus the database migrations take minutes on a cold container, during which the port is already bound and simply never answers. That is not a hang.

### A page of theirs is content, not just code

Their pages live in the CMS database as well as in the assembly, so pulling their code is not enough to
make one appear. If a page they added 404s, or a card on the front page bounces to the home page, the
page type exists and the *content* does not — restore their database.

Backups are committed to `Helsedata.AppHost/db-backup/`. The documented flow is: stop `Optimizely` in the
dashboard, then on the `HelsedataSql` container use `...` → **Database Restore**, then start `Optimizely`
again.

**Stopping first is the part that matters.** Optimizely serves a cached content tree, so a restore under a
running site changes nothing you can see — the same 404, the same dead link — and it looks as though the
restore failed. It did not; the app simply never re-read. Measured 2026-08-28: after restoring beneath a
running site, `/muninvariabelutforsker/` still 404'd and the front-page card still pointed at a dead
`/link/<guid>.aspx`; after a restart the card's href became `/muninvariabelutforsker/` and the page
returned 200, with no further change to the database.

To check whether the content is actually in the restored database rather than guessing at the cache,
ask it directly:

```sql
SELECT c.pkID, cl.Name, cl.URLSegment
FROM tblContent c JOIN tblContentLanguage cl ON cl.fkContentID = c.pkID
WHERE cl.URLSegment LIKE '%munin%';
```

### The package they pin is usually behind

Their `csproj` pins a published `Fhi.Munin.Explorer` version, and the feed only ever carries prereleases.
A component added after that cut simply does not exist for them: on 2026-08-28 their `main` pinned
`0.1.0-alpha.5`, which predates `VariableListView` (added in #80), so a view mounting it failed with
`CS0246: The type or namespace name 'VariableListView' could not be found` — which reads like a typo and
is really a version gap. `KildeExplorer` is missing from `alpha.4` the same way.

For local work, do not chase this with a new release. Their `csproj` carries a dev-loop flag that
references our source instead of the package:

```bash
dotnet run --project Helsedata.AppHost -p:UseLocalStiler=true -p:UseLocalMuninExplorer=true
```

To prove the local Stiler actually reached the page, compare what the site serves against the file on disk:

```bash
curl -sk https://localhost:5000/_content/Fhi.Helsedata.Stiler/css/main.css | sha256sum
sha256sum ../Fhi.Helsedata.Stiler/wwwroot/css/main.css
```

Matching hashes mean the ProjectReference won. Different ones mean the PackageReference did and the flag did not take — the published package is a different build.

Grepping for a class name does not answer this. Any name you would think to grep for is either in both copies, or is one you have only just added locally and have not published, in which case the count is zero whichever reference won.

### Working against a local Stiler

Styling questions need our stylesheet changes in the page, not the published package. Their `Fhi.Helsedata.Optimizely.csproj` has a switch for exactly this:

```bash
UseLocalStiler=true dotnet run --project Helsedata.AppHost
```

It swaps the `Fhi.Helsedata.Stiler` PackageReference for a ProjectReference against the Stiler repo in the umbrella, so `_content/Fhi.Helsedata.Stiler/css/main.css` is served straight from that repo's `wwwroot/` on disk. Run `npm run watch` there and edits show up on refresh, with no pack and no restore. CI never sets the flag.

### Two things that will waste your afternoon

**The SQL port.** The AppHost pins a fixed port for SQL Server, deliberately — Aspire's proxy binds it and the rest of the graph depends on it being stable. If that port happens to sit inside a Windows *excluded* range (reserved by Hyper-V/WSL), Windows refuses the bind and the whole run hangs with SQL never going healthy. It looks like a container-runtime problem and is not; switching from Podman to Docker changes nothing.

```
netsh interface ipv4 show excludedportrange protocol=tcp
```

If the pinned port falls in a listed range, change it in `Helsedata.AppHost/Program.cs`. The ranges are reassigned on reboot and differ per machine, so this can appear from one day to the next.

**The SA password.** There is no default. It lives in the environment or in the AppHost's user secrets (`Parameters:helsedata-sql-password`). If a SQL container already exists from an earlier run, it was created with a specific password and a different one will not open it.

That combination is a dead end the first time you meet it: the container is declared `ContainerLifetime.Persistent`, so it is reused rather than recreated, and nobody wrote the password down. It is not lost — the container carries it in its own environment:

```bash
podman inspect HelsedataSql-<hash> --format '{{range .Config.Env}}{{println .}}{{end}}' | grep SA_PASSWORD
```

Put it straight into the user secret so the next run needs no environment variable at all. Keep it in a variable rather than typing it — a password given as a command-line argument is visible to anything reading the process list, and lands in shell history:

```bash
PW=$(podman inspect HelsedataSql-<hash> --format '{{range .Config.Env}}{{println .}}{{end}}' \
     | sed -n 's/^MSSQL_SA_PASSWORD=//p')
dotnet user-secrets --project Helsedata.AppHost set "Parameters:helsedata-sql-password" "$PW"
unset PW
```

Deleting the container to get a fresh password works too, and costs a full legacy restore from the `.bak` — do that only when the database is disposable.

**The container runtime may be asleep.** `podman machine list` shows the VM; if it has not run for a while it is stopped, and every `podman` command answers `Cannot connect to Podman ... target machine actively refused it` rather than saying so. `podman machine start` fixes it. On Windows the binary is often not on `PATH` — it lives at `C:\Program Files\RedHat\Podman\podman.exe`.

---

## Verifying the real thing

- **Styling**: check class names against helsedata's compiled stylesheet, not against a list or another component's markup. See [`AGENTS.md`](../AGENTS.md).
- **Authentication**: the signed-in path cannot be exercised from a sample host — it needs a real ID-porten session in their app. See `samples/LegacyHost/Authentication/` for the pattern a host must implement, and note that their ID-porten access tokens are short-lived, so a token that worked a few minutes ago will not work now.
