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
# So the second clause reads the names out of src/ and asks the stylesheet, for each, for a rule
# that DOES something. Not merely one that names it: an empty block draws exactly what no block
# draws, so a check that stops at the selector reports a rule nobody wrote a declaration into as
# coverage. The facet fold is the shape to keep in mind — the selector for it was never the
# missing half, the declaration that undoes the fold on a host with room for a sidebar was.
#
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
#   SAMPLE_CSS_MODERN=… SAMPLE_CSS_LEGACY=… HOST_CLASS_NAMES=… \
#     scripts/assert-sample-css-in-step.sh                                        # tests only
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

# The two copies — overridable, and for one reason only: so a test can run this whole script,
# every clause of it unchanged, against a stylesheet it has deliberately broken. Nothing else sets
# these; CI and the pre-PR checklist run the script bare and get the samples.
#
# The seam is here because the riskier half of this check is the shell half. Its C# twin has one
# already (HostClassNames.OrphansIn takes a stylesheet, so a test empties a rule in memory and
# watches the check go red), and without the same thing here every branch below — the NAMED/DRAWN
# split, the rule floor, `missing` against `empty` — is dead on every green run, which is how a
# guard stops working without anything saying so. SampleCssGuardTest in the test project drives it.
#
# HOST_CLASS_NAMES, at clause three, is the third override and exists for the same reason. A
# stylesheet is not enough to reach that clause: it is the only one whose names come from a second
# source, and every borrowed name the package emits is in the real fixture today, so the fixture
# rescues all of them however the stylesheet is mutated. Without a lever on the fixture, clause
# three is what clause two would be without these two — green on every run and never once seen to
# bite.
#
# Paths are resolved from the checkout root, because of the `cd` above; a test hands in absolute
# ones.
MODERN="${SAMPLE_CSS_MODERN:-samples/ModernHost/wwwroot/host.css}"
LEGACY="${SAMPLE_CSS_LEGACY:-samples/LegacyHost/wwwroot/css/host.css}"

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

# Both clauses below ask whether a selector is drawn, by searching text. That question has to be
# put to the RULES only. This file carries more prose than CSS, and a comment naming a selector is
# indistinguishable from a rule declaring one to a substring search — the comment above the
# kildetype badge, which exists to record that helsedata has no `tag` class, was written with a
# leading dot and made this check answer "styled" for that very name. A check a comment can satisfy
# is a check prose can switch off, so strip them first.
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

# The rules themselves, cut apart into selector and declaration block, because "does the file
# mention this name" and "does the file draw this name" are different questions and only the
# second one is worth asking. NAMED holds the selector of every rule; DRAWN holds the selector of
# every rule that declares something. A name in NAMED and not in DRAWN has a rule with nothing in
# it, which renders identically to having no rule at all — and the two are reported apart below,
# because "unstyled" alone sends a reader looking for a rule that is sitting right there empty.
#
# Innermost blocks only — `[^{}]` on both sides — so the rules inside an `@media` are read as
# themselves rather than swallowed whole by the at-rule. Whitespace in a selector is squeezed to
# single spaces so a selector broken across lines still arrives as one line here. This mirrors
# HostClassNames.CssRule in the test project, which does the same thing to the same file.
#
# An `@media` is not the only thing shaped like a block holding a block, and the other one is NOT
# handled the same way: a NESTED rule has the same shape and comes out wrong. Given
#
#   .munin-explorer-x { color: red; & .child { color: blue; } }
#
# the only innermost block is the child's, so the selector recorded is `color: red; & .child` —
# `.munin-explorer-x` reaches neither NAMED nor DRAWN, and its own declarations arrive on the
# selector side. A name styled only by such a rule is then reported as `missing`, pointing the
# reader at a rule that is sitting right there with declarations in it, which is the confusion the
# `empty` bucket below exists to avoid. Loud rather than silent, and the samples nest nothing today
# (228 rules, all captured) — but the day they do, this extraction has to learn about it rather
# than the reader having to.
RULES=$(mktemp)
NAMED=$(mktemp)
DRAWN=$(mktemp)
trap 'rm -f "$STRIPPED" "$RULES" "$NAMED" "$DRAWN"' EXIT

perl -0ne '
  while (/([^{}]*)\{([^{}]*)\}/g) {
    my ($selector, $declarations) = ($1, $2);
    $selector =~ s/\s+/ /g;
    $selector =~ s/^ //;
    $selector =~ s/ $//;
    # Whitespace and stray semicolons are not declarations: `{ ; }` is as silent as `{}`.
    my $verdict = $declarations =~ /[^\s;]/ ? "drawn" : "empty";
    print "$verdict\t$selector\n";
  }' "$STRIPPED" > "$RULES"

cut -f2- < "$RULES" > "$NAMED"
grep $'^drawn\t' "$RULES" | cut -f2- > "$DRAWN"

