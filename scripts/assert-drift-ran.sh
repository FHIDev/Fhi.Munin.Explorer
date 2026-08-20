#!/usr/bin/env bash
#
# Fails if the contract-drift tests did not actually run.
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
#   scripts/assert-drift-ran.sh <trx-file> [minimum-tests]
#
# The minimum is how many drift tests are expected — one per endpoint. Passing it means deleting
# a test is a decision somebody makes here, rather than a number that quietly goes down.

set -uo pipefail

TRX="${1:-}"
MINIMUM="${2:-1}"

if [ -z "$TRX" ]; then
  echo "Usage: scripts/assert-drift-ran.sh <trx-file> [minimum-tests]" >&2
  exit 2
fi

if [ ! -s "$TRX" ]; then
  echo "::error::No test results at '$TRX'. The drift tests did not run, so nothing was checked against the live API." >&2
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
not_executed=$(count_of notExecuted)

: "${total:=0}" "${executed:=0}" "${not_executed:=0}"

echo "Drift tests: $total found, $executed executed, $not_executed skipped."

failures=0

if [ "$executed" -lt "$MINIMUM" ]; then
  echo "::error::Expected at least $MINIMUM contract-drift tests to run; $executed did." >&2
  echo "  * MUNIN_EXPLORER_LIVE must be set for the job, or every test skips itself." >&2
  echo "  * --filter must still match the [Trait(\"Category\", \"ContractDrift\")] on ContractDriftTest." >&2
  echo "  * If a test was removed on purpose, lower the minimum passed to this script." >&2
  failures=1
fi

if [ "$not_executed" -ne 0 ]; then
  echo "::error::$not_executed of $total contract-drift tests were skipped. A skipped test checked nothing." >&2
  failures=1
fi

exit "$failures"
