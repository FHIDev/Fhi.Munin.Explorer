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

Add project references from their `Fhi.Helsedata.Optimizely.csproj` to `src/Fhi.Munin.Explorer.Blazor` and `src/Fhi.Munin.Explorer.Client`, register it in `Startup.cs` before the CMS registrations:

```csharp
services.AddMuninExplorer(o => o.ApiBaseUrl = "https://munin.skytest.fhi.no");
```

and mount it in a view with the tag helper — `render-mode="Server"`, **not** `ServerPrerendered`, since prerendering runs `OnInitializedAsync` twice and doubles the API calls.

> **These edits stay local. Never commit or push in helsedata's repositories.**

### Running it

```bash
SQL_SA_PASSWORD=<password> dotnet run --project Helsedata.AppHost
```

The site comes up on `https://localhost:5000`.

### Two things that will waste your afternoon

**The SQL port.** The AppHost pins a fixed port for SQL Server, deliberately — Aspire's proxy binds it and the rest of the graph depends on it being stable. If that port happens to sit inside a Windows *excluded* range (reserved by Hyper-V/WSL), Windows refuses the bind and the whole run hangs with SQL never going healthy. It looks like a container-runtime problem and is not; switching from Podman to Docker changes nothing.

```
netsh interface ipv4 show excludedportrange protocol=tcp
```

If the pinned port falls in a listed range, change it in `Helsedata.AppHost/Program.cs`. The ranges are reassigned on reboot and differ per machine, so this can appear from one day to the next.

**The SA password.** There is no default. It lives in the environment or in the AppHost's user secrets (`Parameters:helsedata-sql-password`). If a SQL container already exists from an earlier run, it was created with a specific password and a different one will not open it.

---

## Verifying the real thing

- **Styling**: check class names against helsedata's compiled stylesheet, not against a list or another component's markup. See [`AGENTS.md`](../AGENTS.md).
- **Authentication**: the signed-in path cannot be exercised from a sample host — it needs a real ID-porten session in their app. See `samples/LegacyHost/Authentication/` for the pattern a host must implement, and note that their ID-porten access tokens are short-lived, so a token that worked a few minutes ago will not work now.
