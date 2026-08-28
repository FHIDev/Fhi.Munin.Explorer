#!/usr/bin/env bash
#
# Fails if a built package does not have exactly the shape we intend to publish.
#
# A published version cannot be taken back. The internal feed does allow one to be deleted, but
# that does not reach anyone who already restored it, and by the time anyone notices that 0.4.0
# shipped a stray stylesheet the wrong bytes are already on helsedata's build server. So the
# check runs before the push, and on every PR, rather than after someone reports it.
#
# The rule is "exactly this set", not "at least this set". A missing file and an unexpected one
# are both packaging bugs, and the unexpected one is the more dangerous of the two:
#
#   * A wwwroot/ or staticwebassets/ entry in the RCL would mean the component started shipping
#     its own CSS. It deliberately does not — it emits the host's class names so helsedata's
#     Stiler styles it, and a stylesheet riding along in the package would silently start
#     competing with theirs.
#   * A second lib/<tfm>/ directory would mean the target framework moved without anyone saying
#     so, which changes who can install the package at all.
#   * A .pdb, content file, or build/ props file means something got included that we never
#     decided to support, and support is exactly what shipping it implies.
#
# Usage:
#   scripts/assert-package-contents.sh [artifacts-dir] [expected-version]
#
# Defaults to ./artifacts and does not check the version. The release workflow passes the
# version derived from the tag, so a mis-tagged build fails before it is pushed anywhere.

set -euo pipefail

DIR="${1:-artifacts}"
EXPECTED_VERSION="${2:-}"

# Every package we publish, and the framework they all target. Adding a package here is the
# deliberate act that makes the check aware of it — a new .nupkg nobody listed is an error.
PACKAGES=(
  Fhi.Munin.Explorer
)
TFM=net10.0

failures=0
note() { printf '  %s\n' "$*"; }
bad() { printf '  FAIL  %s\n' "$*" >&2; failures=$((failures + 1)); }

if [ ! -d "$DIR" ]; then
  echo "No such directory: $DIR — run 'dotnet pack -c Release -o $DIR' first." >&2
  exit 1
fi

# ---------------------------------------------------------------------------------------------
# Nothing unlisted may be packed. A stray .nupkg cannot reach the feed by itself any more —
# push-packages.sh names the one file it pushes and release.yml has no wildcard push — so this is
# not the last line of defence it once was, when a set of packages went out together. It is still
# the earliest sign that packing did something nobody asked for: a sample host that lost its
# IsPackable=false, a project that gained one, a rename that produced two ids where there is meant
# to be one. Whatever produced it, the answer to "which package are we shipping" stopped being
# obvious, and that is worth failing a PR over before it is worth reasoning about at tag time.
# ---------------------------------------------------------------------------------------------
shopt -s nullglob
for path in "$DIR"/*.nupkg; do
  base=$(basename "$path")
  known=0
  for pkg in "${PACKAGES[@]}"; do
    case "$base" in "$pkg".[0-9]*.nupkg) known=1 ;; esac
  done
  [ "$known" = "1" ] || bad "unexpected package in $DIR: $base (add it to PACKAGES if it is meant to ship)"
done
shopt -u nullglob

for pkg in "${PACKAGES[@]}"; do
  echo "$pkg"

  matches=("$DIR/$pkg".[0-9]*.nupkg)
  if [ ! -e "${matches[0]}" ]; then
    bad "not built — no $DIR/$pkg.<version>.nupkg"
    continue
  fi
  if [ "${#matches[@]}" -gt 1 ]; then
    bad "more than one build present: ${matches[*]} — clear $DIR and pack again"
    continue
  fi
  nupkg="${matches[0]}"

  # ------------------------------------------------------------------------------------------
  # Contents. Everything under _rels/ and package/services/ is OPC plumbing written by the
  # packer itself, and [Content_Types].xml likewise — none of it is ours to assert on.
  # ------------------------------------------------------------------------------------------
  # `|| true` for the same reason as below: a package whose every entry was filtered out would
  # otherwise abort the script rather than be reported as the empty package it is.
  actual=$(unzip -Z1 "$nupkg" \
    | grep -Ev '^(_rels/|package/services/)' \
    | grep -Fxv '[Content_Types].xml' \
    | LC_ALL=C sort || true)

  expected=$(printf '%s\n' \
    "$pkg.nuspec" \
    "README.md" \
    "lib/$TFM/$pkg.dll" \
    "lib/$TFM/$pkg.xml" \
    | LC_ALL=C sort)

  if [ "$actual" = "$expected" ]; then
    note "contents ok ($(echo "$actual" | wc -l | tr -d ' ') entries)"
  else
    bad "contents differ from what we intend to publish:"
    diff <(echo "$expected") <(echo "$actual") | sed 's/^/        /' >&2 || true
    note "        (< expected, > actually in the package)"
  fi

  # ------------------------------------------------------------------------------------------
  # Metadata that a stranger sees before they trust the package. An empty description is not a
  # cosmetic problem: it is the whole of what the feed shows next to the name, and for this
  # version it can never be corrected.
  # ------------------------------------------------------------------------------------------
  nuspec=$(unzip -p "$nupkg" "$pkg.nuspec")
  flat=$(echo "$nuspec" | tr '\n' ' ')

  # Note what this compares against. When no <Description> property is set, dotnet pack does not
  # leave the element empty — it substitutes the literal string "Package Description". Checking
  # only for emptiness therefore passes happily on exactly the package this check exists to
  # stop, and "Package Description" would be the headline text on the feed for good.
  # The `|| true` matters. grep exits 1 when it matches nothing, and under `set -e` a bare
  # assignment from a failing pipeline kills the script — so a nuspec with no <description> at
  # all would abort the run with a bare exit code instead of reporting which package is wrong
  # and carrying on to check the others. An absent element must reach the check as "".
  description=$(echo "$flat" | grep -o '<description>.*</description>' | sed 's/<[^>]*>//g' | tr -d ' \t' || true)
  case "$description" in
    "")
      bad "empty <description> — this is the text the feed shows, and it cannot be edited after publish"
      ;;
    "PackageDescription")
      bad "<description> is still dotnet pack's placeholder 'Package Description' — set <Description> in $pkg.csproj"
      ;;
    *)
      note "description present"
      ;;
  esac

  echo "$flat" | grep -q '<license type="expression">MIT</license>' \
    || bad "license is not the MIT expression we publish under"
  echo "$flat" | grep -q '<readme>README.md</readme>' \
    || bad "README not declared in the nuspec — the package page would render empty"

  if [ -n "$EXPECTED_VERSION" ]; then
    version=$(echo "$flat" | grep -o '<version>[^<]*</version>' | head -1 | sed 's/<[^>]*>//g' || true)
    if [ "$version" = "$EXPECTED_VERSION" ]; then
      note "version $version"
    else
      bad "version is $version but the tag says $EXPECTED_VERSION"
    fi
  fi
done

echo
if [ "$failures" -gt 0 ]; then
  echo "$failures package check(s) failed — nothing has been published." >&2
  exit 1
fi
echo "All packages have the expected shape."
