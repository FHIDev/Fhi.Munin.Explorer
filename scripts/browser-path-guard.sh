# shellcheck shell=bash
# Sourced by the browser gates before they run `playwright install chromium` (Fhi.Metadata-l9l2n.148).
# That install REPLACES the target folder's contents with this repo's pinned build. On the devbox the default
# PLAYWRIGHT_BROWSERS_PATH is the shared /opt/ms-playwright, so a gate run there broke every other session's browser.

refuse_shared_browser_path() {
  [ -n "${PLAYWRIGHT_BROWSER_CHANNEL:-}" ] && return 0   # an installed browser: nothing is downloaded
  local target="${PLAYWRIGHT_BROWSERS_PATH:-}"
  [ -z "$target" ] && return 0                            # Playwright's own per-user cache
  local resolved
  resolved="$(cd "$target" 2>/dev/null && pwd -P || printf '%s' "${target%/}")"
  if [ "$resolved" = "/opt/ms-playwright" ] || [ "${target%/}" = "/opt/ms-playwright" ]; then
    echo "refusing to install browsers into $target: it is the box's shared folder, and the install replaces it." >&2
    echo "use a folder of your own, for example:" >&2
    echo "  PLAYWRIGHT_BROWSERS_PATH=\"\$HOME/.cache/explorer-pw\" bash $0" >&2
    exit 2
  fi
}
