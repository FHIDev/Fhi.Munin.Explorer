#!/usr/bin/env bash
#
# Every `munin-explorer*` name in src/ is listed in the README's inventory table, and nothing else is.
#
# This is the check `assert-new-names-noted-for-hosts.sh` cannot be. That one diffs the branch
# against its base, so it only ever asks about names that did not exist on the base branch. A name
# already missing on the day the guard was written stays missing forever, however many branches
# pass through — the window moves with the branch and never looks behind it. That is not a guard
# that is switched off; it is a guard that by construction sees a window.
#
# What went unseen: README said "the nine munin-explorer-kilde* names in KildeView" while KildeView
# emitted twelve, said three munin-explorer-kilder* names while KildeExplorer emitted four, and the
# eight munin-explorer-whole* names VariableView emits appeared in no markdown file in the
# repository at all. Both stale counts were written before the branch diff existed and the whole*
# names arrived with the view itself, so every one of them was outside that window.
#
# So this check has no window. It reconciles the FULL set, both directions:
#
#   - a name src/ emits and the table does not list is a host told about part of the component;
#   - a name the table lists and src/ no longer emits is a host sent looking for an element that
#     is not there, which is the same defect read backwards.
#
# The table is the README's own inventory, between the two marker comments named below. It carries
# a kind per name, because "which names exist" and "what does an undefined one cost a host" are
# different questions and the README is where the second one is answered. The kinds are checked
# for spelling only — no script can tell a handle from a name that carries meaning.
#
# Usage:
#   scripts/assert-class-names-listed.sh
#   README_FILE=… scripts/assert-class-names-listed.sh                             # tests only
#
# Exit 0 clean, 1 the two sets differ, 2 the check could not run — the last is a tooling failure
# and must not read as a violation.

set -uo pipefail

# Anchored on the script's own location, not on the caller's repository, so running it from
# scripts/ means what it says. Same reasoning as assert-sample-css-in-step.sh.
cd "$(dirname "${BASH_SOURCE[0]}")/.." || { echo "::error::cannot anchor to checkout root" >&2; exit 2; }

# Overridable for one reason: so a test can run this whole script, every clause unchanged, against
# a README it has deliberately broken. CI and the pre-PR checklist run it bare.
README_FILE="${README_FILE:-README.md}"

START='<!-- class-names:start -->'
END='<!-- class-names:end -->'

if [ ! -f "$README_FILE" ]; then
  echo "::error::'$README_FILE' is missing, so there is no inventory to reconcile against." >&2
  exit 2
fi

if [ ! -d src ]; then
  echo "::error::No src/ directory to read class names out of, so this check would pass having" >&2
  echo "compared the inventory against nothing. Has the solution layout moved?" >&2
  exit 2
fi

# The same extraction as assert-sample-css-in-step.sh and assert-new-names-noted-for-hosts.sh, on
# purpose: three checks disagreeing about what a name is would each be right about a different set.
# Deliberately over-inclusive — a name written down in prose is a name a reader will go looking for,
# and the inventory has a `prose` kind for exactly that. `-` and `__` endings are stems finished
# elsewhere and are not names.
emitted="$(
  grep -rhoE --include='*.cs' --include='*.razor' --exclude-dir=bin --exclude-dir=obj \
         'munin-explorer[A-Za-z0-9_-]*' src/ 2>/dev/null \
    | grep -vE -- '(-|__)$' \
    | sort -u
)"

# Rows are `| `name` | kind |`. Anything else between the markers is prose and is skipped, which is
# what lets the table keep its own header row. Leading whitespace is allowed because the table is
# indented into a list item — unindenting it would end the bullet it belongs to.
listed_rows="$(
  awk -v s="$START" -v e="$END" '
    { sub(/\r$/, "") }
    index($0, s) { inside = 1; next }
    index($0, e) { inside = 0; next }
    inside && /^ *\| *`munin-explorer[A-Za-z0-9_-]*` *\| *[a-z]+ *\| *$/ { print }
  ' "$README_FILE"
)"

