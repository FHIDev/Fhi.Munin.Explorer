#!/usr/bin/env bash
#
# Renders the component inside samples/HostileHost — helsedata's real stylesheet, helsedata's
# header positioned over the top of document flow — and measures it. Geometry first, then axe on
# the same page, because the two see different things and only one of them was here before.
#
# WHY THIS EXISTS. On 2026-09-03 four layout defects reached a branch having passed 1317 unit tests
# and eight axe states. All four were found by a human looking at the component inside helsedata,
# and all four were invisible to everything we run because the sample hosts render the package on a
# bare page: no author stylesheet whose element rules beat the browser's defaults, and no header
# over the content. Two of the four were collisions with rules only Stiler has. A synthesised
# stylesheet would have caught neither. (Fhi.Metadata-l9l2n.40)
#
# WHAT IT DOES NOT SEE, so nobody reads a green run as more than it is:
#   - anything below the fold that only misbehaves once scrolled; every assertion measures at
#     scroll offset 0, which is where the absolute header overlaps;
#   - the kildeutforsker and the search-only mount, neither of which this host renders;
#   - widths other than the three in GEOMETRY_WIDTHS, and any height at all — nothing here asks
#     about vertical layout;
#   - whether it LOOKS right. Boxes in the right places can still be the wrong design.
#
# Usage:  ./scripts/check-hostile-host.sh
# Needs:  dotnet, node (for npx), a Chrome/Chromium on PATH — and credentials for helsedata's
#         Azure Artifacts feed, because HostileHost has a PackageReference to
#         Fhi.Helsedata.Stiler. Locally that is the Azure Artifacts Credential Provider; in CI it
#         is VSS_NUGET_EXTERNAL_FEED_ENDPOINTS from a repository secret. See nuget.config.

set -euo pipefail

# Pinned, for the reason check-accessibility.sh pins them: an unpinned npx resolves to whatever is
# newest on the day, which turns an unrelated PR red for a reason nobody changed.
PLAYWRIGHT_VERSION="1.49.1"
AXE_PLAYWRIGHT_VERSION="4.10.1"

