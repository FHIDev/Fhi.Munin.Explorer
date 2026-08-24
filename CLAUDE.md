# Claude Code Instructions

Instructions for Claude Code working in `Fhi.Munin.Explorer` — the Munin variable explorer
packaged as a Blazor Razor Class Library for helsedata.no.

**Read [`AGENTS.md`](AGENTS.md) first.** It is canonical for all AI tools and covers the
conventions a compiler cannot check. This file adds the Claude-Code-specific workflow.

---

## The rules that bite

Four things here fail *silently* when broken — no build error, no failing test, just wrong
behaviour found later by someone else.

1. **Identifiers are English.** Norwegian is for user-facing strings and untranslatable domain
   terms (`kilde`, `datasamling`, `variabelgruppe`, `kildetype`, `kodeverk`). The Norwegian
   names the package started with were renamed under `Fhi.Metadata-osxfx`, before the first
   publish to the feed, so there is no Norwegian half left to add to. A DTO property's C# name is
   free to differ from its wire name — every one carries a `[JsonPropertyName]`, so the JSON
   keeps Munin's spelling whatever the property is called. Reasoning in `AGENTS.md`.

2. **Borrowed class names belong to `Fhi.Helsedata.Stiler`; the `munin-explorer` prefix is
   ours.** Ordinary page furniture — search field, buttons, headings, infobox, choicepicker —
   wears Stiler's own name, and you verify every one against Stiler's compiled `main.css` before
   using it. A name Stiler has never heard of renders as a raw browser default inside an otherwise
   styled page — the exact failure this package exists to avoid, and it has happened twice. The
   explorer's own structure and result vocabulary is the other half: it used to write helsedata's
   `variable-*` names and inherit rules from their `variables.css`, and now writes
   `munin-explorer*`, whose rules ship in Stiler 0.1.13 under `components/munin-explorer/`. Don't
   write a new `variable-*` name — Stiler still defines `.variable-explorer-header`, so that
   namespace is helsedata's. Reasoning in `AGENTS.md`.

3. **The package ships no CSS.** No `wwwroot`, no `.razor.css`. Sample hosts carry their own
   styling because they have no Stiler; the package must not.
   `scripts/assert-package-contents.sh` enforces it and runs in CI on every PR.

4. **The two sample stylesheets are one file, copied — and they style every name we invent.**
   `samples/ModernHost/wwwroot/host.css` and `samples/LegacyHost/wwwroot/css/host.css` must be
   byte-identical, and between them must have a rule for every `munin-explorer*` class the
   package invents. The samples carry no Stiler, so they are the only stylesheet those names have
   here. Edit one, copy it over the other. Both halves have already failed quietly: the kodeverk
   block reached LegacyHost only, and the kilde view shipped with rules for none of its nine names
   in either copy. `scripts/assert-sample-css-in-step.sh` enforces both and runs in CI on every
   PR.

---

## Before opening a PR

```bash
dotnet build && dotnet test
dotnet pack -c Release -o artifacts && ./scripts/assert-package-contents.sh artifacts
./scripts/assert-sample-css-in-step.sh          # only if you touched samples/ or a class name
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

### Every bead for this repository gets `helsedata` and `rcl`

```bash
bd create --title="..." --description="..." --label=helsedata --label=rcl
```

Add `--label=kelda` for kildeutforsker work.

Because the database is Munin's, a bead for this repository sits among hundreds that are not, and
the label is the only thing separating them — `bd list --label=rcl` is how anyone finds this
repository's work at all. Beads created ad hoc drifted out of that: eleven of them had no labels
by 19 August 2026 and were invisible to the filter that was supposed to find them, which is a
quiet failure — the list still returns results, just not all of them.

Labels also cost nothing to add and are awkward to add later, since finding what to fix means
already knowing what the filter missed.

**Never run `bd label list-all`** to check what exists — see the hazard below. `bd list --json`
carries every bead's labels and is safe.

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

### One hazard worth knowing

**Never run `bd label list-all`.** It once blocked the shared Dolt cluster for thirteen minutes.
Label directly with `bd create --label X` or `bd update <id> --add-label X`.

## Publishing

Tag-triggered: `git tag v0.2.0 && git push origin v0.2.0`. The one package, `Fhi.Munin.Explorer`,
goes to `Fhi.Helsedata.no`, the Azure Artifacts feed helsedata restores from — **not** nuget.org,
which this package has never been published to. Requires the `ADO_PACKAGING_TOKEN` secret. The
workflow refuses a tag whose commit is not on `main`, a malformed version, a packed version that
disagrees with the tag, and a version already on the feed: deleting one there does not take it
back from anyone who restored it. See "Releasing" in `README.md` for the rest.

## Host constraints worth remembering

The component must render inside helsedata's **legacy** Blazor Server (`AddServerSideBlazor` +
`MapBlazorHub`, mounted with the `<component>` tag helper) as well as a modern Blazor Web App.
So: no `@page`, no `@rendermode`, no `HeadOutlet`, nothing host-specific. `BannedSymbols.txt`
turns the last one into a build error.

There is **no `HttpContext` during circuit activity**. Anything reaching for
`IHttpContextAccessor` finds nothing and fails quietly rather than loudly — see
`samples/LegacyHost/Authentication/` for the pattern that works, and why a token provider must
be singleton-safe and resolve the user per call.
