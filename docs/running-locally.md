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

It talks to `https://munin.skytest.fhi.no`, which is public and read-only, so you get the real ~20 000 variables rather than fixtures.

### Which sample?

Both mount the same component; they differ in *how*, and that difference has caught real bugs.

- **`samples/LegacyHost`** — legacy Blazor Server: `AddServerSideBlazor()` + `MapBlazorHub()`, component mounted in an MVC view with the `<component>` tag helper. **This is the one that mirrors helsedata.** Prefer it.
- **`samples/ModernHost`** — a Blazor Web App with `MapRazorComponents<App>()`. <http://localhost:5087>, or `--launch-profile https` for <https://localhost:7079>.

A component that only ever ran in ModernHost can break in LegacyHost. That is why both exist.

### Pointing it somewhere else

The base URL comes from `MuninExplorer:ApiBaseUrl`, or the environment variable `MuninExplorer__ApiBaseUrl`:

```bash
MuninExplorer__ApiBaseUrl=https://localhost:7134 dotnet run --project samples/LegacyHost
```

In Development the samples fall back to the test API if you set nothing. Outside Development startup fails loudly rather than silently calling nowhere.

### What it will not tell you

The sample hosts carry their **own** CSS in `wwwroot`, hand-written to stand in for helsedata's stylesheet — the package itself ships no CSS at all. So the samples show you *layout and behaviour*, not *what it looks like on helsedata.no*. If a change is about appearance, verify it in the real host (below) or against helsedata's compiled stylesheet; a screenshot from a sample host proves nothing about their site.

---

## 2. Inside helsedata's own site

Heavier, and only worth it when the question is styling or authentication.

### You need

- Their repositories, cloned as siblings (umbrella + `Fhi.Helsedata`, `Fhi.Helsedata.Helseid`, `Fhi.Helsedata.ServiceDefaults`). Access is via FHI's Azure DevOps.
- **Podman or Docker** for the SQL Server container.
- Access to their Azure DevOps NuGet feed — their projects restore Optimizely packages from it. Note this feed is declared in *their* repo config; do not add it to your machine-wide `NuGet.Config`, or every unrelated build on your machine starts consulting an authenticated feed and failing when the token expires.

### Wiring our component in

Add a project reference from their `Fhi.Helsedata.Optimizely.csproj` to `src/Fhi.Munin.Explorer`, register it in `Startup.cs` before the CMS registrations:

> Until 2026-08-21 this was **two** references, to `src/Fhi.Munin.Explorer.Blazor` and `src/Fhi.Munin.Explorer.Client`. The three projects were merged into one that day. A checkout of theirs still carrying the old pair fails to build with `CS0234: The type or namespace name 'Munin' does not exist in the namespace 'Fhi'`, which reads like a missing package and is really a path that no longer exists.


```csharp
services.AddMuninExplorer(o => o.ApiBaseUrl = "https://munin.skytest.fhi.no");
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

The site comes up on **`https://localhost:5001`** — title "Finn helsedata". `:5000` is bound too and answers 404, so it looks like the site is broken when you have the wrong one of the pair; the backend API is on `:5064`/`:7245` (Scalar) and `:7150` answers 401 by design. Read the dashboard's resource list rather than guessing, since Aspire assigns these.

It does not come up quickly: Optimizely's CMS boot plus the database migrations take minutes on a cold container, during which the port is already bound and simply never answers. That is not a hang.

To prove the local Stiler actually reached the page, ask their site for it rather than trusting the flag:

```bash
curl -sk https://localhost:5001/_content/Fhi.Helsedata.Stiler/css/main.css | grep -c munin-explorer
```

A non-zero count means the ProjectReference won. Zero means the PackageReference did, and the flag did not take.

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

Put what comes back into the user secret so the next run needs no environment variable at all:

```bash
dotnet user-secrets --project Helsedata.AppHost set "Parameters:helsedata-sql-password" '<password>'
```

Deleting the container to get a fresh password works too, and costs a full legacy restore from the `.bak` — do that only when the database is disposable.

**The container runtime may be asleep.** `podman machine list` shows the VM; if it has not run for a while it is stopped, and every `podman` command answers `Cannot connect to Podman ... target machine actively refused it` rather than saying so. `podman machine start` fixes it. On Windows the binary is often not on `PATH` — it lives at `C:\Program Files\RedHat\Podman\podman.exe`.

---

## Verifying the real thing

- **Styling**: check class names against helsedata's compiled stylesheet, not against a list or another component's markup. See [`AGENTS.md`](../AGENTS.md).
- **Authentication**: the signed-in path cannot be exercised from a sample host — it needs a real ID-porten session in their app. See `samples/LegacyHost/Authentication/` for the pattern a host must implement, and note that their ID-porten access tokens are short-lived, so a token that worked a few minutes ago will not work now.
