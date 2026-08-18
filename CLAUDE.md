# Claude Code Instructions

Instructions for Claude Code working in `Fhi.Munin.Explorer` — the Munin variable explorer
packaged as a Blazor Razor Class Library for helsedata.no.

**Read [`AGENTS.md`](AGENTS.md) first.** It is canonical for all AI tools and covers the
conventions a compiler cannot check. This file adds the Claude-Code-specific workflow.

---

## The rules that bite

Three things here fail *silently* when broken — no build error, no failing test, just wrong
behaviour found later by someone else.

1. **Identifiers are English.** Norwegian is for user-facing strings, changelog fragments and
   untranslatable domain terms (`kilde`, `datasamling`, `variabelgruppe`). Existing Norwegian
   names are being renamed under `Fhi.Metadata-osxfx`, which must land **before the first
   nuget.org publish** — the public surface becomes a breaking change afterwards. Reasoning in
   `AGENTS.md`.

2. **Class names in markup belong to `Fhi.Helsedata.Stiler`, not to us.** Verify every one
   against Stiler's compiled `main.css` before using it. A name Stiler has never heard of
   renders as a raw browser default inside an otherwise styled page — the exact failure this
   package exists to avoid, and it has happened twice.

3. **The package ships no CSS.** No `wwwroot`, no `.razor.css`. Sample hosts carry their own
   styling because they have no Stiler; the package must not.
   `scripts/assert-package-contents.sh` enforces it and runs in CI on every PR.

---

## Before opening a PR

```bash
dotnet build && dotnet test
dotnet pack -c Release -o artifacts && ./scripts/assert-package-contents.sh artifacts
```

- **A `src/` change needs a changelog fragment** in `changelog.d/`. CI fails without one.
  Fragments here are **English only** — deliberately unlike Munin's bilingual `.en.md`/`.nb.md`
  pair. See `changelog.d/README.md`.
- `dotnet format` on a Windows checkout reports pre-existing whitespace noise from CRLF. Compare
  the count against untouched `main` before believing it is yours; `.gitattributes` normalises to
  LF, so CI sees clean files.

## Beads

Work is tracked in the **Munin** beads workspace, not this repository's issues — `.beads/redirect`
points there, so `bd` behaves exactly as it does in Munin and sees the same database. Epics:
`Fhi.Metadata-l9l2n` (v1 read-only), `Fhi.Metadata-dfy9u` (v2 login and variable lists),
`Fhi.Metadata-2fomm` (Kelda).

### Starting work

```bash
bd update <bead-id> --status in_progress --assignee Claude
bd worktree create .worktrees/<name> --branch feature/<name>
```

Use `bd worktree` rather than raw `git worktree` — it writes the `.beads/redirect` the new
checkout needs to find the shared database.

### Finishing

- `bd preflight --check` before opening the PR.
- Reference the bead with the **cross-repository** form, since the issues live in Munin:
  `Closes FHIDev/Munin#1234`. Use `Refs` when the PR only partly satisfies the bead — `Closes`
  shuts it whether or not the acceptance criteria are met.
- **Work is not done until it is pushed.** A worktree can be deleted with the session.

### Two hazards specific to this repository

**The forge dispatch tag is not Munin's.** Both anvils resolve beads from the *same* database
through `.beads/redirect`, so the tag is the only thing partitioning them:

| Anvil | Local | Skybert (in-cluster) |
| --- | --- | --- |
| munin | `forgeReady` | `forgeSkybert` |
| **explorer** | **`forgeExplorer`** | **`forgeExplorerSkybert`** |

Label an explorer bead `forgeSkybert` and it surfaces under the *Munin* anvil, which dispatches
it into the Munin checkout — where the component does not exist. Nothing warns you.

**Never run `bd label list-all`.** It once blocked the shared Dolt cluster for thirteen minutes.
Label directly with `bd create --label X` or `bd update <id> --add-label X`.

## Publishing

Tag-triggered: `git tag v0.2.0 && git push origin v0.2.0`. The workflow refuses a tag whose
commit is not on `main`, a malformed version, and a packed version that disagrees with the tag,
because nuget.org is append-only — a version can be unlisted but never replaced. Requires the
`NUGET_ORG_FHI_PUBLISH` secret. See "Releasing" in `README.md`.

## Host constraints worth remembering

The component must render inside helsedata's **legacy** Blazor Server (`AddServerSideBlazor` +
`MapBlazorHub`, mounted with the `<component>` tag helper) as well as a modern Blazor Web App.
So: no `@page`, no `@rendermode`, no `HeadOutlet`, nothing host-specific. `BannedSymbols.txt`
turns the last one into a build error.

There is **no `HttpContext` during circuit activity**. Anything reaching for
`IHttpContextAccessor` finds nothing and fails quietly rather than loudly — see
`samples/LegacyHost/Authentication/` for the pattern that works, and why a token provider must
be singleton-safe and resolve the user per call.
