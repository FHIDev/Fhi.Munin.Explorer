---
name: prove-it
description: >
  Evidence-based verification before committing, pushing, or opening a pull request in
  Fhi.Munin.Explorer. Runs the build, the tests, dotnet format and the repository's own guard
  scripts, checks the changelog fragment and the comment budget, and checks the pull request
  reference is the cross-repository form. Use instead of declaring work done because it looks
  right.
allowed-tools: "Read, Grep, Glob, Bash(dotnet:*), Bash(git:*), Bash(./scripts/:*), Bash(scripts/:*), Bash(gh:*), Bash(bd:*)"
version: "1.0.0"
license: "MIT"
---

# Prove it — verify before declaring done

Never declare work complete on belief. Run the steps and report what they said.

This repository's failures are almost all *quiet* ones: a guard that fails open, a stylesheet
copy that drifted, a `Closes` that closed nothing. None of them announce themselves, so the only
thing standing between them and a merge is somebody actually running the checks.

## When to activate

- About to commit
- About to push
- About to open a pull request
- Right after thinking "this should be fine"

## 1. Build and test

```bash
dotnet build && dotnet test
```

`TreatWarningsAsErrors` is on, so a warning is already a failure here. No warnings allowed unless
they pre-existed — say so explicitly if they did.

## 2. Format

```bash
dotnet format --verify-no-changes
```

**On a Windows checkout this reports pre-existing whitespace noise from CRLF.** Compare the count
against untouched `main` before believing it is yours. `.gitattributes` normalises to LF, so CI
sees clean files. Do not "fix" a hundred files you did not touch.

## 3. The guard scripts

Each of these exists because the thing it checks broke silently at least once. Run the ones your
change reaches; run all of them if unsure — they are cheap next to a bad publish.

```bash
dotnet pack -c Release -o artifacts && ./scripts/assert-package-contents.sh artifacts
```

Always. Packaging is the deliverable, and a publish to `Fhi.Helsedata.no` cannot be walked back —
the feed lets you delete a version, but whoever already restored it keeps what they got.

```bash
./scripts/assert-sample-css-in-step.sh          # touched samples/ or invented a class name
./scripts/assert-portability-guard-armed.sh     # touched Directory.Build.props
./scripts/check-accessibility.sh                # touched markup
./scripts/check-hostile-host.sh                 # touched markup or layout
```

`check-hostile-host.sh` is the same component in helsedata's real stylesheet under a header that
overlaps document flow, measured with `getBoundingClientRect` and then scanned with axe. It needs
credentials for helsedata's Azure Artifacts feed — the Azure Artifacts Credential Provider locally
— because `samples/HostileHost` references `Fhi.Helsedata.Stiler`. Run it for anything that changes
what an element IS or where it sits; axe was green through all four of the layout defects that made
it exist.

`check-accessibility.sh` needs `dotnet`, `node` and a Chrome on PATH. **A green run means "no
detected regression" and nothing else.** Automated checking finds on the order of a third of WCAG
issues and is blind to structure that is simply absent — `VariableExplorer` scored 95 while being
unnavigable by column. Never write a pass down as more than the gate can see; `AGENTS.md` under
"Accessibility is a requirement, not a preference" is the full statement and the reason the
script carries no such claim of its own.

`assert-drift-ran.sh` is for the scheduled live-contract job, not for a pull request.

## 4. Changelog fragment

A `src/` change needs one, and CI fails without it. Same script CI runs:

```bash
scripts/check-changelog-fragment.sh
```

**Fragments here are English only** — one file, no language suffix. This is a deliberate
difference from Munin's bilingual `.en.md` / `.nb.md` pair and is explained in
`changelog.d/README.md`; do not "fix" it back. Line 1 is `category: <Category>`, one category per
file, and the bullet is written as the released line because that is what it becomes. The audience
is a host developer: *what changed for me, and what do I have to do about it?* Test refactors, CI
tweaks and sample-host changes are invisible to a consuming host and need no fragment — CI does
not ask for one.

