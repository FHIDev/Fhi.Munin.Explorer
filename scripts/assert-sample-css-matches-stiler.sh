#!/usr/bin/env bash
#
# Does the sample stand-in DECLARE what Fhi.Helsedata.Stiler declares?
#
# WHY THIS EXISTS, and it is not the same question `assert-sample-css-in-step.sh` asks. That one
# asks whether every `munin-explorer*` name has a rule declaring SOMETHING. It was green on
# 2026-09-07 while a manual diff of the sample against Stiler's component partials found around
# forty divergences: a rule carrying half of Stiler's declarations passes it, and so does the right
# property carrying the wrong value. `.munin-explorer-frequency__track` is the one to remember —
# it painted grey20 where Stiler's `_kodeverk.scss` says, in as many words, «IKKE grey20, som
# prøvestilarkene bruker». Stiler's own comments already named the sample stylesheets as the thing
# that gets it wrong, and nothing here measured it. (Fhi.Metadata-3dwar)
#
# So this compares DECLARATIONS — property and value — for the selectors under the `munin-explorer`
# prefix the package owns. `scripts/sample-css-declarations.mjs` is the comparison, and the comment
# above `NOT_COMPARED` in it is the honest statement of what is compared and what is not: font
# family and `src` are skipped because Stiler ships a typeface this repository cannot redistribute,
# shorthands are compared as written rather than expanded, and specificity and source order are not
# compared at all.
#
# WHICH STILER. The published package, read out of the NuGet global-packages folder — not a
# checkout of Stiler's `main`. That is a deliberate choice and not a compromise. Diffing against
# `main` answers "does the fixture match unreleased work"; diffing against the pinned package
# answers "does the fixture match the Stiler our hosts will actually restore", which is the question
# whose failure keeps reaching helsedata. Compiling Stiler's scss from source (Explorer #203) is the
# INVESTIGATION tool, for looking ahead of a release; it is not what a host restores and it skips
# whatever the package build does beyond sass.
#
# The stylesheet is already on the CI runner. `.github/workflows/ci.yml` restores HostileHost
# against helsedata's private feed for the layout job and reads exactly this file, which is why
# this guard needs no new infrastructure, no scheduled job and no credentials of its own.
#
# WHAT THAT COSTS, said plainly: the feed secret is not available to a pull request from a fork, so
# the whole job SKIPS there and this comparison does not run. The summary job counts a skip as
# fine, which is right — it is the same bound `check-hostile-host.sh` beside it has always had, and
# an unconfigured guard should say so and stop rather than go red. But it means a required check
# passing is not on its own proof that the stylesheets were compared. Read the job, not the tick.
#
# THE BASELINE IS NOT SELF-UPDATING, and that is the point of it. 175 declaration-level
# divergences stand today. They are listed in test/sample-css-known-divergences.txt, this script
# reads that list, and NOTHING here ever writes to it. A guard that records its own failures is
# decoration. So:
#
#   - a divergence not in the list fails the build. Fix the stylesheet, or add the line by hand
#     with a note saying why it stands;
#   - a line in the list that no longer diverges ALSO fails the build, with an instruction to
#     delete it. Otherwise the list rots into a claim nobody has checked, which is how the check it
#     replaces stopped meaning anything.
#
# The count can only go down, and every step down is a decision somebody made.
#
# Usage:
#   scripts/assert-sample-css-matches-stiler.sh
#   STILER_MAIN_CSS=… SAMPLE_CSS_MODERN=… KNOWN_DIVERGENCES=… \
#     scripts/assert-sample-css-matches-stiler.sh          # tests only
#
# Needs: node, and Fhi.Helsedata.Stiler restored. Locally that means the Azure Artifacts Credential
# Provider and `dotnet restore samples/HostileHost/HostileHost.csproj`; see nuget.config.

set -uo pipefail

# Anchored on the script's own location rather than on `git rev-parse --show-toplevel`, which
# answers for whatever repository the caller happens to be standing in. Same reasoning as
# assert-sample-css-in-step.sh, which has the long version of it.
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2

MODERN="${SAMPLE_CSS_MODERN:-samples/ModernHost/wwwroot/host.css}"
LEGACY="${SAMPLE_CSS_LEGACY:-samples/LegacyHost/wwwroot/css/host.css}"
KNOWN="${KNOWN_DIVERGENCES:-test/sample-css-known-divergences.txt}"
ENGINE="scripts/sample-css-declarations.mjs"

