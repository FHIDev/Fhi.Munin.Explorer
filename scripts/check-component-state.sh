#!/usr/bin/env bash
#
# Drives the component in a real browser and asks whether the DOM and the component still agree
# after a press the component REFUSED. Nothing else here does: bUnit renders a render tree, so the
# browser's own flip of a checkbox — which happens before any handler runs — never happens in it.
#
# WHY THIS EXISTS. The column picker and the facet panel both call
# `builder.SetUpdatesAttributeName("checked")`, and it is there for one reason: a render that equals
# the render before it writes nothing back to the DOM, so a press the component declines leaves the
# browser's tick standing over a column that is still drawn, or a filter that is off. Removing
# either call leaves every test in test/ green — measured rather than assumed, once per call site —
# because the disagreement is invisible to bUnit. This run fails on either, and each removal fails
# only its own assertion. (Fhi.Metadata-1s7z1)
#
# WHAT IT DOES NOT SEE, so nobody reads a green run as more than it is:
#   - the two presses it stages are the whole of it. scripts/state-assertions.mjs lists what that
#     leaves out — the kildeutforsker's copy of the same picker, the facet panel's other refusal
#     path, and every other control in the component;
#   - one press per call site. The picker's other columns and the panel's other facets go the same
#     way by construction, but by construction is not measured;
#   - the sample stylesheet, not helsedata's. This runs ModernHost, as check-accessibility.sh does,
#     because a control's own state is not a question about CSS. What a rule of Stiler's could do to
#     the same markup is scripts/check-hostile-host.sh's business;
#   - whether the refusal is the RIGHT rule. It asks the picker and the header to say the same
#     thing, not whether the last column should be the one that cannot be turned off.
#
# Usage:  ./scripts/check-component-state.sh
# Needs:  dotnet, node (for npx), and a Chrome/Chromium on PATH. No feed credentials: a control's
#         own state does not depend on a stylesheet. PLAYWRIGHT_BROWSER_CHANNEL=msedge runs an
#         installed browser where `playwright install chromium` cannot complete, which on Node 26 is
#         always (Fhi.Metadata-2nfvm); the run says which it used and CI sets neither.

set -euo pipefail

# Pinned, for the reason check-accessibility.sh pins it: an unpinned npx resolves to whatever is
# newest on the day, which turns an unrelated PR red for a reason nobody changed.
PLAYWRIGHT_VERSION="1.49.1"

