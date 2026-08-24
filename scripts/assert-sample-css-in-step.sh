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
# Every name in the `munin-explorer` prefix is OURS: the package owns that prefix, and nothing
# outside this repository is obliged to style any of it. There used to be a second category —
# names in the old `variable-explorer` prefix that were helsedata's, read back off their compiled
# variables.css and already styled on every page of their site, which this script skipped. The
# rename to `munin-explorer` emptied it; THEIRS below is what is left of the idea.
#
# The extraction is deliberately over-inclusive: every `munin-explorer*` token in src/, prose and
# all, minus the ones ending in `-` or `__`. Both are stems completed elsewhere and neither is a
# name: `-` is an id prefix finished at runtime (`munin-explorer-toggle-`), and `__` is an
# interpolated modifier stem a line of C# builds the rest of. A name the package writes down
# anywhere is a name a reader will go looking for in a stylesheet, and the cost of a false
# positive is one line — a rule, or an entry in THEIRS. A false negative costs an unstyled
# component nobody notices.
#
# Which is why an extraction that finds nothing is an error rather than a pass: it is the false
# negative applied to every name at once, and the clause that would report success having checked
# none of them.
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

# Names in the prefix that belong to someone else, not to us — styled wherever the component is
# mounted whether or not this sample styles them, so a missing rule here is not a missing rule
# anywhere that matters.
#
# It held six names until the munin-explorer rename: the container and the results column
# helsedata's variable page is built from, plus the header row their column picker hangs in and
# the dropdown it opens, every one read back off their compiled variables.css. The package
# borrowed their prefix and inherited their rules for free, which worked on helsedata.no and
# nowhere else. It now writes its own `munin-explorer` names, so there is nothing left to skip.
THEIRS=(
  # Empty by construction, and a list rather than a deletion: the category comes back the moment
  # the package borrows a prefixed name again. Anything added here needs a note saying which
  # stylesheet it was read back off, the way the six used to — and the same note in
  # VariableExplorerTest's `invented` assertions, which annotate the prefix name by name.
)

# Names in the prefix that are ours but are not classes: element ids the package writes down in
# prose without the `-{instance}` suffix that completes them at runtime. A stylesheet cannot have
# a rule for them, because `.munin-explorer-source` selects nothing — the attribute is `id`.
#
# The `-$` filter below catches the ones written with their trailing hyphen; these are the ones
# written bare, in a sentence explaining what they are. `munin-explorer-source` is the whole
# list and arrived that way: it was a class while the kilde opened inside the variable's panel,
# and became an id prefix when the panel became a drill-in, leaving prose that still names it.
#
# Kept apart from THEIRS on purpose. THEIRS means "something else styles this, so a missing rule
# here costs nothing"; this means "no rule is possible". Filing one under the other would claim
# the name is styled somewhere, which it is not — it is not a class at all.
IDS=(
  munin-explorer-source
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

# Both clauses below ask whether the stylesheet mentions a selector, by searching its text. That
# question has to be put to the RULES only. This file carries more prose than CSS, and a comment
# naming a selector is indistinguishable from a rule declaring one to a substring search — the
# comment above the kildetype badge, which exists to record that helsedata has no `tag` class,
# was written with a leading dot and made this check answer "styled" for that very name. A check
# a comment can satisfy is a check prose can switch off, so strip them first.
STRIPPED=$(mktemp)
trap 'rm -f "$STRIPPED"' EXIT
perl -0pe 's{/\*.*?\*/}{ }gs' "$MODERN" > "$STRIPPED"

# An empty result would report every name as unstyled, which is loud rather than silent — but it
# would be loud about the wrong thing, and a reader would go looking for missing rules that are
# all still there. Say what actually broke instead.
if [ ! -s "$STRIPPED" ]; then
  echo "::error::Stripping comments from '$MODERN' produced nothing, so the checks below would" >&2
  echo "report every class name as unstyled. Is perl on PATH?" >&2
  exit 2
fi

# Clause two, on either copy — they are identical by the time we get here.
#
# The extraction below IS clause two: a name it does not produce is a name nothing checks. So it
# is checked before it is used, the way a missing stylesheet is checked above, rather than left to
# a `while read` loop that runs zero times. There is no `-e` in the `set` line, the pipeline would
# sit inside a process substitution where its exit status is discarded anyway, and grep's "No such
# file or directory" is one stderr line scrolling past in a job that stays green — so an extraction
# returning nothing would leave `missing` empty and print that every name is styled, having looked
# at none of them. src/ moving, and the prefix being renamed, are exactly the changes that need
# this guard most — the rename from `variable-explorer` to `munin-explorer` has already happened
# once, and a grep still spelling the old prefix would have gone quiet rather than red.
if [ ! -d src ]; then
  echo "::error::No src/ directory to read class names out of, so the check below would pass" >&2
  echo "without checking anything. Has the solution layout moved? Run this from the checkout." >&2
  exit 2
fi

names=()
while read -r name; do
  names+=("$name")
done < <(
  grep -rhoE --include='*.cs' --include='*.razor' --exclude-dir=bin --exclude-dir=obj \
         'munin-explorer[A-Za-z0-9_-]*' src/ \
    | grep -vE -- '(-|__)$' \
    | sort -u
)

# A floor, not a count. Names come and go with the component, so a check that had to be updated
# every time one was added would be updated without being read. This one only has to tell "the
# extraction works" from "the extraction found nothing" — 10 is far below the 75 the extraction
# yields today and far above anything a stale regex returns.
MIN_NAMES=10
if [ "${#names[@]}" -lt "$MIN_NAMES" ]; then
  echo "::error::Found only ${#names[@]} class name(s) under src/, below the floor of $MIN_NAMES." >&2
  echo "Either the naming convention moved off the 'munin-explorer' prefix or the regex in this" >&2
  echo "script went stale against it. Either way the check below cannot see what it is meant to" >&2
  echo "check, which is an error rather than a pass — fix the extraction, and the floor with it if" >&2
  echo "the package really does invent fewer than $MIN_NAMES names now." >&2
  exit 2
fi

missing=()
for name in "${names[@]}"; do
  case " ${THEIRS[*]} ${IDS[*]} " in
    *" $name "*) continue ;;
  esac

  # Anchored on both sides so `.munin-explorer-period__fill` does not answer for
  # `.munin-explorer-period`: a rule for the part is not a rule for the whole.
  grep -qE "\.${name}([^A-Za-z0-9_-]|\$)" "$STRIPPED" || missing+=("$name")
