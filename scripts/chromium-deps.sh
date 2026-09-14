#!/usr/bin/env bash
#
# Sourced by the browser gates after `playwright install chromium`. Where the bundled chromium is
# missing shared libraries or the host has no fonts, and there is no root to `install-deps` with,
# it unpacks Ubuntu's own packages into a user prefix and exports LD_LIBRARY_PATH and
# FONTCONFIG_FILE at it. A no-op on a host that already has both, which is every CI runner.
#
# Usage:  . "$ROOT/scripts/chromium-deps.sh" && chromium_deps_ensure || exit 2
# Why, and what it costs a measurement: docs/running-locally.md, "No root and no browser libraries".

# Bump the suffix when PACKAGES changes: a prefix is never rebuilt in place, since another worktree
# on the same box may be running a browser out of it.
CHROMIUM_DEPS_PREFIX="${CHROMIUM_DEPS_PREFIX:-$HOME/.cache/munin-explorer/chromium-deps-v1}"
CHROMIUM_DEPS_MIRROR="${CHROMIUM_DEPS_MIRROR:-http://archive.ubuntu.com/ubuntu}"

# Ubuntu 24.04 names. The t64 suffixes are noble's time_t transition: libglib2.0-0 does not exist
# there. Anything dpkg already reports installed is skipped, so the prefix never shadows the host.
CHROMIUM_DEPS_PACKAGES="fonts-dejavu-core fonts-liberation libasound2t64 libatk-bridge2.0-0t64
libatk1.0-0t64 libatomic1 libatspi2.0-0t64 libavahi-client3 libavahi-common3 libblkid1 libbrotli1
libbsd0 libcairo-gobject2 libcairo2 libcap2 libcups2t64 libdatrie1 libdbus-1-3 libdrm2 libepoxy0
libexpat1 libffi8 libfontconfig1 libfreetype6 libfribidi0 libgbm1 libgcrypt20 libgdk-pixbuf-2.0-0
libgl1 libglib2.0-0t64 libglvnd0 libglx0 libgmp10 libgnutls30t64 libgpg-error0 libgraphite2-3
libgtk-3-0t64 libharfbuzz0b libhogweed6t64 libice6 libidn2-0 liblzma5 libmd0 libmount1
libnettle8t64 libnspr4 libnss3 libp11-kit0 libpango-1.0-0 libpangocairo-1.0-0 libpangoft2-1.0-0
libpcre2-8-0 libpixman-1-0 libpng16-16t64 libselinux1 libsm6 libsystemd0 libtasn1-6 libthai0
libudev1 libunistring5 libuuid1 libwayland-client0 libwayland-server0 libx11-6 libxau6
libxcb-dri2-0 libxcb-dri3-0 libxcb-glx0 libxcb-present0 libxcb-render0 libxcb-shm0 libxcb-sync1
libxcb-xfixes0 libxcb1 libxcomposite1 libxcursor1 libxdamage1 libxdmcp6 libxext6 libxfixes3 libxi6
libxkbcommon0 libxrandr2 libxrender1 libxshmfence1 libxtst6 libzstd1 zlib1g"

# Unresolved libraries across every bundled chromium build in the cache, headless shell included.
chromium_deps_missing() {
  local cache="${PLAYWRIGHT_BROWSERS_PATH:-$HOME/.cache/ms-playwright}" bin n=0 c
  for bin in "$cache"/chromium_headless_shell-*/chrome-linux/headless_shell \
             "$cache"/chromium-*/chrome-linux/chrome; do
    [ -x "$bin" ] || continue
    c=$(ldd "$bin" 2>/dev/null | grep -c 'not found' || true)
    n=$((n + c))
  done
  echo "$n"
}

# Without a font chromium still starts, and lays every glyph out at zero width: text has no box,
# so a geometry assertion about text reads a missing font as a page defect.
chromium_deps_host_has_fonts() {
  [ -n "$(find /usr/share/fonts /usr/local/share/fonts -type f \
      \( -name '*.ttf' -o -name '*.otf' -o -name '*.pfb' \) 2>/dev/null | head -1 || true)" ]
}

chromium_deps_export() {
  local p="$CHROMIUM_DEPS_PREFIX"
  export LD_LIBRARY_PATH="$p/usr/lib/x86_64-linux-gnu:$p/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  export FONTCONFIG_FILE="$p/fonts.conf"
}