PORT="${HOSTILE_PORT:-5097}"
BASE="http://localhost:${PORT}"
STUB_PORT="${HOSTILE_STUB_PORT:-5096}"
STUB_BASE="http://127.0.0.1:${STUB_PORT}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# HostileHost serves one page, and both states are on it. `explorer-tabs` is the search results as
# they load; `explorer-list-tab` is the second tab open, which is the state defect 2 was found in
# and the only one where a panel is asked to be hidden at all.
TARGETS=(
  "/::explorer-tabs"
  "/::explorer-list-tab"
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
  # `dotnet run` is a launcher: killing it leaves the app it started holding the port, and the
  # next run then refuses to start against an orphan it cannot see. On a CI runner the job ends
  # and the point is moot; locally this is the difference between a script you can run twice and
  # one you cannot. Both forms, because neither is enough on its own — Git Bash's pkill does not
  # match Windows process command lines, and taskkill does not exist on the runner.
  pkill -f 'HostileHost' 2>/dev/null || true
  if command -v taskkill >/dev/null 2>&1; then
    taskkill //F //IM HostileHost.exe >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

# Anything already answering on these ports is measured in place of what this run starts,
# stylesheet and all — an orphan from a previous run is the usual case, and a green run that
# belongs to someone else's page is the result.
for occupied in "$BASE/" "${STUB_BASE}/api/explorer/kilder"; do
  if curl -fsS -o /dev/null --max-time 2 "$occupied" 2>/dev/null; then
    echo "something is already listening on ${occupied} - TOOLING failure." >&2
    echo "stop it, or set HOSTILE_PORT / HOSTILE_STUB_PORT to free ports." >&2
    exit 2
  fi
done

echo "==> starting the stub API on ${STUB_BASE}"
node "$ROOT/scripts/axe-stub-api.mjs" "$STUB_PORT" >/tmp/hostile-stub.log 2>&1 &
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
  tail -10 /tmp/hostile-stub.log >&2
  exit 2
fi

echo "==> starting HostileHost on ${BASE}"
(
  cd "$ROOT"
  MuninExplorer__ApiBaseUrl="$STUB_BASE" dotnet run --project samples/HostileHost --urls "$BASE" >/tmp/hostile-host.log 2>&1
) &
host_pid=$!

echo "==> waiting for the host"
for _ in $(seq 1 90); do
  if curl -fsS -o /dev/null --max-time 2 "$BASE/" 2>/dev/null; then
    break
  fi
  if ! kill -0 "$host_pid" 2>/dev/null; then
    echo "the host exited before it answered:" >&2
    tail -30 /tmp/hostile-host.log >&2
    exit 1
  fi
  sleep 2
done

if ! curl -fsS -o /dev/null --max-time 5 "$BASE/" 2>/dev/null; then
  echo "the host never answered on ${BASE}" >&2
  tail -30 /tmp/hostile-host.log >&2
  exit 1
fi

# The whole point of this host is that the stylesheet is helsedata's own, served out of the
# package's static web assets. If it 404s the page renders at browser defaults, every assertion
# holds, and the run is a green that means nothing. Checked here rather than assumed: a
# PackageReference that restored is not the same as an asset that is being served.
echo "==> checking Stiler is actually being served"
STILER="${BASE}/_content/Fhi.Helsedata.Stiler/css/main.css"
# Downloaded to a file rather than piped into grep. `grep -q` stops at the first match and closes
# the pipe, curl takes a SIGPIPE 350 kB from the end, and pipefail then reports the whole thing as
# a failure — a stylesheet that IS being served correctly, called missing.
stiler_copy="$(mktemp)"
if ! curl -fsS --max-time 10 -o "$stiler_copy" "$STILER" 2>/dev/null ||
   ! grep -q 'grid-template-columns: 384px' "$stiler_copy"; then
  rm -f "$stiler_copy"
  echo "the Stiler stylesheet is missing or is not the real one - TOOLING failure." >&2
  echo "expected ${STILER} to carry .munin-explorer's 384px grid." >&2
  exit 2
fi
rm -f "$stiler_copy"

SETTLE_MS="${ACCESSIBILITY_SETTLE_MS:-4000}"

echo "==> installing the scanner"
npm install --no-save --silent \
    "playwright@${PLAYWRIGHT_VERSION}" \
    "@axe-core/playwright@${AXE_PLAYWRIGHT_VERSION}" >/tmp/hostile-npm-install.log 2>&1 || {
  echo "could not install the scanner - TOOLING failure." >&2
  tail -10 /tmp/hostile-npm-install.log >&2
  exit 2
}

npx --yes playwright install chromium >/tmp/hostile-pw-install.log 2>&1 || {
  echo "could not install chromium - TOOLING failure." >&2
  tail -10 /tmp/hostile-pw-install.log >&2
  exit 2
}

urls=()
for t in "${TARGETS[@]}"; do urls+=("${BASE}${t}"); done

set +e
ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" node "$ROOT/scripts/geometry-scan.mjs" "${urls[@]}"
geometry_status=$?
set -e

[ "$geometry_status" -eq 2 ] && exit 2

# axe on the same page, and it is not a duplicate of the accessibility job: that one scans
# ModernHost, where the cascade is the sample stylesheet's. A contrast or focus rule can hold
# there and fail here, because here the colours are helsedata's.
# 1440, not axe-scan's usual 1280: Stiler's own `@media (max-width: 1280px)` collapses every result
# row to zero height (Fhi.Metadata-l9l2n.41), so at the default viewport the states cannot be
# entered at all and the scan stops before it judges anything. Drop this line the day that lands.
set +e
ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" AXE_VIEWPORT_WIDTH=1440 AXE_VIEWPORT_HEIGHT=900   node "$ROOT/scripts/axe-scan.mjs" "${urls[@]}"
axe_status=$?
set -e

[ "$axe_status" -eq 2 ] && exit 2

echo
if [ "$geometry_status" -ne 0 ] || [ "$axe_status" -ne 0 ]; then
  cat >&2 <<'EOF'
The component does not render correctly inside helsedata's stylesheet and chrome.

Each failure above names what was measured and what was expected. A geometry failure marked
[invariant] is a property of any correct rendering and is very unlikely to be a false positive;
one marked [pin] is a replay of a defect from 2026-09-03 and says that defect is back.

scripts/geometry-assertions.mjs says which is which and why.
EOF
  exit 1
fi

cat <<'EOF'
Every geometry assertion held and axe found no violations, against helsedata's real stylesheet.

Read that for what it is. It says the boxes are where they should be at three widths and at
scroll offset 0; it does not say the page looks right, and it says nothing at all about the two
components this host does not mount. The header of this script lists the rest.
EOF