## 5. Comment budget

The ceiling is in `AGENTS.md` under "Comments" and is three lines. This is the mechanical screen
for the lines *you* wrote — a run of four or more non-doc comment lines is where you stop and
justify it, not automatically a defect:

```bash
git diff --name-only main...HEAD | while read -r f; do
  case "$f" in
    *.cs|*.razor) [ -f "$f" ] || continue
      awk -v f="$f" '
        /^[[:space:]]*\/\// && !/^[[:space:]]*\/\/\// { if (n++ == 0) s = FNR; next }
        { if (n > 3) print f ":" s "  " n " comment lines"; n = 0 }
        END { if (n > 3) print f ":" s "  " n " comment lines" }' "$f" ;;
  esac
done
```

For each hit, the question is whether the block carries knowledge a reader cannot recover from
the code — a race, a non-obvious invariant, a workaround for behaviour outside this repository.
If it is incident history, it belongs in the bead and the block becomes a bead id. If it is
something everyone needs rather than everyone touching this file, it belongs in `AGENTS.md`.

Two things this screen deliberately does not do. It skips `///`, because the public XML docs of
`src/Fhi.Munin.Explorer` are the standing exception — their reader has only the package. And it is
scoped to the diff on purpose: **a repo-wide run returns dozens of pre-existing blocks.** The
ceiling arrived after most of this code did, and cleaning up files you did not otherwise touch is
a separate piece of work with its own bead, not something to smuggle into an unrelated pull
request.

## 6. Scope

```bash
git diff --stat
```

Every file in the list should relate to the stated task. Revert what does not.

## 7. The pull request reference — the one that has actually cost us

**Work items for this repository live in `FHIDev/Munin`, not here.** The reference that closes
one is therefore the cross-repository form:

```
Closes FHIDev/Munin#1234      # yes
Closes #1234                  # closes nothing — or the wrong thing
```

Thirty-two merged pull requests here carried the bare form and closed nothing, because the
session was reading Munin's instructions — where the bare form is correct, since there the issue
is in the same repository — while pushing to Explorer. Check the reference before opening the PR,
and check it again on any PR body an agent generated.

Use `Refs FHIDev/Munin#N` when the change only partly satisfies the bead: `Closes` shuts it
whether or not the acceptance criteria are met.

Then:

```bash
bd preflight --check
```

## Output format

Report what ran, not what you believe:

```
✓ Build + test: passes, 0 warnings, 214 tests
✓ Format:       clean (12 CRLF diffs, same count as main — not mine)
✓ Package:      assert-package-contents.sh green
✓ Sample CSS:   assert-sample-css-in-step.sh green
– Portability:  not run, Directory.Build.props untouched
✓ Accessibility: no detected regression on / and /kilder
✓ Changelog:    changelog.d/Fhi.Metadata-abc12.md, category: Fixed
✓ Comments:     no block over three lines in the diff
✓ Scope:        5 files, all related
✓ PR reference: Closes FHIDev/Munin#1234
```

Or, when something fails, say which and what you are doing about it:

```
✓ Build + test: passes
✗ Sample CSS:   host.css copies differ — kodeverk block only in LegacyHost
                → copying ModernHost's over it and re-running
✗ Changelog:    src/ changed, no fragment in changelog.d/
```

Fix every failure before proceeding.

## Anti-patterns

- **"The tests passed last time."** Run them.
- **"CI will catch it."** CI catching it costs a round trip and a red check on the board.
- **"It's only a docs change."** Then most of this is one `git diff --stat` and you are done in
  seconds — cheap enough that skipping it saves nothing.
- **Quoting a green accessibility run as "accessible".** It is not what the gate measured.
- **Pushing before it is verified.** Work is not done until it is pushed, but pushed is not the
  same as done.
