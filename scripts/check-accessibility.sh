#!/usr/bin/env bash
#
# Runs axe against the ModernHost sample and fails on any violation. A green run means no
# DETECTED regression and nothing more; what this gate is blind to is in AGENTS.md under
# "Accessibility is a requirement, not a preference". Read it before quoting a pass.
#
# Usage:  ./scripts/check-accessibility.sh
# Needs:  dotnet, node (for npx), and a Chrome/Chromium on PATH.

set -euo pipefail

# Pinned. An unpinned npx resolves to whatever is newest on the day, which turns an
# unrelated PR red for a reason nobody changed.
AXE_CLI_VERSION="4.10.1"

PORT="${ACCESSIBILITY_PORT:-5099}"
BASE="http://localhost:${PORT}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# The two root components the package ships. Both are entry points a host mounts on its
# own page, so both are surfaces a reader meets.
PATHS=("/" "/kilder")

host_pid=""
cleanup() {
  if [ -n "$host_pid" ] && kill -0 "$host_pid" 2>/dev/null; then
    kill "$host_pid" 2>/dev/null || true
    wait "$host_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT

echo "==> starting ModernHost on ${BASE}"
(
  cd "$ROOT"
  dotnet run --project samples/ModernHost --urls "$BASE" >/tmp/accessibility-host.log 2>&1
) &
host_pid=$!

echo "==> waiting for the host"
for _ in $(seq 1 60); do
  if curl -fsS -o /dev/null --max-time 2 "$BASE/" 2>/dev/null; then
    break
  fi
  if ! kill -0 "$host_pid" 2>/dev/null; then
    echo "the host exited before it answered:" >&2
    tail -30 /tmp/accessibility-host.log >&2
    exit 1
  fi
  sleep 2
done

if ! curl -fsS -o /dev/null --max-time 5 "$BASE/" 2>/dev/null; then
  echo "the host never answered on ${BASE}" >&2
  tail -30 /tmp/accessibility-host.log >&2
  exit 1
fi

# Blazor Server renders over a circuit, so the first paint is not the finished page.
# Without a settle the scan reads an empty shell and passes for the wrong reason.
SETTLE_MS="${ACCESSIBILITY_SETTLE_MS:-4000}"

violations=0
for p in "${PATHS[@]}"; do
  echo
  echo "==> axe ${BASE}${p}"

  out="$(mktemp)"
  set +e
  npx --yes "@axe-core/cli@${AXE_CLI_VERSION}" \
      "${BASE}${p}" \
      --tags wcag2a,wcag2aa,wcag21a,wcag21aa \
      --load-delay "${SETTLE_MS}" \
      --save "$out" \
      --exit 2>&1 | tee /tmp/axe-stdout.log
  axe_status=${PIPESTATUS[0]}
  set -e

  # axe exits non-zero for a violation AND for a toolchain failure, so telling a broken page
  # from a broken chromedriver needs the result file rather than the exit code: no file means
  # it never got as far as scanning, and calling that a violation trains people to ignore it.
  if [ ! -s "$out" ]; then
    echo >&2
    echo "axe could not run against ${BASE}${p} - this is a TOOLING failure, not a finding." >&2
    echo "Exit was ${axe_status}. Common cause: chromedriver could not start, or no Chrome on PATH." >&2
    rm -f "$out"
    exit 2
  fi

  if [ "$axe_status" -ne 0 ]; then
    violations=1
  fi
  rm -f "$out"
done

echo
if [ "$violations" -ne 0 ]; then
  cat >&2 <<'EOF'
Accessibility violations found. See the axe output above; each entry names the rule,
the element and a link to the fix.

Before you reach for a suppression: this gate is deliberately narrow, so a violation it
DID catch is very unlikely to be a false positive.
EOF
  exit 1
fi

cat <<'EOF'
No violations detected.

Read that literally. This gate sees the sample stylesheet, not the one the component
ships into, and automated checking cannot see missing structure at all. A green run is
evidence of no detected regression, and nothing more.

Why, at length: AGENTS.md, "Accessibility is a requirement, not a preference".
EOF
