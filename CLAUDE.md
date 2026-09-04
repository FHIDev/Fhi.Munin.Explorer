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
   `munin-explorer*`, whose rules ship in Stiler under `components/munin-explorer/` — in 0.1.13,
   except the pager's and its skip link's, which are in 0.1.14. Don't write a new `variable-*`
   name — Stiler still defines `.variable-explorer-header`, so that namespace is helsedata's.
   And know what the guards here can see: both ask whether a name has a rule that declares
   something, in `test/host-class-names.txt` — a capture of the live helsedata page, their own
   page-specific stylesheets included — or in the sample stylesheet. Neither reads Stiler at all,
   and neither can say which declarations a rule has to carry.
   `skiplink-pagination` was in both sources the whole time it was broken, because helsedata
   styles it and so do the samples, while a Stiler-only host got nothing; a rule somewhere is not
   a rule where it is needed, and that host is the one the prefix exists for. Separately: when
   Stiler does carry a rule, read the selector and not just the name. The first attempt at this
   link's rule, on Stiler's unpublished `feature/munin-explorer-scss` branch, was scoped
   `.munin-explorer-header .skiplink-pagination` and could never have matched, since that header
   opens and closes entirely inside `ColumnPicker()`. Reasoning in `AGENTS.md`. The Stiler rule for
   a name you invent is a separate bead of its own — see "Finishing".

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
./scripts/assert-portability-guard-armed.sh     # only if you touched Directory.Build.props
```

- **A `src/` change needs a changelog fragment** in `changelog.d/`. CI fails without one.
  Fragments here are **English only** — deliberately unlike Munin's bilingual `.en.md`/`.nb.md`
  pair. See `changelog.d/README.md`.
- **Comments have a three-line ceiling** — the why, never the what; incident history goes in the
  bead; the package's public XML docs are the exception, because their reader has only the
  package. Full rule in `AGENTS.md` under "Comments". Prose that outgrows the ceiling belongs in
  the bead, the PR description, or `AGENTS.md` — not in a file people reopen on every visit.
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
bd update <bead-id> --claim
bd worktree create .worktrees/<name> --branch feature/<name>
```

Claim with `--claim`, never `--assignee Claude` — the pool is shared with other people's agents
and that string is the same on every box. AGENTS.md, "Claiming a bead names whose agent it is",
has the reason.

Use `bd worktree` rather than raw `git worktree` — it writes the `.beads/redirect` the new
checkout needs to find the shared database.

### Finishing

- `bd preflight --check` before opening the PR.
- **If the change touches markup, run `./scripts/check-accessibility.sh`.** WCAG 2.1 AA applies
  to this package by law, and green means no detected regression rather than accessible — what the
  gate cannot see is in AGENTS.md under "Accessibility is a requirement, not a preference". CI runs
  the same script, so a red check there is never a surprise.
- **If the change touches layout, run `./scripts/check-hostile-host.sh` as well.** It renders the
  component in helsedata's real stylesheet under their top-anchored header and measures boxes, which
  is the only thing here that would have caught the four defects of 2026-09-03 — axe was green
  through every one. It needs Azure Artifacts credentials; see `docs/running-locally.md`.
- **A new or renamed `munin-explorer*` name needs a rule in `Fhi.Helsedata.Stiler`, filed as its
  own bead before this PR merges** — `bd create --label=stiler --label=rcl --label=helsedata` —
  not as a clause in the RCL bead's criteria, which is in nobody's `bd ready` and cannot be
  claimed by anyone. Nothing on this side can see that repository, so green here is not evidence the
  element is styled on helsedata.no. Reasoning in `AGENTS.md` under "Class names in markup".
- **Working in Stiler is Azure DevOps, not GitHub.** The checkout this workspace uses is
  `C:\source\fhigit\helsedata\Helsedata.Claude\Fhi.Helsedata.Stiler` — there are other Stiler
  checkouts on the box and they are not it. Rules go under `Static/scss/components/munin-explorer/`,
  one file per area (`_trail.scss`, `_filters.scss`, `_results.scss`, and so on). No `gh`, no
  Copilot review, no `Closes #N`. And `az repos pr create --description` truncates at the first
  newline and turns æøå into question marks: create the PR, then PATCH title and description over
  the REST API with explicit UTF-8 bytes, and read it back.
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

**The tag also assembles the changelog, before it packs** — the version's `changelog.d/` fragments
become one `CHANGELOG.md` section and that section is the package's `PackageReleaseNotes`. Never
run `assemble-changelog.ps1` by hand to release: it was a documented manual step for three weeks
and eight versions shipped without it (`Fhi.Metadata-l9l2n.44`). `-DryRun`, and
`scripts/release-changelog.sh --dry-run`, are the rehearsals. The commit reaches `main` as a pull
request the workflow opens, since `MainRules` has no bypass actors — merging it is the one step
left to a person, and it arrives with its checks unreported because no workflow runs for an event
`GITHUB_TOKEN` caused.

## Host constraints worth remembering

The component must render inside helsedata's **legacy** Blazor Server (`AddServerSideBlazor` +
`MapBlazorHub`, mounted with the `<component>` tag helper) as well as a modern Blazor Web App.
So: no `@page`, no `@rendermode`, no `HeadOutlet`, nothing host-specific. `BannedSymbols.txt`
turns the last one into a build error — and `scripts/assert-portability-guard-armed.sh` is what
keeps that true, because it was quietly not for a while: the ItemGroup wiring the analyzer up is
conditioned on the RCL's project name, and the name it held was one project rename out of date.

There is **no `HttpContext` during circuit activity**. Anything reaching for
`IHttpContextAccessor` finds nothing and fails quietly rather than loudly — see
`samples/LegacyHost/Authentication/` for the pattern that works, and why a token provider must
be singleton-safe and resolve the user per call.
