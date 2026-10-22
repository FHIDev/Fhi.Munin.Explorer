#!/usr/bin/env bash
# The browser gates refuse the devbox's shared browser folder (Fhi.Metadata-l9l2n.148).
#   bash scripts/test-browser-path-guard.sh
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
pass=0; fail=0
check() { # $1 label, $2 expected exit, rest: env assignments
  local label=$1 want=$2; shift 2
  # shellcheck disable=SC2016  # $1 is bash -c's own argument, expanded inside that shell
  env -i PATH="$PATH" HOME="$HOME" "$@" bash -c '. "$1"; refuse_shared_browser_path; exit 0' _ "$ROOT/scripts/browser-path-guard.sh" >/dev/null 2>&1
  local got=$?
  if [ "$got" = "$want" ]; then pass=$((pass + 1)); else fail=$((fail + 1)); echo "FAIL: $label (exit $got, wanted $want)"; fi
}
check "the shared folder is refused" 2 PLAYWRIGHT_BROWSERS_PATH=/opt/ms-playwright
check "with a trailing slash too" 2 PLAYWRIGHT_BROWSERS_PATH=/opt/ms-playwright/
check "a private folder is allowed" 0 PLAYWRIGHT_BROWSERS_PATH="$HOME/.cache/explorer-pw"
check "unset (Playwright's own cache) is allowed" 0
check "a browser channel downloads nothing, so even the shared path passes" 0 PLAYWRIGHT_BROWSERS_PATH=/opt/ms-playwright PLAYWRIGHT_BROWSER_CHANNEL=msedge
# A CI runner has no /opt/ms-playwright; the link case needs the folder to point at.
if [ -d /opt/ms-playwright ] && ln -s /opt/ms-playwright "${TMPDIR:-/tmp}/pw-guard-link-$$" 2>/dev/null; then
  check "a link to the shared folder is refused" 2 PLAYWRIGHT_BROWSERS_PATH="${TMPDIR:-/tmp}/pw-guard-link-$$"
  rm -f "${TMPDIR:-/tmp}/pw-guard-link-$$"
fi
echo "browser-path-guard: $pass passed, $fail failed"
[ "$fail" = 0 ]