PORT="${STATE_PORT:-5095}"
BASE="http://localhost:${PORT}"
STUB_PORT="${STATE_STUB_PORT:-5094}"
STUB_BASE="http://127.0.0.1:${STUB_PORT}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# `/` is VariableSearch on its own, which is where both presses live: the column picker above the
# results and the facet panel beside them. `variables-list` is the state that waits for a row, so
# neither assertion stages a press against a list whose data never arrived.
TARGETS=(
  "/::variables-list"
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
  # `dotnet run` is a launcher: killing it leaves the app it started holding the port, and the next
  # run then refuses to start against an orphan it cannot see. Both forms, because neither is enough
  # on its own - Git Bash's pkill does not match Windows process command lines. Scoped to the URL
  # rather than to the project name, unlike check-hostile-host.sh: ModernHost is the everyday
  # development host, so a bare `taskkill //IM` would take down a second worktree's run as well.
  pkill -f "ModernHost.*${PORT}" 2>/dev/null || true
  if command -v powershell >/dev/null 2>&1; then
    powershell -NoProfile -Command "Get-CimInstance Win32_Process |
      Where-Object { \$_.CommandLine -like '*ModernHost*:${PORT}*' } |
      ForEach-Object { Stop-Process -Id \$_.ProcessId -Force }" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

# Anything already answering on these ports is driven in place of what this run starts — an orphan
# from a previous run is the usual case, and a green run that belongs to someone else's page is the
# result. Worse here than in the sibling scans: this one ARMS a failure at the stub, so a stub that
# is not ours would be handed one and never spend it.
for occupied in "$BASE/" "${STUB_BASE}/api/explorer/kilder"; do
  if curl -fsS -o /dev/null --max-time 2 "$occupied" 2>/dev/null; then
    echo "something is already listening on ${occupied} - TOOLING failure." >&2
    echo "stop it, or set STATE_PORT / STATE_STUB_PORT to free ports." >&2
    exit 2
  fi
done

echo "==> starting the stub API on ${STUB_BASE}"
node "$ROOT/scripts/axe-stub-api.mjs" "$STUB_PORT" >/tmp/state-stub.log 2>&1 &
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

if ! curl -fsS -o /dev/null --max-time 5 "${STUB_BASE}/api/explorer/kilder" 2>/dev/null; then
  echo "the stub API never answered on ${STUB_BASE} - TOOLING failure." >&2
  tail -10 /tmp/state-stub.log >&2
  exit 2
fi

echo "==> starting ModernHost on ${BASE}"
(
  cd "$ROOT"
  MuninExplorer__ApiBaseUrl="$STUB_BASE" dotnet run --project samples/ModernHost --urls "$BASE" >/tmp/state-host.log 2>&1
) &
host_pid=$!

echo "==> waiting for the host"
for _ in $(seq 1 60); do
  if curl -fsS -o /dev/null --max-time 2 "$BASE/" 2>/dev/null; then
    break
  fi
  if ! kill -0 "$host_pid" 2>/dev/null; then
    echo "the host exited before it answered - TOOLING failure." >&2
    tail -30 /tmp/state-host.log >&2
    exit 2
  fi
  sleep 2
done

if ! curl -fsS -o /dev/null --max-time 5 "$BASE/" 2>/dev/null; then
  echo "the host never answered on ${BASE} - TOOLING failure." >&2
  tail -30 /tmp/state-host.log >&2
  exit 2
fi

SETTLE_MS="${ACCESSIBILITY_SETTLE_MS:-4000}"

echo "==> installing the driver"
npm install --no-save --silent "playwright@${PLAYWRIGHT_VERSION}" >/tmp/state-npm-install.log 2>&1 || {
  echo "could not install the driver - TOOLING failure." >&2
  tail -10 /tmp/state-npm-install.log >&2
  exit 2
}

# Skipped when a channel is set: that browser is already installed, and on Node 26 this step cannot
# succeed at all - the pinned fetcher calls fs.rmdir(recursive), removed in that version, which
# leaves a half-written cache with a chrome.dll and no chrome.exe (Fhi.Metadata-2nfvm).
if [ -z "${PLAYWRIGHT_BROWSER_CHANNEL:-}" ]; then
  npx --yes playwright install chromium >/tmp/state-pw-install.log 2>&1 || {
    echo "could not install chromium - TOOLING failure." >&2
    echo "on Node 26 try PLAYWRIGHT_BROWSER_CHANNEL=msedge to use an installed browser." >&2
    tail -10 /tmp/state-pw-install.log >&2
    exit 2
  }
else
  echo "==> BROWSER: ${PLAYWRIGHT_BROWSER_CHANNEL} (not the bundled chromium)"
fi

urls=()
for t in "${TARGETS[@]}"; do urls+=("${BASE}${t}"); done

set +e
ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" STATE_STUB_BASE="$STUB_BASE" \
  node "$ROOT/scripts/state-scan.mjs" "${urls[@]}"
scan_status=$?
set -e

# 2 is the scanner saying it could not run, which includes an assertion that held against the very
# defect it exists for. Passing it through unchanged keeps a harness that has stopped measuring from
# reading as a component that is fine.
if [ "$scan_status" -eq 2 ]; then
  exit 2
fi

echo
if [ "$scan_status" -ne 0 ]; then
  cat >&2 <<'EOF'
The DOM and the component disagree after a press the component refused.

Each failure above names the control, what the browser says about it and what the component drew.
The usual cause is a missing builder.SetUpdatesAttributeName("checked") beside the checkbox's
onchange: without it a render equal to the render before it writes nothing back, so the browser's
own flip is left standing.
EOF
  exit 1
fi

cat <<'EOF'
A refused press left the DOM and the component agreeing, and each assertion still fires against
the defect it exists for.

Read that for what it is. Two presses were staged, in the variable explorer's column picker and
its facet panel, against the sample stylesheet. The header of this script and of
scripts/state-assertions.mjs list what that leaves out.
EOF
