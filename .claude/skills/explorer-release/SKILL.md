---
name: explorer-release
description: >
  Cut a Fhi.Munin.Explorer release (v0.1.0-alpha.N and friends). Use when asked to
  cut, publish or tag an Explorer release, or to open the changelog PR that follows
  one. Covers the preconditions, the two local traps that waste a step every time,
  and the human step Actions cannot do. NOT for Fhi.Helsedata.Stiler — that one
  releases on merge to main and has no tag at all.
---

# Cutting an Explorer release

**A published version is permanent and a spent version cannot be reused.** Everything
below is ordered so that the irreversible act — pushing the tag — happens last, after
the things that can still be corrected for free.

## What actually triggers a release

`.github/workflows/release.yml`, `on: push: tags: v*`. Pushing the tag IS the release.
There is no button and no manual dispatch. The run then does, in order: assemble the
changelog → restore → build → **test again** → pack (the section becomes
`PackageReleaseNotes`) → `assert-package-contents.sh` → push to the internal feed →
create the GitHub release (prerelease auto-detected from the version string).

## Preconditions — check all four before tagging

1. **The tag commit must be on `origin/main`.** The workflow refuses otherwise:
   *"Tag points at a commit that is not on origin/main. Merge it first, then tag the
   merge commit."* Verify with `git merge-base --is-ancestor <sha> origin/main`.
2. **Every earlier `changelog/v*` version must have its section on main.** The guard
   greps `CHANGELOG.md` on main for `^##[[:space:]]+<version>([[:space:]]|$)`.
   **It tests the SECTION, not branch ancestry** — deliberately, because a squash merge
   makes a new object and an ancestry test would call a merged branch unmerged. So
   **leftover `changelog/v*` branches on origin are NOT blockers** if their sections
   landed. Do not delete them in a panic; check `git show origin/main:CHANGELOG.md`.
3. **The previous release's changelog PR must have merged.** This is the same check as
   (2) from the other side, and it is the usual reason a release refuses to run.
4. **Ask the peer sessions whether anything should be in it.** Someone usually has a PR
   an hour from landing, and a release that misses it costs another whole cycle.

## Rehearse first — `--dry-run` spends nothing

```bash
scripts/release-changelog.sh 0.1.0-alpha.N --dry-run
```

Note the version takes **no leading `v`** here, though the tag does.

### Two local traps, both of which will cost you a step

**`pwsh` "is not on PATH" while pwsh is plainly installed.** The script shells out to
`assemble-changelog.ps1`, and **Git Bash does not inherit a Windows PATH entry that
contains a space**, so `C:\Program Files\PowerShell\7` is invisible to it. Export the
POSIX form inside bash:

```bash
export PATH="/c/Program Files/PowerShell/7:$PATH"
```

**The dry run really does assemble into the working tree.** It prints
`[OK] Wrote '## 0.1.0-alpha.N' to CHANGELOG.md` and
`[OK] Deleted 33 consumed fragment(s) from changelog.d/` — and then restores. It does
restore correctly, but **verify rather than trust it**: `git status` clean, and the
`changelog.d/*.md` count on disk equal to the count on `origin/main`. The sibling repo's
`Fhi.Metadata/scripts/assemble-changelog.ps1` deletes fragments for real and has eaten
all 70 of them once (bead `Fhi.Metadata-zqy4d`), so the instinct is right even though
this script is well-behaved.

### Read the dry run, do not skim it

The fragment list is the release notes. **Build them from `changelog.d/`, never from
memory or from a list of merged PRs** — on 2026-09-23 both a peer session and I named
merges from memory and the dry run found **fifteen** neither of us had listed. Count the
`-hosts` fragments separately and say how many there are: those are what a host has to
read before its pin moves.

## Cut it

```bash
git fetch origin --tags
git tag -a v0.1.0-alpha.N <sha-on-origin-main> -m "Explorer 0.1.0-alpha.N"
git push origin v0.1.0-alpha.N
```

## Then the human step — and it blocks the next release

The workflow pushes `changelog/v0.1.0-alpha.N` but **cannot open the PR**: org policy
forbids Actions opening PRs in FHIDev. Open it yourself, immediately, and say in the
body that the next release is blocked until it merges. Forgetting this does not fail
today; it fails the *next* release, confusingly, with a message about an old version.

## Verify, then report

- `git ls-remote --heads origin 'changelog/v0.1.0-alpha.N'` — the branch exists
- `gh release view v0.1.0-alpha.N --repo FHIDev/Fhi.Munin.Explorer` — published,
  `isPrerelease` true for an alpha
- `gh run view <id> --json jobs` — the feed publish job succeeded

Tell the peer sessions the tag name: deploy watchers fire on it.

## The release is not finished until helsedata pins it

**A published package nobody pins changes nothing.** The pin bump is a separate PR in a
separate repo with its own ADO work item — but it is part of the job, not an optional
extra, and the release is only half done without it. Robin, 2026-09-23.

**The repo is `Fhi.Helsedata`** — the Optimizely host, NOT `Fhi.Helsedata.Stiler` and
NOT the RCL. Both pins live in one file:

```
Fhi.Helsedata/Fhi.Helsedata.Optimizely/Fhi.Helsedata.Optimizely.csproj
  :70  Fhi.Helsedata.Stiler
  :84  Fhi.Munin.Explorer
```

Line numbers drift — **locate by package id**. Both sit in the `!= 'true'` ItemGroups;
the `== 'true'` `ProjectReference` branches are local-dev and must not be touched.

**BUMP BOTH IN ONE COMMIT, AND STILER FIRST IF YOU EVER SPLIT THEM.** The ordering is
load-bearing and has bitten: Explorer `vdnm4` deleted a name tooltip on the assumption
that a newer Stiler wraps the names. Explorer-first reintroduces ADO 121065 *worse* than
the original — clipped names with no tooltip at all. One commit avoids the question.

**Which Stiler version?** Stiler publishes on *merge*, so every merged Stiler PR that day
is a new `0.1.x`. Check the feed for the newest rather than assuming — and say in the PR
which Stiler PRs are included, because helsedata's reviewers cannot see the ADO repo's
history from the GitHub side.

**The ADO work item**: a **Task** under feature **120543**, area
`Fhi.Helsedata\Helsedata\Metadata`. Norwegian, HTML body, signed *Skrevet av AI* — see
the `ado-work-item` skill, which also says to draft it and **ask before creating**.
Precedent to copy: **ADO 121492** with **PR 39516** (alpha.13 + Stiler 0.1.98, both pins,
one commit). Linking the PR to the item is a separate `ArtifactLink`, not a field.

**`az` mangles æøå in both directions** on this console. Create the PR with an ASCII
placeholder, then PATCH title and description over REST with explicit UTF-8, read back
over REST, and verify by codepoint — never through `az ... -o json`.

## Stiler is not like this at all

`Fhi.Helsedata.Stiler` publishes **on merge to main** — merging IS the release, the csproj
stays `0.0.0-lokal`, CI assigns the version, and there are no tags. See
`reference_stiler_releases_on_merge`.
