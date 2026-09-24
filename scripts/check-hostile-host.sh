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
#   - the search-only mount, which this host does not render. The kildeutforsker IS measured, on
#     /kilder, as of Fhi.Metadata-fih3y;
#   - widths other than the six in GEOMETRY_WIDTHS and 320, and any height at all — nothing here
#     asks about vertical layout. At 320 some assertions are left out in some states, by name;
#   - whether it LOOKS right. Boxes in the right places can still be the wrong design.
#
# Exits: 0 measured and correct; 1 measured and wrong; 3 not measured, because a geometry assertion
#        stopped firing against the defect it exists for; 2 could not measure at all (TOOLING).
#        Callers that report unattended runs need 1 and 3 apart — see devbox drift.yaml.
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
HOST_PROJECT="$ROOT/samples/HostileHost/HostileHost.csproj"

# `explorer-tabs` is the search results as they load; `explorer-list-tab` is the second tab open,
# which is the state defect 2 was found in and the only one where a panel is asked to be hidden at
# all. Both are on the front page.
#
# `/kilder::kilder-list` is the kildeutforsker, and it is the page most worth measuring: the kilder
# table is the widest thing this package draws and the only part whose overflow lands on the HOST's
# page. Three fixture problems had to be solved before it could go in - the Stiler pin, a
# deliberate host un-hide, and pins that need a tablist - and the widths and numbers are recorded
# on Fhi.Metadata-fih3y.
#
# `/kilder::kilder-counts` is the same table with Delkilder turned on, and it is here for one
# assertion: Delkilder starts hidden, so the count column whose alignment nothing else in either
# repository measures is also the one `kilder-list` never draws (Fhi.Metadata-y7ilr).
#
# `/kilder::kilde-facets` is here rather than only in check-accessibility.sh because the fold it
# stages is the browser's own and an author rule can beat it: helsedata's bare `div { display:
# block }` is what left a folded panel on screen at 3798px, and ModernHost's stylesheet cannot
# reproduce that. The state asserts a folded facet's values are off screen before it opens one, so
# this is the run where that assertion means anything (Fhi.Metadata-co3sf).
#
# `variable-detail-about`, `variable-whole` and `explorer-search-code` are where the stub's long code
# is drawn, and a code is one unbroken word: what decides the reflow width (Fhi.Metadata-ofg1h).
# `variable-detail` stays for the Data tab, which opens first (Fhi.Metadata-l9l2n.101).
# `variable-statistics` and `variable-statistics-suppressed` are that tab with numbers in it, which
# no captured variable has: the bars, and a sixty-character label beside its count (Fhi.Metadata-9mxmw).
#
# `filters-level-lines` and `filters-node-icons-off` unfold the facets, whose values include a
# variabelgruppe name with no break in it; only the label rule wraps it (Fhi.Metadata-7484a).
#
# `kilde-drilldown`, `kilde-datasamling` and `variable-page` are the three detail pages whose fact
# lists count their tracks against the container; `variable-page` presses a row's name, because a
# `?variabelId=` load draws the drill-in's grid instead and measures nothing (Fhi.Metadata-2w7fx).
TARGETS=(
  "/::explorer-tabs"
  "/::explorer-list-tab"
  "/::variable-detail"
  "/::variable-detail-about"
  "/::variable-detail-long-name"
  "/::variable-statistics"
  "/::variable-statistics-suppressed"
  "/::variable-whole"
  "/::explorer-search-code"
  "/::tree-collapsed"
  "/::tree-populated"
  "/::tree-empty-results"
  "/::tree-no-match"
  "/::filters-level-lines"
  "/::filters-node-icons-off"
  "/kilder::kilder-list"
  "/kilder::kilder-counts"
  "/kilder::kilde-hierarchy-collapsed"
  "/kilder::kilde-hierarchy-expanded"
  "/kilder::kilde-hierarchy-metadata"
  # hierarchy-fixture.mjs: shapes hierarchy.json lacks, its empty and failing answers, and the variable
  # explorer's drill-in with Ikoner on and off, for Fhi.Metadata-zkl14.
  "/kilder?kilde=dddddddd-0000-4000-8000-000000000001::kilde-hierarchy-deep"
  "/kilder?kilde=dddddddd-0000-4000-8000-000000000002::kilde-hierarchy-empty"
  "/kilder?kilde=dddddddd-0000-4000-8000-000000000003::kilde-hierarchy-error"
  "/::variable-kilde-hierarchy"
  "/::variable-kilde-hierarchy-icons-off"
  "/kilder::kilde-facets"
  "/kilder::kilde-drilldown"
  "/kilder::kilde-datasamling"
  "/::variable-page"
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

# STILER_FROM_SOURCE=1 swaps the pinned package for the Stiler checkout beside this repository,
# which is the only way this port runs on a machine without the Azure Artifacts Credential
# Provider. What it then measures is Stiler MAIN, not the pinned package, so the
# run says which of the two it used and CI never sets it (Fhi.Metadata-wgwa0).
STILER_ARGS=()
if [ "${STILER_FROM_SOURCE:-0}" = "1" ]; then
  STILER_REPO="${STILER_REPO_PATH:-$ROOT/../Fhi.Helsedata.Stiler}"
  if [ ! -f "$STILER_REPO/Fhi.Helsedata.Stiler.csproj" ]; then
    echo "STILER_FROM_SOURCE=1 but no Stiler checkout at $STILER_REPO - TOOLING failure." >&2
    exit 2
  fi
  if [ ! -f "$STILER_REPO/wwwroot/css/main.css" ]; then
    echo "==> building Stiler from source in $STILER_REPO"
    (cd "$STILER_REPO" && npm install --silent && npm run build) || {
      echo "Stiler would not build - TOOLING failure." >&2; exit 2; }
  fi
  STILER_ARGS=(-p:UseLocalStiler=true "-p:StilerRepoPath=$STILER_REPO")
  echo "==> STILER: built from source at $STILER_REPO (main, NOT the pinned package)"
else
  echo "==> STILER: the pinned Fhi.Helsedata.Stiler package, as helsedata restore it"
fi

# Built here and started below, rather than `dotnet run`: that is a launcher which starts the app
# as a SEPARATE child, so nothing $! can name is the server, and the trap killed a shell while
# HostileHost kept the port. Started this way the host is one process, as the stub already was.
echo "==> building HostileHost"
if ! dotnet build "$HOST_PROJECT" "${STILER_ARGS[@]}" --nologo -v quiet >/tmp/hostile-build.log 2>&1; then
  echo "HostileHost would not build - TOOLING failure." >&2
  tail -20 /tmp/hostile-build.log >&2
  exit 2
fi

# set +e, because a plain assignment carries the substitution's status: `set -e` would abort here
# on an msbuild that failed, before the message below - and with its stderr on the build log rather
# than the screen, that abort says nothing at all.
set +e
HOST_DLL="$(dotnet msbuild "$HOST_PROJECT" "${STILER_ARGS[@]}" -getProperty:TargetPath -nologo 2>>/tmp/hostile-build.log | tr -d '\r')"
located=$?
set -e
if [ "$located" -ne 0 ] || [ -z "$HOST_DLL" ] || [ ! -f "$HOST_DLL" ]; then
  echo "could not find what the HostileHost build produced - TOOLING failure." >&2
  tail -20 /tmp/hostile-build.log >&2
  exit 2
fi

echo "==> starting HostileHost on ${BASE}"
# The environment and the content root `dotnet run` would have supplied, from launchSettings.json
# and the project directory. Nothing reads either when the build output is started directly, and
# Stiler's stylesheet is served as a Development static web asset.
MuninExplorer__ApiBaseUrl="$STUB_BASE" ASPNETCORE_ENVIRONMENT=Development \
  dotnet "$HOST_DLL" --urls "$BASE" --contentRoot "$ROOT/samples/HostileHost" \
  >/tmp/hostile-host.log 2>&1 &
host_pid=$!

echo "==> waiting for the host"
for _ in $(seq 1 90); do
  if curl -fsS -o /dev/null --max-time 2 "$BASE/" 2>/dev/null; then
    break
  fi
  if ! kill -0 "$host_pid" 2>/dev/null; then
    # 2, not 1: the host never came up, so nothing was measured. Reporting this as 1 tells an
    # unattended caller a layout defect was found, which is the confusion this exit code exists
    # to remove.
    echo "the host exited before it answered - TOOLING failure." >&2
    tail -30 /tmp/hostile-host.log >&2
    exit 2
  fi
  sleep 2
done

if ! curl -fsS -o /dev/null --max-time 5 "$BASE/" 2>/dev/null; then
  # Same reason as the loop above: unreachable is not wrong, it is unmeasured.
  echo "the host never answered on ${BASE} - TOOLING failure." >&2
  tail -30 /tmp/hostile-host.log >&2
  exit 2
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
npm install --prefix "$ROOT" --no-save --silent \
    "playwright@${PLAYWRIGHT_VERSION}" \
    "@axe-core/playwright@${AXE_PLAYWRIGHT_VERSION}" >/tmp/hostile-npm-install.log 2>&1 || {
  echo "could not install the scanner - TOOLING failure." >&2
  tail -10 /tmp/hostile-npm-install.log >&2
  exit 2
}

# Skipped when a channel is set: that browser is already installed, and on Node 26 this step
# cannot succeed at all - the pinned fetcher calls fs.rmdir(recursive), removed in that version,
# which leaves a half-written cache with a chrome.dll and no chrome.exe (Fhi.Metadata-wgwa0).
if [ -z "${PLAYWRIGHT_BROWSER_CHANNEL:-}" ]; then
  npx --prefix "$ROOT" --yes playwright install chromium >/tmp/hostile-pw-install.log 2>&1 || {
    echo "could not install chromium - TOOLING failure." >&2
    echo "on Node 26 try PLAYWRIGHT_BROWSER_CHANNEL=msedge to use an installed browser." >&2
    tail -10 /tmp/hostile-pw-install.log >&2
    exit 2
  }
  # A Linux box with no root and no browser libraries, the Forge's, gets them unpacked into a prefix.
  . "$ROOT/scripts/chromium-deps.sh"
  chromium_deps_ensure || { echo "could not provide chromium's libraries - TOOLING failure." >&2; exit 2; }
else
  echo "==> BROWSER: ${PLAYWRIGHT_BROWSER_CHANNEL} (not the bundled chromium)"
fi

urls=()
for t in "${TARGETS[@]}"; do urls+=("${BASE}${t}"); done

# Measured for geometry but not scanned by axe, each with the open bead that keeps it out. The
# datasamling page's stuck bar is `hidden` and `aria-hidden`, but Stiler's `div { display: block }`
# draws it with a focusable link inside: aria-hidden-focus (Fhi.Metadata-5fuvd).
AXE_EXCEPT_TARGETS=("/kilder::kilde-datasamling")
axe_urls=()
for t in "${TARGETS[@]}"; do
  [[ " ${AXE_EXCEPT_TARGETS[*]} " == *" ${t} "* ]] && continue
  axe_urls+=("${BASE}${t}")
done

set +e
GEOMETRY_EXCEPT= ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" node "$ROOT/scripts/geometry-scan.mjs" "${urls[@]}"
geometry_status=$?
set -e

[ "$geometry_status" -eq 2 ] && exit 2

# 320px, WCAG 1.4.10 Reflow. Each call leaves out only what fails in its states today, with the bead
# that says why; an open bead's last step is deleting its call's exception.
reflow_status=0
reflow() {
  local except="$1"; shift
  local urls=() t status
  for t in "$@"; do urls+=("${BASE}${t}"); done
  set +e
  GEOMETRY_ASSERTIONS= GEOMETRY_WIDTHS=320 GEOMETRY_EXCEPT="$except" \
    ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" \
    node "$ROOT/scripts/geometry-scan.mjs" "${urls[@]}"
  status=$?
  set -e
  [ "$status" -eq 2 ] && exit 2
  [ "$status" -ne 0 ] && reflow_status=1
  return 0
}

echo
echo "==> measuring the reflow width WCAG 1.4.10 names"
reflow "" "/::explorer-tabs" "/::explorer-list-tab"
# The closed column picker hangs 2px off the left edge; Fhi.Metadata-abmom records why that stays.
reflow "the component stays inside the box the host gave it" \
  "/kilder::kilder-list" "/kilder::kilder-counts" "/kilder::kilde-facets"
# Fhi.Metadata-s9h1k's exception went with Stiler 0.1.79, which breaks a long value rather than
# widening its column; samples/HostileHost pins a release at or after it.
reflow "" \
  "/kilder::kilde-hierarchy-collapsed" "/kilder::kilde-hierarchy-expanded" "/kilder::kilde-hierarchy-metadata"
reflow "" \
  "/kilder?kilde=dddddddd-0000-4000-8000-000000000001::kilde-hierarchy-deep" \
  "/kilder?kilde=dddddddd-0000-4000-8000-000000000002::kilde-hierarchy-empty" \
  "/kilder?kilde=dddddddd-0000-4000-8000-000000000003::kilde-hierarchy-error"
reflow "" "/::variable-kilde-hierarchy" "/::variable-kilde-hierarchy-icons-off"
# The states the stub's long code reaches: the row panel's Kode, the whole-variable page's heading
# and fact row, and the result count that quotes a searched term (Fhi.Metadata-ofg1h).
reflow "" "/::variable-detail" "/::variable-detail-about" "/::variable-whole" "/::explorer-search-code"
# The drawer heading's unbroken name (Fhi.Metadata-yaco2).
reflow "" "/::variable-detail-long-name"
# The Data tab's coverage bar and frequency list, and the long label wrapping in it (Fhi.Metadata-9mxmw).
reflow "" "/::variable-statistics" "/::variable-statistics-suppressed"
reflow "" "/::tree-collapsed" "/::tree-populated" "/::tree-empty-results" "/::tree-no-match"
# The facets unfolded, and the unbroken variabelgruppe name in them (Fhi.Metadata-7484a).
reflow "" "/::filters-level-lines" "/::filters-node-icons-off"
# The three detail pages' fact lists, one track at 320 (Fhi.Metadata-2w7fx).
reflow "" "/kilder::kilde-drilldown" "/kilder::kilde-datasamling" "/::variable-page"

# An assertion that has quietly stopped measuring anything reports success forever, so each one is
# handed a page carrying the defect it was written for and required to say so.
set +e
ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" node "$ROOT/scripts/geometry-negative-control.mjs" "$BASE"
control_status=$?
set -e

[ "$control_status" -eq 2 ] && exit 2

# Real Tab presses, read off document.activeElement: a stop inside a [hidden] subtree, or with no
# box, is one a keyboard reader lands on and cannot see. Only this host has the `div { display:
# block }` that makes a hidden panel focusable at all (Fhi.Metadata-w8sms).
echo
echo "==> walking the Tab order"
set +e
ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" node "$ROOT/scripts/tab-stop-scan.mjs" \
  "${BASE}/::explorer-tabs" "${BASE}/::explorer-list-tab" \
  "${BASE}/::variable-detail" "${BASE}/::variable-detail-about"
tab_stop_status=$?
set -e

[ "$tab_stop_status" -eq 2 ] && exit 2

# axe on the same page, and it is not a duplicate of the accessibility job: that one scans
# ModernHost, where the cascade is the sample stylesheet's. A contrast or focus rule can hold
# there and fail here, because here the colours are helsedata's.
# 1440, not axe-scan's usual 1280: Stiler's own `@media (max-width: 1280px)` collapses every result
# row to zero height (Fhi.Metadata-l9l2n.41), so at the default viewport the states cannot be
# entered at all and the scan stops before it judges anything. Drop this line the day that lands.
set +e
ACCESSIBILITY_SETTLE_MS="$SETTLE_MS" AXE_VIEWPORT_WIDTH=1440 AXE_VIEWPORT_HEIGHT=900   node "$ROOT/scripts/axe-scan.mjs" "${axe_urls[@]}"
axe_status=$?
set -e

[ "$axe_status" -eq 2 ] && exit 2

echo
if [ "$control_status" -ne 0 ]; then
  cat >&2 <<'EOF'
A geometry assertion did not fire against the defect it exists for.

Read the geometry result above as unmeasured, whichever way it went: an assertion that holds
against a page carrying its own defect is not passing, it is absent.
EOF
  # 3, NOT 1, AND IT IS A CONTRACT, not a tidier number. 1 says the component renders wrong; this
  # says nobody knows how it renders, which is a different thing to be told at 04:00. The nightly
  # sidecar files the two under different titles off this code (Fhi.Metadata-pvwzl); 2 is taken by
  # the TOOLING failures above.
  exit 3
fi

# Same contract as the control above: the walk missed the stop it planted, so nothing was measured.
if [ "$tab_stop_status" -eq 3 ]; then
  echo "The Tab walk never landed on the hidden stop it plants; read its result above as unmeasured." >&2
  exit 3
fi

if [ "$geometry_status" -ne 0 ] || [ "$reflow_status" -ne 0 ] || [ "$axe_status" -ne 0 ] || [ "$tab_stop_status" -ne 0 ]; then
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
Every geometry assertion that applies held, each one still fires against the defect it exists
for, and axe found no violations - against helsedata's real stylesheet.

Read that for what it is. It says the boxes are where they should be at six widths and at
scroll offset 0, and at 320 minus the gaps the reflow step names, on the front page and on the
kildeutforsker; it does not say the page looks
right, and it says nothing at all about the search-only mount this host does not render. The
header of this script lists the rest. Assertions printed `n/a` measured nothing on that page and
say so; they are not passes.
EOF
