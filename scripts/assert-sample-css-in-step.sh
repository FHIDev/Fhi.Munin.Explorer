#!/usr/bin/env bash
#
# Two things about the sample hosts' stylesheets that nothing else checks.
#
# ONE: they are the same file twice.
#
# samples/ModernHost/wwwroot/host.css and samples/LegacyHost/wwwroot/css/host.css are one
# stylesheet, copied. That is what makes the samples worth having in pairs: both hosts style the
# component with identical rules, so any difference a reader sees between them is a difference in
# the hosting model rather than in the CSS.
#
# Both files say so in their header comment, and for a while that was the only thing enforcing it.
# It did not hold: the ~76 lines styling the Data tab's kodeverk list and code table landed in
# LegacyHost's copy alone, so ModernHost rendered an unstyled list — which looks exactly like a
# bug in the component, and gives a reader checking the kodeverk work no way to tell the two
# apart. Nothing failed; the comment just stopped being true.
#
# TWO: between them they style every class name the package invents.
#
# The first check alone reads as "the samples are right" and does not say that. Delete a block
# from BOTH copies and they still agree, while both samples render that part of the component at
# raw browser defaults — the same visible failure, mirrored. That is not hypothetical either: the
# kilde view arrived in #43 with nine names of its own and no rules for any of them in either
# copy, and a check that only compared the two files called that green.
#
# So the second clause reads the names out of src/ and asks the stylesheet for a rule for each.
# A name in the `variable-explorer` prefix is one of two things: OURS, invented here, which
# helsedata's stylesheet has never heard of and only a host can style — or THEIRS, read back off
# helsedata's compiled variables.css and already styled on every page of their site. Only ours
# need a rule here; theirs are listed below and skipped.
#
# The extraction is deliberately over-inclusive: every `variable-explorer*` token in src/, prose
# and all, minus the ones ending in `-` (those are id prefixes completed at runtime, like
# `variable-explorer-toggle-`). A name the package writes down anywhere is a name a reader will
# go looking for in a stylesheet, and the cost of a false positive is one line — a rule, or an
# entry in THEIRS. A false negative costs an unstyled component nobody notices.
#
# Usage:
#   scripts/assert-sample-css-in-step.sh
#
# Runs from anywhere: the paths below are repo-relative and the script anchors itself to the
# checkout root, so `bash scripts/assert-sample-css-in-step.sh` from scripts/ means what it says
# rather than reporting both files missing.
#
# When the first clause goes red, the fix is to copy whichever copy is correct over the other one.
# There is no merging to do: the files are not allowed to differ at all, not even in a comment
# naming the other file, which is why neither of them does.

set -uo pipefail

# Anchored on the script's own location rather than on `git rev-parse --show-toplevel`, which
# answers for whatever repository the CALLER happens to be standing in — and, when it answers
# nothing at all, leaves `cd ""` to succeed and do nothing, which is the silent version of the
# bug this line is here to fix.
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

MODERN="samples/ModernHost/wwwroot/host.css"
LEGACY="samples/LegacyHost/wwwroot/css/host.css"

# Names in the `variable-explorer` prefix that belong to helsedata, not to us. Every one was read
# back off their compiled variables.css — the container and the results column their variable page
# is built from, plus the header row their own column picker hangs in and the dropdown it opens.
# They are styled on helsedata.no whether or not this sample styles them, so a missing rule here
# is not a missing rule anywhere that matters. The list is the same one VariableExplorerTest's
# `invented` assertions annotate name by name; keep the two in step.
THEIRS=(
  variable-explorer-container
  variable-explorer-results
  variable-explorer-header
  variable-explorer-header__actions
  variable-explorer-header__actions-button
  variable-explorer__dropdown
)

# Names in the prefix that are ours but are not classes: element ids the package writes down in
# prose without the `-{instance}` suffix that completes them at runtime. A stylesheet cannot have
# a rule for them, because `.variable-explorer-source` selects nothing — the attribute is `id`.
#
# The `-$` filter below catches the ones written with their trailing hyphen; these are the ones
# written bare, in a sentence explaining what they are. `variable-explorer-source` is the whole
# list and arrived that way: it was a class while the kilde opened inside the variable's panel,
# and became an id prefix when the panel became a drill-in, leaving prose that still names it.
#
# Kept apart from THEIRS on purpose. THEIRS means "helsedata styles this, so a missing rule here
# costs nothing"; this means "no rule is possible". Filing one under the other would say the name
# is on helsedata.no, which it is not.
IDS=(
  variable-explorer-source
)

fail=0
for f in "$MODERN" "$LEGACY"; do
  if [ ! -f "$f" ]; then
    echo "::error::'$f' is missing. The sample hosts each need their own copy of the stylesheet." >&2
    fail=1
  fi
done
[ "$fail" = "0" ] || exit 1

if ! cmp -s "$MODERN" "$LEGACY"; then
  echo "::error::The sample host stylesheets have drifted apart. Copy one over the other:" >&2
  echo "  cp $LEGACY $MODERN     # or the other way round" >&2
  echo "" >&2
  diff -u "$MODERN" "$LEGACY" >&2
  exit 1
fi

# Clause two, on either copy — they are identical by the time we get here.
missing=()
while read -r name; do
  case " ${THEIRS[*]} ${IDS[*]} " in
    *" $name "*) continue ;;
  esac

  # Anchored on both sides so `.variable-explorer-period__fill` does not answer for
  # `.variable-explorer-period`: a rule for the part is not a rule for the whole.
  grep -qE "\.${name}([^A-Za-z0-9_-]|\$)" "$MODERN" || missing+=("$name")
done < <(
  grep -rhoE 'variable-explorer[A-Za-z0-9_-]*' src/ \
    | grep -vE -- '-$' \
    | sort -u
)

if [ ${#missing[@]} -gt 0 ]; then
  echo "::error::The sample stylesheet has no rule for ${#missing[@]} class name(s) the package invents:" >&2
  printf '  %s\n' "${missing[@]}" >&2
  echo "" >&2
  echo "Each renders at raw browser defaults in both samples, which reads as a bug in the" >&2
  echo "component rather than as a host that has not been asked for the rule. Add a rule to" >&2
  echo "$LEGACY, copy it over $MODERN — or, if the name is helsedata's" >&2
  echo "rather than ours, add it to THEIRS at the top of this script and say where you read it" >&2
  echo "back from." >&2
  exit 1
fi

echo "Sample host stylesheets are in step ($(wc -l < "$MODERN" | tr -d ' ') lines), and style every name the package invents."