# The same guard the stripping above gets, for the same reason: an extraction that finds nothing
# would report every name as unstyled, loudly and about the wrong thing. A floor rather than a
# count — the stylesheet yields 228 rules today, and a stale regex yields a handful.
MIN_RULES=50
if [ "$(wc -l < "$DRAWN")" -lt "$MIN_RULES" ]; then
  echo "::error::Read only $(wc -l < "$DRAWN") non-empty rule(s) out of '$MODERN', below the floor" >&2
  echo "of $MIN_RULES. The rule extraction in this script has gone stale against the stylesheet, so" >&2
  echo "the checks below would report every class name as unstyled. Fix the extraction." >&2
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

# The exception lists are applied BEFORE the check, not after it, and that ordering is the whole
# reason they survive a stricter check. `munin-explorer-source` is the case to understand rather
# than delete: it is an id prefix and not a class — the region wears `munin-explorer-drilldown`,
# the id is `munin-explorer-source-{instance}` — so `.munin-explorer-source` selects nothing, no
# stylesheet can have a rule for it, and demanding a declaration for it would fail forever. The
# entry in IDS is the answer; removing it to make this loop green is not.
missing=()
empty=()
for name in "${names[@]}"; do
  case " ${THEIRS[*]} ${IDS[*]} " in
    *" $name "*) continue ;;
  esac

  # Anchored on both sides so `.munin-explorer-period__fill` does not answer for
  # `.munin-explorer-period`: a rule for the part is not a rule for the whole.
  if grep -qE "\.${name}([^A-Za-z0-9_-]|\$)" "$DRAWN"; then
    continue
  fi

  if grep -qE "\.${name}([^A-Za-z0-9_-]|\$)" "$NAMED"; then
    empty+=("$name")
  else
    missing+=("$name")
  fi
done

if [ ${#missing[@]} -gt 0 ] || [ ${#empty[@]} -gt 0 ]; then
  if [ ${#missing[@]} -gt 0 ]; then
    echo "::error::The sample stylesheet has no rule for ${#missing[@]} class name(s) the package invents:" >&2
    printf '  %s\n' "${missing[@]}" >&2
  fi

  if [ ${#empty[@]} -gt 0 ]; then
    echo "::error::The sample stylesheet names ${#empty[@]} class name(s) the package invents in a" >&2
    echo "selector and then declares nothing under it:" >&2
    printf '  %s\n' "${empty[@]}" >&2
    echo "" >&2
    echo "An empty block draws what no block draws, so the rule is coverage on paper only." >&2
  fi

  echo "" >&2
  echo "Each renders at raw browser defaults in both samples, which reads as a bug in the" >&2
  echo "component rather than as a host that has not been asked for the rule. Add a rule to" >&2
  echo "$LEGACY, copy it over $MODERN — or, if the name turns out" >&2
  echo "to be borrowed rather than ours, add it to THEIRS at the top of this script and say which" >&2
  echo "stylesheet you read it back off. If it is not a class at all — an id prefix, the way" >&2
  echo "munin-explorer-source is — it belongs in IDS instead, with a note saying so." >&2
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
#
# One thing about the reach of this clause, because the message it prints below describes more
# than it can ever be shown: it reads DRAWN, so an empty rule arrives here as no rule — but only
# ever for a BORROWED name. A `munin-explorer*` name whose rule is empty trips clause two and
# exits 1 before this clause is entered, and every name this clause reads is also read by that
# one. The empty-rule half of the advice below is therefore about names like `caption`, not about
# ours.
#
# Overridable for the same one reason the stylesheets above are: a test hands in a fixture with one
# borrowed name taken out of it, so that name's only remaining cover is the sample rule, and
# emptying that rule drives this clause red. Nothing else sets it; CI runs the script bare.
HOST_NAMES="${HOST_CLASS_NAMES:-test/host-class-names.txt}"
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

# Against DRAWN rather than STRIPPED, same as clause two: a rule the samples wrote with an empty
# block is not styling, here either. The fixture on the next line is a list of names and not a
# stylesheet, so nothing can be asked of helsedata's declarations — what that half claims is only
# that the name is one their stylesheets define.
orphans=()
for name in "${emitted[@]}"; do
  grep -qE "\.${name}([^A-Za-z0-9_-]|\$)" "$DRAWN" && continue
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
  echo "rule in $LEGACY, copied over $MODERN. This clause reads DRAWN, so" >&2
  echo "'no rule' includes a rule that names it and declares nothing: look for the selector before" >&2
  echo "concluding it is absent, because an empty block reaches here as though it were." >&2
  exit 1
fi

echo "Sample host stylesheets are in step ($(wc -l < "$MODERN" | tr -d ' ') lines), declare something"
echo "under every name the package invents, and every borrowed name in a class attribute is one a"
echo "host stylesheet defines. Names reaching the DOM through helper arguments are checked by"
echo "HostClassNames in the test project, not here."