chromium_deps_build() {
  local p="$CHROMIUM_DEPS_PREFIX" tool codename work stage suite want unresolved pkg file
  for tool in curl gzip awk dpkg-deb dpkg-query; do
    command -v "$tool" >/dev/null || { echo "chromium-deps: needs $tool, which is not on PATH." >&2; return 2; }
  done
  codename=$(. /etc/os-release 2>/dev/null && echo "${VERSION_CODENAME:-}")
  if [ "$codename" != noble ] || [ "$(dpkg --print-architecture)" != amd64 ]; then
    echo "chromium-deps: the package list is Ubuntu 24.04 amd64's; this is '${codename:-unknown}'." >&2
    return 2
  fi

  work=$(mktemp -d)
  mkdir -p "$(dirname "$p")"
  stage=$(mktemp -d "$p.partial.XXXXXX")

  echo "==> unpacking chromium's libraries and fonts into $p (once per box, a few minutes)"
  # Later suites win, so noble-updates overrides the release.
  for suite in noble noble-security noble-updates; do
    curl -fsSL "$CHROMIUM_DEPS_MIRROR/dists/$suite/main/binary-amd64/Packages.gz" | gzip -dc \
      | awk '/^Package: /{p=$2} /^Filename: /{print p, $2}' >>"$work/index" \
      || { echo "chromium-deps: could not read the $suite index from $CHROMIUM_DEPS_MIRROR." >&2; rm -rf "$work" "$stage"; return 2; }
  done

  # A substitution, not a pipe into `grep -q`: under pipefail its early exit reads as "not
  # installed" (Fhi.Metadata-v198s).
  for pkg in $CHROMIUM_DEPS_PACKAGES; do
    [[ "$(dpkg-query -W -f='${Status}' "$pkg" 2>/dev/null || true)" == *"ok installed"* ]] || echo "$pkg"
  done >"$work/wanted"

  awk 'NR==FNR{want[$1]=1; next} ($1 in want){f[$1]=$2} END{for (k in want) print k, (k in f ? f[k] : "-")}' \
    "$work/wanted" "$work/index" >"$work/resolved"
  unresolved=$(awk '$2=="-"{print $1}' "$work/resolved")
  if [ -n "$unresolved" ]; then
    echo "chromium-deps: not in noble main: $unresolved" >&2
    rm -rf "$work" "$stage"
    return 2
  fi

  while read -r pkg file; do
    curl -fsSL "$CHROMIUM_DEPS_MIRROR/$file" -o "$work/pkg.deb" && dpkg-deb -x "$work/pkg.deb" "$stage" \
      || { echo "chromium-deps: could not fetch or unpack $pkg." >&2; rm -rf "$work" "$stage"; return 2; }
  done <"$work/resolved"

  mkdir -p "$stage/fontcache"
  cat >"$stage/fonts.conf" <<EOF
<?xml version="1.0"?>
<!DOCTYPE fontconfig SYSTEM "fonts.dtd">
<fontconfig>
  <dir>$p/usr/share/fonts</dir>
  <dir>/usr/share/fonts</dir>
  <dir>/usr/local/share/fonts</dir>
  <cachedir>$p/fontcache</cachedir>
</fontconfig>
EOF
  touch "$stage/.complete"
  rm -rf "$work"

  # A worktree beside this one may have finished first; its prefix is as good as ours.
  mv -T "$stage" "$p" 2>/dev/null || rm -rf "$stage"
}

chromium_deps_ensure() {
  [ "$(uname -s)" = Linux ] || return 0
  if [ "$(chromium_deps_missing)" -eq 0 ] && chromium_deps_host_has_fonts; then
    return 0
  fi

  if [ ! -f "$CHROMIUM_DEPS_PREFIX/.complete" ]; then
    rm -rf "$CHROMIUM_DEPS_PREFIX"
    chromium_deps_build || return 2
  fi

  chromium_deps_export
  local still
  still=$(chromium_deps_missing)
  if [ "$still" -ne 0 ]; then
    echo "chromium-deps: $still libraries still unresolved with $CHROMIUM_DEPS_PREFIX on the path." >&2
    return 2
  fi
  echo "==> BROWSER LIBS: $CHROMIUM_DEPS_PREFIX, not the host's - its fonts are not CI's, so text widths need not match CI's"
}
