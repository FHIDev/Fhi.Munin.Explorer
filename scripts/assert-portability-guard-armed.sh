#!/usr/bin/env bash
#
# Fails if the RCL's portability guard is not actually armed.
#
# BannedSymbols.txt is what stops host-specific types — IHttpContextAccessor, EPiServer, file IO —
# from reaching a component that has to render in two different hosts. README.md and CLAUDE.md both
# say it "turns that into a build error". It only does so while three separate things line up: the
# ItemGroup in Directory.Build.props matches the RCL's project name, BannedApiAnalyzers is
# referenced from it, and BannedSymbols.txt reaches the compiler as an AdditionalFile.
#
# All three were off at once for as long as nobody looked. The condition named
# 'Fhi.Munin.Explorer.Blazor', a project that stopped existing when the three were merged into one,
# so it matched nothing — no analyzer, no AdditionalFile, no RS0030 — and every build stayed green
# the whole time. A guard that fails open announces nothing when it breaks, which is why the
# comment above that condition is not enough on its own: it asks a future renamer to remember, and
# the last person's memory is exactly what was already spent.
#
# So this does not read the build files and reason about them. It hands the compiler a file that
# uses a banned symbol and insists on being told no.
#
# Usage:
#   scripts/assert-portability-guard-armed.sh
#
# Takes about as long as building the RCL, which is why it is its own CI job rather than something
# bolted onto the build.

set -uo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
PROJECT="$ROOT/src/Fhi.Munin.Explorer/Fhi.Munin.Explorer.csproj"
PROJECT_DIR=$(dirname "$PROJECT")
PROPS="$ROOT/Directory.Build.props"
BANNED="$ROOT/BannedSymbols.txt"

# System.IO.File is in BannedSymbols.txt and is in no way special there. Any entry would do; this
# one is picked because it needs no package reference to write down, so the probe compiles or fails
# for one reason only.
PROBE_SYMBOL="System.IO.File"
PROBE="$PROJECT_DIR/PortabilityGuardProbe.g.cs"

cleanup() { rm -f "$PROBE"; }
trap cleanup EXIT

for required in "$PROJECT" "$PROPS" "$BANNED"; do
  if [ ! -f "$required" ]; then
    echo "::error::$required is missing, so the portability guard cannot be checked." >&2
    exit 1
  fi
done

# Cheap check first, because it gives the precise diagnostic. The end-to-end probe below would also
# catch a stale name, but it would only be able to say "RS0030 did not fire".
PROJECT_NAME=$(basename "$PROJECT" .csproj)
if ! grep -q "'\$(MSBuildProjectName)' == '$PROJECT_NAME'" "$PROPS"; then
  echo "::error::No ItemGroup in Directory.Build.props is conditioned on '$PROJECT_NAME'." >&2
  echo "  The RCL was renamed and the portability guard's condition did not move with it." >&2
  echo "  Point the BannedApiAnalyzers ItemGroup at '$PROJECT_NAME' and re-run this script." >&2
  exit 1
fi

cat > "$PROBE" <<EOF
// Written by scripts/assert-portability-guard-armed.sh and deleted again by it. If you are reading
// this in a checkout, that script was interrupted — delete the file, it is not part of the RCL.
namespace Fhi.Munin.Explorer.PortabilityGuardProbe;

internal static class Probe
{
    internal static bool Check(string path) => $PROBE_SYMBOL.Exists(path);
}
EOF

echo "Building the RCL against a probe that uses $PROBE_SYMBOL, which BannedSymbols.txt forbids."

output=$(dotnet build "$PROJECT" --configuration Release --nologo --verbosity quiet 2>&1)
status=$?

cleanup

if grep -q 'RS0030' <<<"$output"; then
  echo "Portability guard is armed: RS0030 fired on $PROBE_SYMBOL."
  exit 0
fi

if [ "$status" -eq 0 ]; then
  echo "::error::The RCL built clean against a file using $PROBE_SYMBOL. RS0030 never fired, so the portability guard is off and any host-specific type would now compile." >&2
else
  echo "::error::The probe build failed without reporting RS0030, so this script cannot tell whether the portability guard is armed." >&2
fi

echo "  * Directory.Build.props must reference Microsoft.CodeAnalysis.BannedApiAnalyzers for $PROJECT_NAME." >&2
echo "  * BannedSymbols.txt must be an <AdditionalFiles> entry in that same ItemGroup." >&2
echo "  * $PROBE_SYMBOL must still be listed in BannedSymbols.txt — if it was removed on purpose, point PROBE_SYMBOL at an entry that is still there." >&2
echo "" >&2
echo "Build output:" >&2
echo "$output" >&2

exit 1
