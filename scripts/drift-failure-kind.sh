#!/usr/bin/env bash
#
# Prints what kind of failure the nightly contract run was: `unreachable` or `drift`.
#
# The workflow titles its issue from the answer. Reporting an outage as drift sends whoever picks
# it up to edit DTOs that were never wrong, which is what happened for three nights running
# (Fhi.Metadata-ghxh4). LiveApiConnection writes LiveApi.UnreachableMarker into the failure message
# when nothing answered; this looks for it, and ShapeDriftTest pins the two spellings together.
#
# Usage:
#   scripts/drift-failure-kind.sh <trx-file>
#
# `drift` is the answer when the results file is missing or says nothing: that is the case where
# somebody has to read the log, and the drift report is the one that tells them to.

set -uo pipefail

# Must match LiveApi.UnreachableMarker.
MARKER='API-UNREACHABLE'

TRX="${1:-}"

if [ -z "$TRX" ]; then
  echo "Usage: scripts/drift-failure-kind.sh <trx-file>" >&2
  exit 2
fi

if [ -s "$TRX" ] && grep -qF "$MARKER" "$TRX"; then
  echo unreachable
else
  echo drift
fi
