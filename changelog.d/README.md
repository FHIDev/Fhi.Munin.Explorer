# Changelog fragments

One file per change, assembled into `CHANGELOG.md` at release time.

`CHANGELOG.md` is a single file that every branch wants to edit on the same few lines, which
makes it the most reliable merge conflict in a repository. With several PRs in flight — and this
repository expects them — the conflict is guaranteed and the resolution is always the same
tedious re-stacking of bullets. A fragment is a new file, and two new files never conflict.

The mechanism is borrowed from Munin (`FHIDev/Fhi.Metadata`), with two deliberate differences.
Both are noted below so nobody "fixes" them back to Munin's shape.

## Write a fragment

Add `changelog.d/<slug>.md` on your branch:

```
category: Added
- `VariableExplorer` gained a `Language` parameter (`"no"` / `"en"`). Hosts that render the
  component in English must set it; it defaults to `"no"`.
```

- **Line 1** is `category: <Category>`, one of `Added`, `Changed`, `Fixed`, `Security`,
  `Deprecated`, `Removed`, `Notes for hosts`.
- **The rest** are markdown bullets, copied into the changelog verbatim. Write them as the
  released line, because that is exactly what they become.
- **One category per file.** A change that both adds a parameter and needs a host note is two
  fragments — split them, they end up in different sections anyway.
- **`<slug>`** is anything unique that hints at the change: the bead id (`Fhi.Metadata-abc12.md`)
  is the recommended choice since it is unique by construction and ties the entry back to the
  work item. `search-debounce.md` is fine too. The name is never published — it only has to not
  collide with another branch's file.

## Difference 1 — English only

Munin's changelog is bilingual (`.en.md` / `.nb.md`) because it is read by Norwegian internal and
business users. This one is read by developers embedding a NuGet package, and the README, the
package metadata and the public API surface are already English. So: `<slug>.md`, one file, no
language suffix. Adding a Norwegian half would mean translating for an audience that never asked
for it.

## Difference 2 — the audience is the consuming host, not us

Every entry answers a question a host developer actually has: **what changed for me, and what do
I have to do about it?** Read the top of `CHANGELOG.md` — this framing is already stated there
and the fragments inherit it.

Good:

> - `AddMuninExplorer(...)` now throws when `ApiBaseUrl` is missing instead of failing on first
>   request. Hosts that relied on lazy configuration must set it at startup.

Not a changelog entry — belongs in the commit message:

> - Refactored `VariableExplorerTest` to use a shared fixture.
> - Bumped the analyzer package.
> - Renamed an internal field.

Internal repository housekeeping — test refactors, CI tweaks, sample-host changes — is invisible
to someone embedding the package. If a change does not alter what a host sees, compiles against,
configures or must react to, it does not need a fragment, and CI does not ask for one (see below).

The `Notes for hosts` category exists for the things that are not a code change at all but still
govern how the package can be mounted — render modes, styling, the `_Imports.razor` requirement.
Those notes are as load-bearing as the API surface.

## CI enforcement

`.github/workflows/ci.yml` fails a pull request that changes anything under `src/` without adding
a fragment. It deliberately does **not** fire for changes to `docs/`, `samples/`, `test/`,
`.github/` or the scripts — those do not reach a consuming host.

Two escape hatches, both intentional:

- Dependabot PRs are exempt (they touch `src/**/*.csproj` and have their own changelog).
- `[no-changelog]` anywhere in the PR title skips the check, for the mechanical `src/` change
  that genuinely has nothing to tell a host. Use it rarely; it is visible in the PR title, which
  is the point.

Run the same check locally before pushing:

```bash
scripts/check-changelog-fragment.sh
```

## Assembly at release time — automatic, on the tag

Pushing a `v*` tag assembles the fragments. `.github/workflows/release.yml` runs
`scripts/release-changelog.sh` **before it packs**, because the package's `PackageReleaseNotes`
is that version's section: packing first would stamp the version with notes nobody had written.
The section groups the bullets by category, becomes one `## <version> — <date>` in `CHANGELOG.md`,
and the fragments it consumed are removed in the same commit. This `README.md` is never consumed,
so it doubles as the directory's `.gitkeep` — `changelog.d/` survives every release.

Nobody has to remember any of that, which is the point of it being on the tag. It was a documented
manual step for three weeks, and in that time eight versions shipped with the step never once run:
166 fragments piled up and `CHANGELOG.md` had no version sections at all (`Fhi.Metadata-l9l2n.44`).

**The commit reaches `main` through a pull request the workflow opens.** The `MainRules` ruleset
requires one for `main` and has no bypass actors, so an unattended push is refused whoever makes
it — and a credential that could bypass it is one this public repository deliberately does not
hold. Merging that pull request is the one step left to a person; the package and the GitHub
release already carry the notes by then. It arrives with its checks unreported, because GitHub
runs no workflow for an event its own `GITHUB_TOKEN` caused — close and reopen it, or push an
empty commit, and they run.

Two things to know before running anything by hand:

- **Running it twice for one version is a no-op**, not an error, and not a duplicate section. A
  release re-run has to reach the pack step, and the fragments queued for the *next* release have
  to survive it. `scripts/assert-changelog-assembles.sh` asserts both on every pull request, along
  with the fragments on your branch actually assembling — a malformed one would otherwise stop a
  release after the tag exists.
- **The fragments a release consumes are the ones the tagged commit has.** One merged after the
  tag was cut waits for the next release rather than being published under a version that never
  contained it.

To preview, or to rehearse a release without spending a version number:

```powershell
./scripts/assemble-changelog.ps1 -Version 0.2.0 -DryRun
```

```bash
scripts/release-changelog.sh 0.2.0 --dry-run     # everything the tag does, minus commit and push
```

## The 166 that piled up

They were backfilled across the eight `0.1.0-alpha.*` tags on 2026-09-04, rather than folded into
one section or left for the next release. Which release a fragment shipped in is not a guess: it
is the first tag whose history contains the commit that added the fragment, so all 166 were
attributed exactly. The alternative buries a breaking change — `0.1.0-alpha.8` deleted a component
helsedata mounts — under a version number that never shipped it, and a host bumping alpha.7 to
alpha.8 is exactly who the file is for.
