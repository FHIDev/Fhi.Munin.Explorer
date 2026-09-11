#!/usr/bin/env bash
#
# Runs axe against the ModernHost sample and fails on any violation, then measures one page at
# 320px - the width WCAG 1.4.10 Reflow names, which no other gate here visits. A green run means no
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
#   - the pager past page one, and anything reached by searching; the kildeutforsker's own list
#     narrowed by a facet IS covered, in kilde-facets, and the variable explorer's is not — so
#     neither its chip row nor the hierarchy trail over its results is ever on screen for axe,
#     and a green run says nothing about either (Fhi.Metadata-oj286);
#   - error and empty states, which need the stub to answer differently than it does;
#   - hierarchy branches beyond the two opened levels in kilde-hierarchy-expanded;
#   - the English texts, and samples/LegacyHost, the same component in the other host;
#   - the list tab's own create, rename and delete forms, and the annotation field in a row.
# Each is another page load and settle, about ten seconds, and none carries the risk the
# targets below do. Add one here and in axe-states.mjs when that stops being true.
#
# /utforsker is the only page in either sample that mounts the composed VariableExplorer, so it is
# the only place the page-level tablist and the reader's list panel are scanned at all.
#
# One of them is visited twice: REFLOW_TARGET is scanned by axe with the rest, and measured again
# at 320px at the end of this script. Named once, so a rename in axe-states.mjs lands in one place.
REFLOW_TARGET="/kilder::kilder-list"

# Measured at 320px but NOT scanned by axe: the ribbon's widest state is a geometry question, and
# the boxes are what a nowrap handover breaks. Its own variable so the run below reads as two
# states of one page rather than a list. (Fhi.Metadata-kvgu7)
REFLOW_TICKED_TARGET="/kilder::kilder-ticked"
TARGETS=(
  "/::variables-list"
  "$REFLOW_TARGET"
  "/::filters-level-lines"
  "/::variable-detail"
  "/kilder::kilde-drilldown"
  "/kilder::kilde-hierarchy-collapsed"
  "/kilder::kilde-hierarchy-expanded"
  "/kilder::kilde-hierarchy-open"
  "/kilder::kilde-datasamling"
  "/kilder::kilde-hierarchy-metadata"
  "/kilder::kilder-expanded"
  "/kilder::kilder-columns"
  "/kilder::kilde-facets"
  "/utforsker::explorer-tabs"
  "/utforsker::explorer-list-tab"
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

# Skipped when a channel is set: that browser is already installed, and on Node 26 this step
# cannot succeed at all - the pinned fetcher calls fs.rmdir(recursive), removed in that version,
# which leaves a half-written cache with a chrome.dll and no chrome.exe (Fhi.Metadata-wgwa0).
if [ -z "${PLAYWRIGHT_BROWSER_CHANNEL:-}" ]; then
  npx --yes playwright install chromium >/tmp/pw-install.log 2>&1 || {
    echo "could not install chromium - TOOLING failure." >&2
    echo "on Node 26 try PLAYWRIGHT_BROWSER_CHANNEL=msedge to use an installed browser." >&2
    tail -10 /tmp/pw-install.log >&2
    exit 2
  }
else
  echo "==> BROWSER: ${PLAYWRIGHT_BROWSER_CHANNEL} (not the bundled chromium)"
fi

set +e
node "$ROOT/scripts/axe-scan.mjs" $(for t in "${TARGETS[@]}"; do printf '%s ' "${BASE}${t}"; done)
scan_status=$?
set -e

# 2 is the scanner saying it could not run. Passing that through unchanged keeps a
# broken toolchain from reading as a broken page.
if [ "$scan_status" -eq 2 ]; then
  exit 2
fi

# WCAG 1.4.10 Reflow is stated at 320px and nothing here measured any page there: geometry-scan.mjs
# drives six widths and the narrowest is 843. One page, the kildeutforsker, in two states, both
# waiting for a row so an empty page fails as TOOLING rather than fitting 320 with nothing in it.
# The second state ticks one: the ribbon is widest there, and the untouched page fits 320 whether
# or not the handover can wrap.
#
# Three of the ten assertions. Four of the seven left out were measured here first; the other
# three are scoped to the explorer-* states and cannot be measured on this one. Which and why:
# AGENTS.md, "And check-accessibility.sh measures one width axe never looks at".
echo
echo "==> measuring the reflow width WCAG 1.4.10 names"
set +e
GEOMETRY_WIDTHS=320 \
GEOMETRY_ASSERTIONS='no horizontal overflow,hidden means hidden,text a reader is meant to see has a box to see it in' \
  node "$ROOT/scripts/geometry-scan.mjs" "${BASE}${REFLOW_TARGET}" "${BASE}${REFLOW_TICKED_TARGET}"
reflow_status=$?
set -e

# Anything non-zero is a finding, except the one code that means the step never ran.
findings=0
[ "$scan_status" -ne 0 ] && findings=1
if [ "$reflow_status" -ne 0 ] && [ "$reflow_status" -ne 2 ]; then
  findings=1
fi

echo
if [ "$findings" -ne 0 ]; then
  cat >&2 <<'EOF'
Accessibility violations found. See the output above; an axe entry names the rule, the
element and a link to the fix, and a geometry failure names what was measured against what
was expected.

A failure at 320px is WCAG 1.4.10 Reflow: the page scrolls sideways at the width the
success criterion names, and no reader on a phone can get to what is off the edge.

Before you reach for a suppression: this gate is deliberately narrow, so a violation it
DID catch is very unlikely to be a false positive.
EOF
fi

# 2 from the geometry step is the scanner saying it could not run. Said after the guidance above
# rather than in place of it, because axe has already decided by this point and exiting 2 over a
# violation it found would report a page defect as a broken toolchain.
if [ "$reflow_status" -eq 2 ]; then
  echo "the 320px measurement could not run - TOOLING failure, so nothing was measured there." >&2

  if [ "$findings" -eq 0 ]; then
    exit 2
  fi
fi

if [ "$findings" -ne 0 ]; then
  exit 1
fi

cat <<'EOF'
No violations detected, and the kildeutforsker fits 320px.

Read that literally. This gate sees the sample stylesheet, not the one the component
ships into, and automated checking cannot see missing structure at all. A green run is
evidence of no detected regression, and nothing more.

The 320px measurement is narrower still: three of the ten assertions, on one page, in
two states. Every other width and every other assertion belongs to check-hostile-host.sh.

Why, at length: AGENTS.md, "Accessibility is a requirement, not a preference".
EOF