done

if [ ${#missing[@]} -gt 0 ]; then
  echo "::error::The sample stylesheet has no rule for ${#missing[@]} class name(s) the package invents:" >&2
  printf '  %s\n' "${missing[@]}" >&2
  echo "" >&2
  echo "Each renders at raw browser defaults in both samples, which reads as a bug in the" >&2
  echo "component rather than as a host that has not been asked for the rule. Add a rule to" >&2
  echo "$LEGACY, copy it over $MODERN — or, if the name turns out" >&2
  echo "to be borrowed rather than ours, add it to THEIRS at the top of this script and say which" >&2
  echo "stylesheet you read it back off." >&2
  exit 1
fi

# Clause three: the names the package BORROWS.
#
# Clauses one and two between them check every name we invent. They checked nothing at all about
# the names we take from helsedata's design system, because those carry no `munin-explorer`
# prefix to find them by — and a borrowed name is only free styling if a host stylesheet really
# defines it. `headline-sm` sat in VariableView for months looking exactly like a borrowed name.
# It was a typo for `headline-s`; nothing anywhere defines it; nine block headings rendered at the
# browser's own `<h*>` size inside helsedata and every check in this repo stayed green, because
# each one was busy verifying the names we made up.
#
# So: a name the package writes into a class attribute must be styled by the sample stylesheet
# (ours) or listed in the fixture (theirs). A name in neither is an orphan.
#
# THE TWO HALVES COVER DIFFERENT GROUND, and knowing where each stops matters more than knowing
# what each catches.
#
# This half reads every file under src/, whether or not a test renders it. KildeView has no test
# at all today, so the badge class on it is checked here and nowhere else.
#
# Where this half stops: it reads class ATTRIBUTES, and the views also pass class names to helpers
# as arguments — `@Heading(BlockLevel, T.HeadingMetadata, "headline headline-s")`. A name arriving
# in the DOM that way is invisible to grep. `headline-sm` arrived exactly that way, which is why
# adding this clause did not catch it: the first version was written, run against the reintroduced
# typo, and reported success.
#
# The half that catches those is HostClassNames in the test project, which renders the component
# and reads the DOM. Neither is sufficient alone. Do not read a pass here as "every borrowed name
# is styled".
HOST_NAMES=test/host-class-names.txt
if [ ! -f "$HOST_NAMES" ]; then
  echo "::error::'$HOST_NAMES' is missing, so the borrowed names below cannot be checked." >&2
  echo "It lists the class names helsedata's own stylesheets define; the header in the file says" >&2
  echo "how it was captured and how to capture it again." >&2
  exit 2
fi

# Every whitespace-separated token in a literal class attribute, from both the .razor `class="…"`
# form and the .cs `AddAttribute(n, "class", "…")` one. Tokens carrying `@` or `{` are Razor
# expressions rather than names — the value is decided at runtime and there is nothing to look up.
emitted=()
while read -r name; do
  emitted+=("$name")
done < <(
  {
    grep -rhoE 'class="[^"@{]*"' src/ --include='*.razor' --exclude-dir=bin --exclude-dir=obj | sed 's/^class="//; s/"$//'
    grep -rhoE '"class", *"[^"@{]*"' src/ --include='*.cs' --exclude-dir=bin --exclude-dir=obj | sed 's/^"class", *"//; s/"$//'
  } | tr ' ' '\n' | grep -vE '^$' | sort -u
)

MIN_EMITTED=20
if [ "${#emitted[@]}" -lt "$MIN_EMITTED" ]; then
  echo "::error::Found only ${#emitted[@]} class name(s) in class attributes under src/, below the" >&2
  echo "floor of $MIN_EMITTED. The extraction has gone stale against the markup, so the check below" >&2
  echo "would pass having looked at almost nothing. Fix the extraction, not the floor." >&2
  exit 2
fi

orphans=()
for name in "${emitted[@]}"; do
  grep -qE "\.${name}([^A-Za-z0-9_-]|\$)" "$STRIPPED" && continue
  grep -qxF "$name" "$HOST_NAMES" && continue
  orphans+=("$name")
done

if [ ${#orphans[@]} -gt 0 ]; then
  echo "::error::${#orphans[@]} class name(s) the package emits are styled by nothing — not the" >&2
  echo "sample stylesheet, and not helsedata's:" >&2
  printf '  %s\n' "${orphans[@]}" >&2
  echo "" >&2
  echo "Each renders unstyled on helsedata.no. Either it is a typo for a real host name — check" >&2
  echo "$HOST_NAMES for what they actually define — or it is a name of ours that still needs a" >&2
  echo "rule in $LEGACY, copied over $MODERN." >&2
  exit 1
fi

echo "Sample host stylesheets are in step ($(wc -l < "$MODERN" | tr -d ' ') lines), style every name"
echo "the package invents, and every borrowed name in a class attribute is one a host stylesheet"
echo "defines. Names reaching the DOM through helper arguments are checked by HostClassNames in the"
echo "test project, not here."
