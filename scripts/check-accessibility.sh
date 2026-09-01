#!/usr/bin/env bash
#
# Runs axe against the ModernHost sample and fails on any violation. A green run means no
# DETECTED regression and nothing more; what this gate is blind to is in AGENTS.md under
# "Accessibility is a requirement, not a preference". Read it before quoting a pass.
#
# Some of what it scans is behind a press. TARGETS below says which states, and which it leaves
# alone on purpose — read that before adding a state, and before quoting this one either.
#
# Usage:  ./scripts/check-accessibility.sh
# Needs:  dotnet, node (for npx), and a Chrome/Chromium on PATH.

set -euo pipefail

# Pinned. An unpinned npx resolves to whatever is newest on the day, which turns an
# unrelated PR red for a reason nobody changed.
PLAYWRIGHT_VERSION="1.49.1"
AXE_PLAYWRIGHT_VERSION="4.10.1"

PORT="${ACCESSIBILITY_PORT:-5099}"
BASE="http://localhost:${PORT}"
STUB_PORT="${ACCESSIBILITY_STUB_PORT:-5098}"
STUB_BASE="http://127.0.0.1:${STUB_PORT}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# What the scan visits, each as `path::state` naming a state in scripts/axe-states.mjs. Every
# state waits for content first, the list pages included: axe reports no violations in a page whose
# data never arrived. Why states at all: AGENTS.md, "It scans states, not only pages".
#
# DELIBERATELY NOT COVERED, so nobody reads a green run as more than it is:
#   - the whole-variable drill-in and the owner panel inside a row, two more presses each;
#   - the kildeutforsker's own facet panel, which sits behind its `Vis filtre` toggle;
#   - the pager past page one, and anything reached by searching or by narrowing a facet;
#   - error and empty states, which need the stub to answer differently than it does;
#   - the English texts, and samples/LegacyHost, the same component in the other host.
# Each is another page load and settle, about ten seconds, and none carries the risk the five
# targets below do. Add one here and in axe-states.mjs when that stops being true.
TARGETS=(
  "/::variables-list"
  "/kilder::kilder-list"
  "/::filters-level-lines"
  "/::variable-detail"
  "/kilder::kilde-drilldown"
)

host_pid=""
stub_pid=""
cleanup() {
  for pid in "$host_pid" "$stub_pid"; do
    if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
      kill "$pid" 2>/dev/null || true
      wait "$pid" 2>/dev/null || true
    fi
  done
}
trap cleanup EXIT

# Anything already answering on these ports is scanned in place of what this run starts,
# stylesheet and all — an orphan from a previous run is the usual case, and a green run that
# belongs to someone else's page is the result. Both checked before either is started.
for occupied in "$BASE/" "${STUB_BASE}/api/explorer/kilder"; do
  if curl -fsS -o /dev/null --max-time 2 "$occupied" 2>/dev/null; then
    echo "something is already listening on ${occupied} - TOOLING failure." >&2
    echo "stop it, or set ACCESSIBILITY_PORT / ACCESSIBILITY_STUB_PORT to free ports." >&2
    exit 2
  fi
done

# The fixtures, not the network. The live API is unreachable from a GitHub runner, and a scan
# pointed at it renders an empty shell that axe passes — see the stub's header.
echo "==> starting the stub API on ${STUB_BASE}"
node "$ROOT/scripts/axe-stub-api.mjs" "$STUB_PORT" >/tmp/accessibility-stub.log 2>&1 &
stub_pid=$!

for _ in $(seq 1 20); do
  if curl -fsS -o /dev/null --max-time 2 "${STUB_BASE}/api/explorer/kilder" 2>/dev/null; then
    break
  fi
  if ! kill -0 "$stub_pid" 2>/dev/null; then
    break
  fi
  sleep 1
done

if ! kill -0 "$stub_pid" 2>/dev/null; then
  echo "the stub API exited before it answered - TOOLING failure." >&2
  tail -10 /tmp/accessibility-stub.log >&2
  exit 2
fi

if ! curl -fsS -o /dev/null --max-time 5 "${STUB_BASE}/api/explorer/kilder" 2>/dev/null; then
  echo "the stub API never answered on ${STUB_BASE} - TOOLING failure." >&2
  tail -10 /tmp/accessibility-stub.log >&2
  exit 2
fi

echo "==> starting ModernHost on ${BASE}"
(
  cd "$ROOT"
  MuninExplorer__ApiBaseUrl="$STUB_BASE" dotnet run --project samples/ModernHost --urls "$BASE" >/tmp/accessibility-host.log 2>&1
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

# Playwright brings its own browser, so nothing here depends on what the runner has.
echo "==> installing the scanner"
npm install --no-save --silent \
    "playwright@${PLAYWRIGHT_VERSION}" \
    "@axe-core/playwright@${AXE_PLAYWRIGHT_VERSION}" >/tmp/npm-install.log 2>&1 || {
  echo "could not install the scanner - TOOLING failure." >&2
  tail -10 /tmp/npm-install.log >&2
  exit 2
}

npx --yes playwright install chromium >/tmp/pw-install.log 2>&1 || {
  echo "could not install chromium - TOOLING failure." >&2
  tail -10 /tmp/pw-install.log >&2
  exit 2
}

set +e
node "$ROOT/scripts/axe-scan.mjs" $(for t in "${TARGETS[@]}"; do printf '%s ' "${BASE}${t}"; done)
scan_status=$?
set -e

# 2 is the scanner saying it could not run. Passing that through unchanged keeps a
# broken toolchain from reading as a broken page.
if [ "$scan_status" -eq 2 ]; then
  exit 2
fi

violations=0
[ "$scan_status" -ne 0 ] && violations=1

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
