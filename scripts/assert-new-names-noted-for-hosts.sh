#!/usr/bin/env bash
#
# A class name new on this branch must be named in a "Notes for hosts" changelog fragment.
#
# The package ships no CSS. The rules that reach helsedata.no live in `Fhi.Helsedata.Stiler`, a
# separate Azure DevOps repository this CI cannot read, so no check here can confirm a rule exists.
# `assert-sample-css-in-step.sh` compares against the two SAMPLE stylesheets instead — which is the
# most it can reach, and is why it goes green while the real site has no rule at all. That happened
# three times in two days (oq40w, the click-target fix, p9c76).
#
# What this script can do is make the handoff loud: a name that did not exist on the base branch has
# to be written down where whoever updates Stiler will read it.
#
# Usage:
#   scripts/assert-new-names-noted-for-hosts.sh
#   BASE_REF=origin/main scripts/assert-new-names-noted-for-hosts.sh
#
# Exit 0 clean, 1 a name went unnoted, 2 the check could not run — the last is a tooling failure and
# must not read as a violation.

set -uo pipefail

cd "$(dirname "$0")/.." || { echo "::error::cannot anchor to checkout root" >&2; exit 2; }

BASE_REF="${BASE_REF:-origin/main}"

if ! git rev-parse --verify --quiet "$BASE_REF^{commit}" >/dev/null; then
  echo "::error::Base ref '$BASE_REF' is not in this checkout, so 'new' cannot be computed." >&2
  echo "In CI this means the base branch was not fetched (needs fetch-depth: 0 or an explicit" >&2
  echo "git fetch origin main). Refusing to guess — every name would look new." >&2
  exit 2
fi

# The same extraction as assert-sample-css-in-step.sh: every munin-explorer* token under src/, minus
# the two stem forms finished elsewhere ('-' is a runtime id prefix, '__' an interpolated modifier).
extract() {
  grep -rhoE --include='*.cs' --include='*.razor' --exclude-dir=bin --exclude-dir=obj \
         'munin-explorer[A-Za-z0-9_-]*' "$1" 2>/dev/null \
    | grep -vE -- '(-|__)$' \
    | sort -u
}

head_names="$(extract src/)"

# The base side is read out of the ref, not the working tree.
base_tree="$(mktemp -d)"
trap 'rm -rf "$base_tree"' EXIT
if ! git archive "$BASE_REF" src/ 2>/dev/null | tar -x -C "$base_tree" 2>/dev/null; then
  echo "::error::Could not read src/ out of '$BASE_REF'." >&2
  exit 2
fi
base_names="$(extract "$base_tree/src/")"

# A floor on BOTH sides. An empty base makes every name look new; an empty head hides everything.
# 10 is far below the ~75 the extraction yields today and far above what a stale regex returns.
MIN_NAMES=10
for side in head base; do
  eval "count=\$(printf '%s\n' \"\$${side}_names\" | grep -cE '^munin-explorer' || true)"
  if [ "${count:-0}" -lt "$MIN_NAMES" ]; then
    echo "::error::Extracted only ${count:-0} name(s) on the $side side, below the floor of $MIN_NAMES." >&2
    echo "The regex has gone stale or the ref is wrong. Either way this check cannot see what it is" >&2
    echo "meant to see, and a pass would be meaningless." >&2
    exit 2
  fi
done

new_names="$(comm -13 <(printf '%s\n' "$base_names") <(printf '%s\n' "$head_names"))"

if [ -z "$new_names" ]; then
  echo "No new munin-explorer class names on this branch."
  exit 0
fi

# Only fragments that declare the host category count. Line 1 carries it, per check-changelog-fragment.sh.
notes=""
for f in changelog.d/*.md; do
  [ -e "$f" ] || continue
  if [ "$(head -n 1 "$f" | tr -d '\r')" = "category: Notes for hosts" ]; then
    notes="$notes$(cat "$f")"$'\n'
  fi
done

missing=()
while read -r name; do
  [ -n "$name" ] || continue
  case "$notes" in
    *"$name"*) ;;
    *) missing+=("$name") ;;
  esac
done <<< "$new_names"

if [ "${#missing[@]}" -eq 0 ]; then
  echo "All new class names are named in a 'Notes for hosts' fragment:"
  printf '%s\n' "$new_names" | sed 's/^/  /'
  exit 0
fi

echo "::error::These class names are new on this branch and no 'Notes for hosts' fragment names them:" >&2
printf '%s\n' "${missing[@]}" | sed 's/^/  /' >&2
cat >&2 <<'EOF'

Fhi.Helsedata.Stiler is where the rule has to land, and this repository's CI cannot see it. A
fragment under changelog.d/ starting with

  category: Notes for hosts

and mentioning the name is what carries the handoff. Say which state needs the rule and why — an
unstyled element that still works is cosmetic, one that misrepresents its state is not.

If the name genuinely needs no rule (an id stem, a hook with no visual state), say that in the
fragment. The point is that somebody decided, not that a rule always exists.
EOF
exit 1
