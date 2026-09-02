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
# all of them executed.
#
# Usage:
#   scripts/assert-drift-ran.sh <trx-file> [minimum-tests] [category]
#
# The minimum is how many tests are expected — one per endpoint. Passing it means deleting a test
# is a decision somebody makes here, rather than a number that quietly goes down. The category is
# named in the diagnostics only, so the same guard serves ContractDrift and FixtureFreshness and
# points at the right trait when it fires.

set -uo pipefail

TRX="${1:-}"
MINIMUM="${2:-1}"
CATEGORY="${3:-ContractDrift}"

if [ -z "$TRX" ]; then
  echo "Usage: scripts/assert-drift-ran.sh <trx-file> [minimum-tests] [category]" >&2
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

echo "$CATEGORY tests: $total found, $executed executed, $not_executed skipped."

failures=0

if [ "$executed" -lt "$MINIMUM" ]; then
  echo "::error::Expected at least $MINIMUM $CATEGORY tests to run; $executed did." >&2
  echo "  * MUNIN_EXPLORER_LIVE must be set for the job, or every test skips itself." >&2
  echo "  * --filter must still match the [Trait(\"Category\", \"$CATEGORY\")] the test class carries." >&2
  echo "  * If a test was removed on purpose, lower the minimum passed to this script." >&2
  failures=1
fi

if [ "$not_executed" -ne 0 ]; then
  echo "::error::$not_executed of $total $CATEGORY tests were skipped. A skipped test checked nothing." >&2
  echo "  * A [LiveApiFact] skips itself when MUNIN_EXPLORER_LIVE is unset — check it reached this job." >&2
  echo "  * A test skipped for any other reason is a Skip= somebody left on a [Fact]." >&2
  failures=1
fi

exit "$failures"