# WHICH SAMPLE HOST THIS READS, and why one and not two: the two copies are one stylesheet, byte
# for byte, and `assert-sample-css-in-step.sh` fails the build when they drift. So a divergence
# found in ModernHost's copy is a divergence in LegacyHost's, and comparing both would compare the
# same bytes twice and print every finding twice.
#
# The identity is asserted HERE as well rather than relied upon, because the two guards run as
# separate CI jobs and neither waits for the other: if the copies HAVE drifted, this script would
# otherwise report ModernHost's divergences under a heading claiming to speak for both hosts. It
# defers the fixing to the guard that owns that clause rather than duplicating its advice.
for f in "$MODERN" "$LEGACY" "$ENGINE"; do
  if [ ! -f "$f" ]; then
    echo "::error::'$f' is missing, so nothing below can be compared." >&2
    exit 2
  fi
done

if ! cmp -s "$MODERN" "$LEGACY"; then
  echo "::error::The two sample host stylesheets have drifted apart, so this guard cannot speak" >&2
  echo "for both hosts. scripts/assert-sample-css-in-step.sh owns that clause and says how to fix" >&2
  echo "it; run it first." >&2
  exit 2
fi

# The version is pinned in exactly one place. Hardcoding it here as well means a bump has to be
# made twice and the second one gets forgotten — which happened on this exact path on 0.1.37 ->
# 0.1.38, and the failure named a missing package rather than a stale assertion. ci.yml reads it
# the same way for the same reason.
CSPROJ="samples/HostileHost/HostileHost.csproj"
if [ -z "${STILER_MAIN_CSS:-}" ]; then
  if [ ! -f "$CSPROJ" ]; then
    echo "::error::'$CSPROJ' is missing, so the pinned Fhi.Helsedata.Stiler version cannot be read." >&2
    exit 2
  fi
  ver=$(sed -n 's/.*Include="Fhi\.Helsedata\.Stiler" Version="\([^"]*\)".*/\1/p' "$CSPROJ")
  if [ -z "$ver" ]; then
    echo "::error::Could not read the Fhi.Helsedata.Stiler version from '$CSPROJ'. The" >&2
    echo "PackageReference has moved or been renamed; this guard must follow it rather than pin a" >&2
    echo "version of its own." >&2
    exit 2
  fi
  # NUGET_PACKAGES wins where it is set, which is how a CI runner with a relocated global-packages
  # folder still finds the restore it just did.
  packages="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
  STILER_MAIN_CSS="$packages/fhi.helsedata.stiler/$ver/staticwebassets/css/main.css"
fi

if [ ! -f "$STILER_MAIN_CSS" ]; then
  echo "::error::Fhi.Helsedata.Stiler's stylesheet is not at" >&2
  echo "  $STILER_MAIN_CSS" >&2
  echo "so there is nothing to compare the sample against. Restore the package first:" >&2
  echo "  dotnet restore samples/HostileHost/HostileHost.csproj" >&2
  echo "That needs credentials for helsedata's Azure Artifacts feed — see nuget.config. In CI the" >&2
  echo "job skips itself when the feed secret is absent rather than passing having read nothing." >&2
  exit 2
fi

if ! command -v node >/dev/null 2>&1; then
  echo "::error::node is not on PATH, and the comparison in $ENGINE needs it." >&2
  exit 2
fi

if [ ! -f "$KNOWN" ]; then
  echo "::error::'$KNOWN' is missing. It is the explicit list of divergences that are known to" >&2
  echo "stand; without it every one of them would be reported as new. It is edited by hand and" >&2
  echo "never written by this script — see the header inside it." >&2
  exit 2
fi

FOUND=$(mktemp)
DETAIL=$(mktemp)
BASE=$(mktemp)
trap 'rm -f "$FOUND" "$DETAIL" "$BASE"' EXIT

if ! node "$ENGINE" "$MODERN" "$STILER_MAIN_CSS" --detail > "$DETAIL" 2>/dev/null; then
  echo "::error::The comparison in $ENGINE failed to run. Re-run it directly to see why:" >&2
  echo "  node $ENGINE $MODERN $STILER_MAIN_CSS --detail" >&2
  exit 2
