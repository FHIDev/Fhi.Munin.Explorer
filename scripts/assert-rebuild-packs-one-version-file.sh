#!/usr/bin/env bash
# Fails if building the package a second time, with wwwroot/version.json left by the first build,
# no longer packs exactly one correct version file.
#
# The failure it was written after only happens on Windows. There the SDK's glob spells the
# existing file wwwroot\version.json, a forward-slash Content Remove did not match it, and from
# the second build on the file was included twice and every build and pack failed
# (Fhi.Metadata-l9l2n.123). On Linux both spellings are the same path, so in the build job this
# script passed even with that bug: there it only guards the rebuild path in general. The
# `windows-rebuild` job in ci.yml runs it on Windows, and that run is the guard.
#
# Usage: scripts/assert-rebuild-packs-one-version-file.sh
# Exit 0 clean, 1 the rebuild or its package is wrong, 2 the check could not run.

set -uo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
PROJECT="$ROOT/src/Fhi.Munin.Explorer/Fhi.Munin.Explorer.csproj"
FILE="$ROOT/src/Fhi.Munin.Explorer/wwwroot/version.json"
VERSION=9.8.7

[ -f "$PROJECT" ] || { echo "::error::$PROJECT is missing." >&2; exit 2; }

OUT=$(mktemp -d)
trap 'rm -rf "$OUT"' EXIT

# Twice on purpose, never cleaning in between: the second build is the one under test.
for round in first second; do
  if ! dotnet build "$PROJECT" --configuration Release -p:Version=$VERSION --nologo -v q > "$OUT/build-$round.log" 2>&1; then
    echo "::error::The $round build failed." >&2; tail -20 "$OUT/build-$round.log" >&2; exit 1
  fi
  [ -f "$FILE" ] || { echo "::error::The $round build did not write wwwroot/version.json." >&2; exit 1; }
done

if ! dotnet pack "$PROJECT" --configuration Release --no-build -p:Version=$VERSION --output "$OUT/pkg" --nologo -v q > "$OUT/pack.log" 2>&1; then
  echo "::error::Packing after the second build failed." >&2; tail -20 "$OUT/pack.log" >&2; exit 1
fi

"$ROOT/scripts/assert-package-contents.sh" "$OUT/pkg" "$VERSION" || exit 1
echo "A rebuild with version.json already on disk still packs one file, saying $VERSION."
