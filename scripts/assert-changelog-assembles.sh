#!/usr/bin/env bash
#
# Fails if the changelog fragments waiting in changelog.d/ do not assemble, or if assembling
# them twice is not a no-op.
#
# .github/workflows/release.yml runs assemble-changelog.ps1 on every run of a v* tag, before the
# pack step, and commits the result. That makes two things a release-time surprise rather than a
# pull-request one, which is the wrong end to find them at: a fragment with a bad category header
# stops the release after the tag exists, and a second run of the same tag - a re-run after a
# network blip on the push, which the release notes tell people to do - must not write a second
# section for a version or eat the fragments queued for the next one.
#
# So both are asserted here, on every pull request, against this branch's real fragments.
#
# Everything happens in a copy under $TMPDIR: the assembler consumes the fragments it reads, and
# a guard that leaves the working tree assembled would be worse than no guard.
#
# Usage:
#   scripts/assert-changelog-assembles.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if ! command -v pwsh >/dev/null 2>&1; then
  echo "pwsh is not on PATH — install PowerShell 7 to run this check." >&2
  exit 2
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

mkdir -p "$WORK/scripts"
cp "$REPO_ROOT/scripts/assemble-changelog.ps1" "$WORK/scripts/"
cp "$REPO_ROOT/CHANGELOG.md" "$WORK/"
cp -R "$REPO_ROOT/changelog.d" "$WORK/"

failures=0
bad() { printf '  FAIL  %s\n' "$*" >&2; failures=$((failures + 1)); }
note() { printf '  %s\n' "$*"; }

queued=$(find "$WORK/changelog.d" -maxdepth 1 -name '*.md' ! -name 'README.md' | wc -l | tr -d ' ')

# A branch can legitimately have none - the changelog pull request the release workflow opens is
# the branch that consumed them all - and the assertions below need a section to have been
# written, so one fragment is invented rather than half the checks quietly not running.
if [ "$queued" = "0" ]; then
  printf 'category: Added\n- No fragment was queued on this branch, so this one stands in for one.\n' \
    > "$WORK/changelog.d/zzz-guard-fixture.md"
  queued=1
  note 'no fragments queued on this branch — assembling one written for the purpose'
fi

# A version no tag will ever carry, so a stray section cannot be mistaken for a real release if
# this ever runs somewhere it should not.
VERSION=9999.0.0
NEXT=9999.0.1

assemble() { pwsh "$WORK/scripts/assemble-changelog.ps1" "$@"; }

echo "Assembling $queued queued fragment(s) as $VERSION in $WORK"
if ! assemble -Version "$VERSION" -Date 2026-01-01 -NotesOutFile "$WORK/notes-first.md"; then
  echo >&2
  echo "The fragments in changelog.d/ do not assemble. The release workflow runs this same" >&2
  echo "script for a v* tag, so this would stop a release after the tag exists. Fix the" >&2
  echo "fragment named above — the format is in changelog.d/README.md." >&2
  exit 1
fi

# --- What a release gets out of it -----------------------------------------------------------
grep -Eq "^## +$VERSION( |—|$)" "$WORK/CHANGELOG.md" \
  || bad "no '## $VERSION' section was written into CHANGELOG.md"

left=$(find "$WORK/changelog.d" -maxdepth 1 -name '*.md' ! -name 'README.md' | wc -l | tr -d ' ')
[ "$left" = "0" ] || bad "$left fragment(s) survived assembly — a release would leave them for the next one"

[ -f "$WORK/changelog.d/README.md" ] || bad 'changelog.d/README.md was consumed — it is the directory'\''s .gitkeep'

[ -s "$WORK/notes-first.md" ] || bad '-NotesOutFile wrote nothing — the package would ship notes-free'

# --- Re-running the same version -------------------------------------------------------------
# The failure this guards: a re-run appends a second section for the version, or consumes the
# fragments that belong to the release after it.
cp "$WORK/CHANGELOG.md" "$WORK/CHANGELOG.after-first.md"
printf 'category: Fixed\n- Queued for the release after %s.\n' "$VERSION" > "$WORK/changelog.d/zzz-next-release.md"

if ! assemble -Version "$VERSION" -Date 2026-01-02 -NotesOutFile "$WORK/notes-second.md"; then
  bad "re-running for $VERSION exited non-zero — a re-run of a release cannot reach the pack step"
fi

cmp -s "$WORK/CHANGELOG.after-first.md" "$WORK/CHANGELOG.md" \
  || bad "re-running for $VERSION changed CHANGELOG.md — a duplicate or empty section"

[ -f "$WORK/changelog.d/zzz-next-release.md" ] \
  || bad "re-running for $VERSION consumed a fragment queued for the release after it"

cmp -s "$WORK/notes-first.md" "$WORK/notes-second.md" \
  || bad 're-running answered different release notes than the run that assembled the section'

# --- A tag whose fragments are all consumed --------------------------------------------------
# The next version, with changelog.d/ empty: no section, no heading with nothing under it, and an
# empty notes file so release.yml can tell there is no entry rather than shipping a blank one.
rm -f "$WORK/changelog.d/zzz-next-release.md"
if ! assemble -Version "$NEXT" -Date 2026-01-03 -NotesOutFile "$WORK/notes-empty.md"; then
  bad "assembling $NEXT with no fragments exited non-zero"
fi
cmp -s "$WORK/CHANGELOG.after-first.md" "$WORK/CHANGELOG.md" \
  || bad "assembling $NEXT with no fragments touched CHANGELOG.md — an empty section"
[ ! -s "$WORK/notes-empty.md" ] || bad "assembling $NEXT with no fragments wrote non-empty notes"

# --- A broken fragment still stops it --------------------------------------------------------
# Idempotency is a no-op on purpose; validation is not. Without this, making the two no-ops above
# quiet could quietly make a malformed fragment quiet too, and it would be published as a bullet.
printf 'not a category line\n- A bullet.\n' > "$WORK/changelog.d/zzz-broken.md"
if assemble -Version "$NEXT" -Date 2026-01-03 >/dev/null 2>&1; then
  bad 'a fragment with no category header assembled anyway'
fi
rm -f "$WORK/changelog.d/zzz-broken.md"

if [ "$failures" -gt 0 ]; then
  echo >&2
  echo "$failures assertion(s) failed — see scripts/assemble-changelog.ps1 and this script's header." >&2
  exit 1
fi

echo "OK — the fragments assemble, and assembling twice is a no-op."