fi

cut -f1 < "$DETAIL" | LC_ALL=C sort -u > "$FOUND"

# A floor, not a count, and it guards the same failure the floors in assert-sample-css-in-step.sh
# guard: an extraction that stops matching reports zero divergences, which is indistinguishable
# from a perfect stylesheet and would be reported as a pass. Stiler carries 275 rules under the
# prefix today; a stale parser yields a handful.
RULES=$(node "$ENGINE" "$MODERN" "$STILER_MAIN_CSS" 2>&1 >/dev/null | sed -n 's/.*across \([0-9]*\) Stiler rule.*/\1/p')
MIN_STILER_RULES=100
if [ -z "$RULES" ] || [ "$RULES" -lt "$MIN_STILER_RULES" ]; then
  echo "::error::Read only ${RULES:-0} rule(s) under the munin-explorer prefix out of" >&2
  echo "  $STILER_MAIN_CSS" >&2
  echo "which is below the floor of $MIN_STILER_RULES. Either the parser in $ENGINE has gone stale" >&2
  echo "against the stylesheet, or this is not Stiler's real main.css — and either way the" >&2
  echo "comparison below would report a clean run having compared almost nothing." >&2
  exit 2
fi

# Comments and blank lines out; the rest is the baseline, one key per line.
grep -vE '^[[:space:]]*(#|$)' "$KNOWN" | LC_ALL=C sort -u > "$BASE"

new=$(LC_ALL=C comm -23 "$FOUND" "$BASE")
fixed=$(LC_ALL=C comm -13 "$FOUND" "$BASE")

status=0

if [ -n "$new" ]; then
  count=$(printf '%s\n' "$new" | wc -l | tr -d ' ')
  echo "::error::$count new divergence(s) between the sample stand-in and Fhi.Helsedata.Stiler," >&2
  echo "none of them listed in $KNOWN. Compared against" >&2
  echo "  $STILER_MAIN_CSS" >&2
  echo "" >&2
  while IFS= read -r key; do
    [ -n "$key" ] || continue
    printf '  %s\n' "$(grep -F "$key	" "$DETAIL" | head -1 | cut -f2-)" >&2
    printf '    %s\n' "$key" >&2
  done <<< "$new"
  echo "" >&2
  echo "The sample hosts exist so that a name is drawn here the way it is drawn on helsedata.no." >&2
  echo "Fix the rule in whichever copy you are editing, then copy that file over the other:" >&2
  echo "  cp $MODERN $LEGACY     # or the other way round" >&2
  echo "" >&2
  echo "Or, if the divergence is deliberate, add its key to $KNOWN with a note saying why. Adding" >&2
  echo "it is a hand edit on purpose: this script never writes that file." >&2
  status=1
fi

if [ -n "$fixed" ]; then
  count=$(printf '%s\n' "$fixed" | wc -l | tr -d ' ')
  echo "::error::$count line(s) in $KNOWN no longer describe a divergence. Delete them:" >&2
  echo "" >&2
  # Read line by line rather than letting printf word-split: a key holds a selector, and a
  # selector holds spaces — `missing-selector|@media (max-width:1280px)|.munin-explorer-meta table|`
  # would otherwise arrive as five lines of nonsense that match nothing in the file.
  while IFS= read -r key; do
    [ -n "$key" ] || continue
    printf '  %s\n' "$key" >&2
  done <<< "$fixed"
  echo "" >&2
  echo "This is the good failure — the stylesheet moved closer to Stiler. The list is only worth" >&2
  echo "reading if every line in it is still true, so a stale entry is as much a defect as a new" >&2
  echo "divergence: it is a claim nobody has checked, which is exactly how the check this one" >&2
  echo "supplements stopped meaning anything." >&2
  status=1
fi

[ "$status" = "0" ] || exit "$status"

echo "The sample stand-in matches Fhi.Helsedata.Stiler's declarations for every selector under the"
echo "munin-explorer prefix, apart from the $(wc -l < "$BASE" | tr -d ' ') divergence(s) listed in $KNOWN."
echo "Compared $RULES Stiler rule(s) property by property; both sample hosts are covered, because"
echo "their two copies are byte-identical and this script checked that before comparing one."
