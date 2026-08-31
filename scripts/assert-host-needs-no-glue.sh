#!/usr/bin/env bash
#
# Fails if a host cannot get shareable explorer URLs out of the package alone.
#
# VariableExplorerWithUrlState and KildeExplorerWithUrlState exist so that a host writes no glue:
# no wrapper component, no query-string parsing, no history.replaceState. Both sample hosts prove
# that badly. They sit in this repository and compile against src/, so a parameter that only exists
# on this branch, a type the package does not actually export, or a component reachable only through
# a project reference all look fine there and fail on someone else's build server.
#
# So this does not mount them in a sample. It packs the package, hands the .nupkg to a throwaway
# project that has a PackageReference and nothing else, and insists the mount compiles.
#
# What it proves is the surface, not the behaviour: that a host outside this repository can write
# exactly the two tags below and no more. What the reader sees when they open a link is
# UrlStateComponentTest's job.
#
# Usage:
#   scripts/assert-host-needs-no-glue.sh
#
# Exit 0 clean, 1 the consumer could not build, 2 the check could not run.

set -uo pipefail

ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
PROJECT="$ROOT/src/Fhi.Munin.Explorer/Fhi.Munin.Explorer.csproj"

if [ ! -f "$PROJECT" ]; then
  echo "::error::$PROJECT is missing, so there is no package to consume." >&2
  exit 2
fi

# Outside the repository on purpose. Inside it, Directory.Build.props would reach the consumer and
# hand it our target framework, our analyzers and our package identity — none of which a real host
# has, and every one of which could hide the thing this check is looking for.
WORK=$(mktemp -d)
cleanup() { rm -rf "$WORK"; }
trap cleanup EXIT

FEED="$WORK/feed"
CONSUMER="$WORK/consumer"
mkdir -p "$FEED" "$CONSUMER"

echo "Packing the package a host would install."

if ! pack=$(dotnet pack "$PROJECT" --configuration Release --output "$FEED" --nologo -v quiet 2>&1); then
  echo "::error::The package would not pack, so no consumer could install it." >&2
  echo "$pack" >&2
  exit 2
fi

cat > "$CONSUMER/nuget.config" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="../feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <auditSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </auditSources>
</configuration>
EOF

cat > "$CONSUMER/Consumer.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- RZ10012 is "markup element with unexpected name", which is what Razor says instead of an
         error when a component tag names a type it cannot find: the tag is emitted as literal HTML
         and the build stays green. Without this the probe below would pass against a package that
         ships neither component. -->
    <WarningsAsErrors>$(WarningsAsErrors);RZ10012</WarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <!-- A PackageReference and nothing else. No ProjectReference: that is the whole point. -->
    <PackageReference Include="Fhi.Munin.Explorer" Version="*" />
  </ItemGroup>
</Project>
EOF

cat > "$CONSUMER/_Imports.razor" <<'EOF'
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
EOF

# Everything a host writes, and there is deliberately nothing else in this file. If the two mounts
# below stop compiling against the package alone, a host has to write glue again.
cat > "$CONSUMER/MountedWithNoGlue.razor" <<'EOF'
@using Fhi.Munin.Explorer.Blazor

<VariableExplorerWithUrlState Language="no" DeclinedKeys="@(new[] { "search" })" />

<KildeExplorerWithUrlState Language="no" VariableExplorerPath="/" />
EOF

# The same two types from C#, where naming one the package does not export is a hard error rather
# than a warning. Belt and braces with RZ10012 above, because that half is one build property away
# from being silent again.
cat > "$CONSUMER/Exported.cs" <<'EOF'
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;

internal static class Exported
{
    internal static readonly Type[] Mounted =
        [typeof(VariableExplorerWithUrlState), typeof(KildeExplorerWithUrlState)];

    // The do-it-yourself route stays public beside them: a host that wants to own its own address
    // bar builds the query with these rather than mounting the components above.
    internal static readonly string Query = ExplorerUrlState.Parse("?search=x").ToQueryString();

    internal static readonly IReadOnlySet<string> Keys = ExplorerUrlState.QueryKeys;
}
EOF

echo "Building a consumer that has the package and no source of ours."

if output=$(dotnet build "$CONSUMER/Consumer.csproj" --configuration Release --nologo -v quiet 2>&1); then
  echo "A host gets shareable URLs from the package alone: both mounts compiled with no glue."
  exit 0
fi

echo "::error::A project with only a PackageReference could not mount the URL-state components." >&2
echo "  Either they are not in the package, or mounting them needs something this repository has" >&2
echo "  and a consumer does not. The two tags in the probe are the whole of what a host may write." >&2
echo "" >&2
echo "$output" >&2

exit 1
