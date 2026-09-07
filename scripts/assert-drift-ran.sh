#!/usr/bin/env bash
#
# Fails if a nightly live category did not actually run.
#
# The drift tests skip themselves unless MUNIN_EXPLORER_LIVE is set, which is what keeps them out
# of every ordinary `dotnet test`. That gate is also the way this whole check can quietly stop
# working: rename the variable, mistype the --filter, move the trait, and the scheduled job runs
# zero tests, reports success, and goes on reporting success for as long as anybody leaves it. A
# green square would then mean "the contracts are fine" when it means "nothing was looked at".
#
# So the job does not get to decide it passed. It has to show its work: a TRX with tests in it,
# all of them executed — and where one was not, which test it was and, in its own words, why.
#
# Usage:
#   scripts/assert-drift-ran.sh <trx-file> [minimum-tests] [category] [authenticated-tests]
#
# The minimum is how many tests the category has — one per endpoint. Passing it means deleting a
# test is a decision somebody makes here, rather than a number that quietly goes down. The category
# is named in the diagnostics only, so the same guard serves ContractDrift and FixtureFreshness and
# points at the right trait when it fires.
#
# The last number is how many of those tests need MUNIN_EXPLORER_TOKEN. No unattended run can hold
# one, so they are reported as UNCHECKED by name every run rather than excused: green here means
# the read half fits and nothing more (Fhi.Metadata-wpcb3; docs/contract-drift.md has the why).

set -uo pipefail

TRX="${1:-}"
MINIMUM="${2:-1}"
CATEGORY="${3:-ContractDrift}"
AUTHENTICATED="${4:-0}"

# Pinned to LiveApi.TokenVariable by DriftRanGuardTest: nothing else connects a C# constant to a
# line of bash, and the same pin already exists for LiveApi.UnreachableMarker.
TOKEN_VARIABLE=MUNIN_EXPLORER_TOKEN

if [ -z "$TRX" ]; then
  echo "Usage: scripts/assert-drift-ran.sh <trx-file> [minimum-tests] [category] [authenticated-tests]" >&2
  exit 2
fi

if [ ! -s "$TRX" ]; then
  echo "::error::No test results at '$TRX'. The $CATEGORY tests did not run, so nothing was checked against the live API." >&2
  exit 1
fi

counters=$(grep -o '<Counters[^>]*>' "$TRX" | head -1)

if [ -z "$counters" ]; then
  echo "::error::'$TRX' has no <Counters> element, so there is no way to tell what ran." >&2
  exit 1
fi

# The leading space matters: "notExecuted" ends with "executed".
count_of() {
  printf '%s' "$counters" | grep -o " $1=\"[0-9]*\"" | grep -o '[0-9]*' | head -1
}

total=$(count_of total)
executed=$(count_of executed)

: "${total:=0}" "${executed:=0}"

# Subtracted rather than read from the TRX's own notExecuted attribute, which is not the count it
# looks like: xUnit's dynamic skips — a Skip= set at construction, which is exactly what
# LiveApiFactAttribute does when MUNIN_EXPLORER_LIVE is unset — reach the VSTest TRX logger as
# tests that were never handed over to be run, so they land in total and nowhere else.
# notExecuted stays 0 through an all-skipped run, which would make this the guard that never fires
# and the diagnostic below a line that says "0 skipped" about eight skipped tests.
not_executed=$((total - executed))