if ! grep -qF "$START" "$README_FILE" || ! grep -qF "$END" "$README_FILE"; then
  echo "::error::'$README_FILE' has no inventory block. It is delimited by" >&2
  echo "  $START" >&2
  echo "  $END" >&2
  echo "and this check cannot run without it." >&2
  exit 2
fi

listed="$(printf '%s\n' "$listed_rows" | sed -E 's/^ *\| *`([^`]*)`.*/\1/' | grep -E '^munin-explorer' | sort -u)"

# A floor on both sides, for the reason the other two scripts have one: an extraction that finds
# nothing would report every name as missing — loud, but about the wrong thing. 10 is far below the
# 108 both sides yield today and far above what a stale regex returns.
MIN_NAMES=10
assert_floor() {
  local side="$1" list="$2" count
  count="$(printf '%s\n' "$list" | grep -cE '^munin-explorer' || true)"
  if [ "${count:-0}" -lt "$MIN_NAMES" ]; then
    echo "::error::Read only ${count:-0} name(s) on the $side side, below the floor of $MIN_NAMES." >&2
    echo "The extraction has gone stale, so a verdict either way would be meaningless. Fix the" >&2
    echo "extraction rather than the floor." >&2
    exit 2
  fi
}
assert_floor src "$emitted"
assert_floor README "$listed"

# Kinds are checked for spelling, not for truth. A row saying `handel` would otherwise sit there
# looking like an answer.
KINDS='handle meaning id prose'
bad_kinds=()
while read -r row; do
  [ -n "$row" ] || continue
  kind="$(printf '%s\n' "$row" | sed -E 's/^ *\| *`[^`]*` *\| *([a-z]+) *\|.*/\1/')"
  case " $KINDS " in
    *" $kind "*) ;;
    *) bad_kinds+=("$row") ;;
  esac
done <<< "$listed_rows"

unlisted="$(comm -23 <(printf '%s\n' "$emitted") <(printf '%s\n' "$listed"))"
stale="$(comm -13 <(printf '%s\n' "$emitted") <(printf '%s\n' "$listed"))"

if [ -z "$unlisted" ] && [ -z "$stale" ] && [ "${#bad_kinds[@]}" -eq 0 ]; then
  echo "The README inventory lists every munin-explorer name src/ writes, and nothing it does not"
  echo "($(printf '%s\n' "$emitted" | grep -cE '^munin-explorer') names)."
  exit 0
fi

if [ -n "$unlisted" ]; then
  echo "::error::src/ writes these class name(s) and the README inventory does not list them:" >&2
  printf '%s\n' "$unlisted" | sed 's/^/  /' >&2
  echo "" >&2
  echo "A host outside helsedata reads that table to find out what it has to style. A name missing" >&2
  echo "from it is a part of the component nobody outside this repository knows exists. Add a row" >&2
  echo "between the markers in $README_FILE, with the kind that says what an undefined one costs:" >&2
  echo "  handle   something else already dresses the element — look, not information" >&2
  echo "  meaning  carries meaning nothing else carries — the host has to draw it" >&2
  echo "  id       not a class; the stem is completed with a per-instance discriminator" >&2
  echo "  prose    written down in a comment, worn by no element" >&2
fi

if [ -n "$stale" ]; then
  echo "::error::The README inventory lists these class name(s) and src/ no longer writes them:" >&2
  printf '%s\n' "$stale" | sed 's/^/  /' >&2
  echo "" >&2
  echo "Either the name was removed or renamed and the row was left behind, or the row has a typo." >&2
  echo "A row for a name nothing emits sends a host looking for an element that is not there." >&2
fi

if [ "${#bad_kinds[@]}" -gt 0 ]; then
  echo "::error::These inventory row(s) name a kind that is not one of: $KINDS" >&2
  printf '%s\n' "${bad_kinds[@]}" | sed 's/^/  /' >&2
fi

exit 1
