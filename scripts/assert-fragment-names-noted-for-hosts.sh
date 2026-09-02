#!/usr/bin/env bash
#
# A `munin-explorer*` name written into any changelog fragment is also named for hosts.
#
# `assert-new-names-noted-for-hosts.sh` is the same rule read through a window: it diffs the branch
# against its base, so it only ever asks about names that did not exist on the base. The facet
# panel's two names are what that costs. They arrived in #72 naming their own host requirement in an
# `Added` bullet — "a host on Fhi.Helsedata.Stiler needs rules for them" — and the branch check
# landed in #96, three weeks later. They were never new on a branch it could see, so the requirement
# sat in the wrong section of the changelog with nothing able to say so.
#
# This check has no window. Its set is every fragment queued in changelog.d, on every run: a name a
# fragment is willing to write down is a name a host will go looking for, and the section a host
# reads about styling is `Notes for hosts`. So a name named in an `Added`, `Changed` or `Fixed`
# fragment and in no `Notes for hosts` one is a host requirement filed where hosts do not read.
#
# Released versions count as noted: `assemble-changelog.ps1` moves the fragments into CHANGELOG.md
# under `### Notes for hosts`, and a later fragment naming an already-noted name is not a defect.
#
# The escape hatch is the one the branch check documents: if the name genuinely needs no rule, say
# so in a `Notes for hosts` fragment. The point is that somebody decided.
#
# Usage:
#   scripts/assert-fragment-names-noted-for-hosts.sh
#   CHANGELOG_DIR=… CHANGELOG_FILE=… scripts/assert-fragment-names-noted-for-hosts.sh   # tests only
#
# Exit 0 clean, 1 a name went unnoted, 2 the check could not run — the last is a tooling failure and
# must not read as a violation.

set -uo pipefail

# Anchored on the script's own location, not on the caller's directory, so running it from scripts/
# means what it says. Same reasoning as assert-class-names-listed.sh.
cd "$(dirname "${BASH_SOURCE[0]}")/.." || { echo "::error::cannot anchor to checkout root" >&2; exit 2; }

# Overridable for one reason: so a test can run this whole script, every clause unchanged, against
# fragments it has deliberately broken. CI and the pre-PR checklist run it bare.
CHANGELOG_DIR="${CHANGELOG_DIR:-changelog.d}"
CHANGELOG_FILE="${CHANGELOG_FILE:-CHANGELOG.md}"

CATEGORY='category: Notes for hosts'

if [ ! -d "$CHANGELOG_DIR" ]; then
  echo "::error::'$CHANGELOG_DIR' is missing, so there are no fragments to read." >&2
  exit 2
fi

if [ ! -f "$CHANGELOG_FILE" ]; then
  echo "::error::'$CHANGELOG_FILE' is missing, so a name noted in a released version would read" >&2
  echo "as unnoted. Refusing to guess." >&2
  exit 2
fi

# The same extraction as the other three checks, on purpose: four checks disagreeing about what a
# name is would each be right about a different set. `-` and `__` endings are stems finished
# elsewhere and are not names.
#
# The staleness check, and it cannot be a floor on the fragments: changelog.d is emptied at release,
# so a run with nothing queued is a legitimate pass. src/ is the fixed point — the same regex over
# the component yields 108 names today, and a regex that has gone stale yields almost none there.
MIN_NAMES=10
src_count="$(
  grep -rhoE --include='*.cs' --include='*.razor' --exclude-dir=bin --exclude-dir=obj \
         'munin-explorer[A-Za-z0-9_-]*' src/ 2>/dev/null \
    | grep -vE -- '(-|__)$' \
    | sort -u \
    | grep -cE '^munin-explorer' || true
)"
if [ "${src_count:-0}" -lt "$MIN_NAMES" ]; then
  echo "::error::The name extraction found only ${src_count:-0} name(s) under src/, below the" >&2
  echo "floor of $MIN_NAMES. It has gone stale, so a verdict either way would be meaningless." >&2
  echo "Fix the extraction rather than the floor." >&2
  exit 2
fi

# Both sources of "noted": the fragments still queued, and the released sections they became.
# Line 1 carries a fragment's category, per check-changelog-fragment.sh.
noted=""
for f in "$CHANGELOG_DIR"/*.md; do
  [ -e "$f" ] || continue
  if [ "$(head -n 1 "$f" | tr -d '\r')" = "$CATEGORY" ]; then
    noted="$noted$(cat "$f")"$'\n'
  fi
done

# Every `### Notes for hosts` section of the released changelog, up to the next heading.
noted="$noted$(awk '
  { sub(/\r$/, "") }
  /^###+ / { inside = ($0 ~ /^### Notes for hosts[[:space:]]*$/); next }
  /^## /   { inside = 0; next }
  inside
' "$CHANGELOG_FILE")"$'\n'

# Whole token, not substring: every name here shares the munin-explorer prefix, so a plain substring
# test lets a note about munin-explorer-filters__toggle-extra silently satisfy
# munin-explorer-filters__toggle. Names are [A-Za-z0-9_-] by the extraction, so no metacharacters.
is_noted() {
  printf '%s\n' "$noted" | grep -qE "(^|[^A-Za-z0-9_-])${1}([^A-Za-z0-9_-]|\$)"
}

violations=()
checked=0
# One recursive grep for the whole directory rather than one per file: `-o` without `-h` prefixes
# each hit with its path, which is what the report needs anyway, and 130-odd fragments is 130-odd
# processes saved.
mentions="$(
  grep -roE --include='*.md' 'munin-explorer[A-Za-z0-9_-]*' "$CHANGELOG_DIR" 2>/dev/null \
    | grep -vE -- '(-|__)$' \
    | sort -u
)"

while IFS= read -r mention; do
  [ -n "$mention" ] || continue
  f="${mention%:*}"
  name="${mention##*:}"

  [ "$(basename "$f")" = "README.md" ] && continue
  [ "$(head -n 1 "$f" | tr -d '\r')" = "$CATEGORY" ] && continue

  checked=$((checked + 1))
  is_noted "$name" || violations+=("$name  ($f)")
done <<< "$mentions"

if [ "${#violations[@]}" -eq 0 ]; then
  echo "Every munin-explorer name a queued fragment writes down is named for hosts ($checked" \
       "mention(s) checked)."
  exit 0
fi

echo "::error::These fragments name a class name that no 'Notes for hosts' fragment names:" >&2
printf '%s\n' "${violations[@]}" | sort -u | sed 's/^/  /' >&2
cat >&2 <<'EOF'

A host reads the Notes for hosts section to find out what it has to style. A name that only ever
appears under Added, Changed or Fixed is a host requirement filed where hosts do not look — which
is how the facet panel's fold came to be documented in an Added bullet and nowhere else.

Add changelog.d/<slug>-vertsstiler.md starting with

  category: Notes for hosts

naming the name and saying which declaration a host has to supply and what an undefined one costs.
One category per file, per changelog.d/README.md — this is a second fragment, not a second bullet
in the first.

If the name genuinely needs no rule, say that in the fragment. The point is that somebody decided,
not that a rule always exists.
EOF
exit 1