# Which tests skipped, and what each one said about it. <Counters> can answer neither, and a bare
# count is how this guard spent four nights offering two causes that were both wrong while the
# real one sat in the same file, in the test's own words (Fhi.Metadata-wpcb3).
skipped=$(awk '
  BEGIN { RS = "<UnitTestResult" }
  /outcome="NotExecuted"/ {
    name = ""; reason = ""

    if (match($0, /testName="[^"]*"/)) {
      name = substr($0, RSTART + 10, RLENGTH - 11)
      sub(/.*\./, "", name)
    }

    # The only <Message> a skipped result carries is its skip reason; it produced no output.
    if (match($0, /<Message>[^<]*/)) {
      reason = substr($0, RSTART + 9, RLENGTH - 9)
      gsub(/&lt;/, "<", reason); gsub(/&gt;/, ">", reason); gsub(/&quot;/, "\"", reason)
      gsub(/&#x[0-9A-Fa-f]+;/, " ", reason); gsub(/&amp;/, "\\&", reason)
    }

    printf "%s\t%s\n", name, reason
  }' "$TRX")

unchecked=0
unchecked_lines=""
unexpected=0
unexpected_lines=""

while IFS=$'\t' read -r name reason; do
  [ -n "$name" ] || continue

  if printf '%s' "$reason" | grep -q -- "$TOKEN_VARIABLE"; then
    unchecked=$((unchecked + 1))
    unchecked_lines="$unchecked_lines  * $name — $reason"$'\n'
  else
    unexpected=$((unexpected + 1))
    unexpected_lines="$unexpected_lines  * $name — ${reason:-(the results file records no reason)}"$'\n'
  fi
done <<< "$skipped"

unaccounted=$((not_executed - unchecked - unexpected))

summary="$CATEGORY tests: $total found, $executed executed, $not_executed skipped."

if [ "$unchecked" -gt 0 ]; then
  summary="$summary $unchecked of them left the authenticated half unchecked."
fi

echo "$summary"

failures=0

# Read off `total` rather than `executed`, because a deleted test is the one thing `executed`
# cannot show: on a run where every test skipped it is already 0 and stays 0.
if [ "$total" -lt "$MINIMUM" ]; then
  echo "::error::Expected $MINIMUM $CATEGORY tests to exist; the run found $total." >&2
  echo "  * A test was deleted, renamed, or moved off [Trait(\"Category\", \"$CATEGORY\")]." >&2
  echo "  * If it went on purpose, bring the minimum passed to this script down with it." >&2
  failures=1
fi

runnable=$((MINIMUM - AUTHENTICATED))

if [ "$executed" -lt "$runnable" ]; then
  echo "::error::Expected at least $runnable $CATEGORY tests to run; $executed did." >&2
  echo "  * MUNIN_EXPLORER_LIVE must be set for the job, or every test skips itself." >&2
  echo "  * --filter must still match the [Trait(\"Category\", \"$CATEGORY\")] the test class carries." >&2
  echo "  * If a test was removed on purpose, lower the minimum passed to this script." >&2
  failures=1
fi

# Said out loud on every run it is true of, and said as "unchecked" rather than as a number in a
# skip column: a green square here means the read half fits, and must never come to mean more.
if [ "$unchecked" -gt 0 ]; then
  echo "::warning::$unchecked of $total $CATEGORY tests need $TOKEN_VARIABLE and did not run. That half of the contract is UNCHECKED by this run." >&2
  printf '%s' "$unchecked_lines" >&2
  echo "  No unattended run can hold that token: it is an ID-porten access token, issued to an" >&2
  echo "  embedding host's client for a signed-in person and good for about two minutes. Those" >&2
  echo "  arms are checked by hand, with a token, and docs/contract-drift.md says so." >&2
fi

if [ "$unchecked" -gt "$AUTHENTICATED" ]; then
  echo "::error::$unchecked $CATEGORY tests skipped for want of $TOKEN_VARIABLE; the caller declared $AUTHENTICATED." >&2
  echo "  * Naming that variable in a Skip= does not make a [Fact] one of the authenticated arms." >&2
  failures=1
fi

if [ "$unexpected" -gt 0 ]; then
  echo "::error::$unexpected of $total $CATEGORY tests were skipped. A skipped test checked nothing." >&2
  printf '%s' "$unexpected_lines" >&2
  echo "  Each reason above is the test's own, read out of the results file. The three to expect:" >&2
  echo "  * A [LiveApiFact] skips itself when MUNIN_EXPLORER_LIVE is unset — check it reached this job." >&2
  echo "  * A [LiveListsFact] skips itself when $TOKEN_VARIABLE is unset; those are counted apart, above." >&2
  echo "  * Anything else is a Skip= somebody left on a [Fact]." >&2
  failures=1
fi

# Fails closed. Either direction is the same fault: a guard whose two readings of one file
# disagree has stopped being able to see, and that is not a pass.
if [ "$unaccounted" -ne 0 ]; then
  echo "::error::<Counters> says $not_executed $CATEGORY tests did not run; the results list $((unchecked + unexpected))." >&2
  echo "  * The TRX logger and its own summary disagree; treat this run as having checked nothing." >&2
  failures=1
fi

exit "$failures"
