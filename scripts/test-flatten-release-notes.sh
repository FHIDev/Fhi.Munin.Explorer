#!/usr/bin/env bash
# Guard for flatten-release-notes.sh. It feeds the package's release notes, and a silently
# dropped entry is invisible until a host reads the feed and finds a version missing from it.
#
# The case that motivated this: the flattener matched only "- " bullets while
# scripts/assemble-changelog.ps1 accepts "^[-*]\s". A fragment written with "*" reached
# CHANGELOG.md and the GitHub release and vanished from the package notes.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
flatten="$here/flatten-release-notes.sh"
fail=0

check () { # name, expected-substring, actual
  if printf '%s' "$3" | grep -qF -- "$2"; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n     wanted to find: %s\n     in:\n%s\n' "$1" "$2" "$3"
    fail=1
  fi
}

refute () { # name, forbidden-substring, actual
  if printf '%s' "$3" | grep -qF -- "$2"; then
    printf '  FAIL  %s\n     should not contain: %s\n' "$1" "$2"
    fail=1
  else
    printf '  ok    %s\n' "$1"
  fi
}

tmp="$(mktemp)"
trap 'rm -f "$tmp"' EXIT

cat > "$tmp" <<'EOF'
### Added

- **A dash bullet with a bolded lead.** Detail that must not reach the flattened line.
* **A star bullet with a bolded lead.** assemble-changelog.ps1 accepts these too.
  - **An indented bullet.** assemble-changelog.ps1 TrimStart()s before matching, so this is
    a legal entry and must not fold into the one above it.

### Changed

- A bullet with no bolded lead at all. Its second sentence must not appear.
- **A lead wrapped across
  two source lines.** Detail after it.
- A bullet with **emphasis in the middle** rather than a leading title.
EOF

out="$("$flatten" "$tmp")"

check  "dash bullet keeps its title"        "A dash bullet with a bolded lead"   "$out"
check  "STAR bullet is not dropped"         "A star bullet with a bolded lead"   "$out"
check  "INDENTED bullet is its own entry"   "  * An indented bullet"             "$out"
check  "unbolded bullet falls back"         "A bullet with no bolded lead at all" "$out"
check  "wrapped lead is joined"             "A lead wrapped across two source lines" "$out"
check  "categories survive"                 "Added"                              "$out"
check  "categories survive"                 "Changed"                            "$out"
refute "detail after the lead is dropped"   "must not reach the flattened line"  "$out"
refute "second sentence is dropped"         "Its second sentence must not appear" "$out"
refute "mid-text emphasis is not lifted"    "  * emphasis in the middle"         "$out"

# Every bullet in must produce exactly one line out. Catches silent drops generically,
# not only the two markers this fixture happens to use.
bullets_in="$(grep -cE '^[[:space:]]*[-*][ \t]' "$tmp")"
lines_out="$(printf '%s\n' "$out" | grep -c '^  \* ' || true)"
if [ "$bullets_in" -eq "$lines_out" ]; then
  printf '  ok    every bullet produced a line (%s)\n' "$bullets_in"
else
  printf '  FAIL  %s bullets in, %s lines out — entries were dropped\n' "$bullets_in" "$lines_out"
  fail=1
fi

empty="$("$flatten" /dev/null)"
check  "empty input has a stand-in"         "No changelog entry was assembled"   "$empty"

# The feed reads these from line one, so a blank first line is a visible fault.
first="$(printf '%s\n' "$out" | head -1)"
if [ -n "$first" ]; then
  printf '  ok    first line is not blank\n'
else
  printf '  FAIL  first line is blank — the feed shows an empty line before the first category\n'
  fail=1
fi

[ "$fail" -eq 0 ] || { echo "flatten-release-notes: FAILED"; exit 1; }
echo "flatten-release-notes: all checks passed"
