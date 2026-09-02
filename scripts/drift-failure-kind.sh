#!/usr/bin/env bash
#
# Prints what kind of failure a nightly live run was: `unreachable`, or what the run checks.
#
# The workflow titles its issue from the answer. Reporting an outage as drift sends whoever picks
# it up to edit DTOs that were never wrong, which is what happened for three nights running
# (Fhi.Metadata-ghxh4). LiveApiConnection writes LiveApi.UnreachableMarker into the failure message
# when nothing answered; this looks for it, and ShapeDriftTest pins the two spellings together.
#
# Usage:
#   scripts/drift-failure-kind.sh <trx-file> [kind-when-reachable]
#
# The second argument is what a real failure is called, so the same classifier serves both live
# runs: `drift` for the contract tests, `fixture` for the freshness ones. The two have different
# fixes — edit a DTO, or re-capture a file under Testdata/ — and the sidecar titles from it.
#
# The reachable kind is also the answer when the results file is missing or says nothing: that is
# the case where somebody has to read the log, and a failure report is what tells them to.

set -uo pipefail

# Must match LiveApi.UnreachableMarker.
MARKER='API-UNREACHABLE'

TRX="${1:-}"
KIND="${2:-drift}"

if [ -z "$TRX" ]; then
  echo "Usage: scripts/drift-failure-kind.sh <trx-file> [kind-when-reachable]" >&2
  exit 2
fi

if [ -s "$TRX" ] && grep -qF "$MARKER" "$TRX"; then
  echo unreachable
else
  echo "$KIND"
fi
