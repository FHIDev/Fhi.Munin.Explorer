#!/usr/bin/env bash
#
# Pushes the built packages to nuget.org, in dependency order, so that a failure part way
# through can be recovered by re-running rather than by hand.
#
# The problem this solves. The three packages are published together under one version. A plain
# loop over `dotnet nuget push` aborts on the first failure, so a network blip while pushing the
# second package leaves the first one live on nuget.org and the other two missing — and since
# published versions cannot be replaced or deleted, that state cannot be cleaned up, only
# completed. Re-running a plain loop does not complete it either: the first package now exists,
# its push fails with a 409, and the run stops again before reaching the ones that are actually
# missing.
#
# So the run starts by asking nuget.org what is already there:
#
#   * none published  — a normal first publish.
#   * some published  — a previous run got part way. Push what is missing, skip what is not.
#   * all published   — this version is already out. That is a reused tag, and it stops here,
#                       because the alternative is reporting success for a push that did not
#                       happen and never noticing the tag meant to ship something new.
#
# Within a run, a 409 from a package we believed was missing is treated as success: it means our
# own attempt landed and we did not see the answer, or nuget.org has not finished indexing it.
# That is not the reused-tag case — that one was already ruled out above.
#
# Usage:
#   NUGET_ORG_FHI_PUBLISH=<key> scripts/push-packages.sh <artifacts-dir> <version>

set -euo pipefail

DIR="${1:?artifacts directory required}"
VERSION="${2:?version required}"
: "${NUGET_ORG_FHI_PUBLISH:?the nuget.org API key must be in the environment}"

SOURCE="https://api.nuget.org/v3/index.json"

# Dependency order. Contracts is what the other two depend on, and nuget.org takes a few minutes
# to index a new package — pushing it first means a consumer who restores during that window is
# never told the dependency does not exist.
PACKAGES=(
  Fhi.Munin.Explorer.Contracts
  Fhi.Munin.Explorer.Client
  Fhi.Munin.Explorer.Blazor
)

MAX_ATTEMPTS=5

# Is this exact version already on nuget.org? Answers "no" when it cannot tell — a failed query
# must not be able to skip a push. The 409 handling below is what covers that case safely.
already_published() {
  local id_lower body code
  id_lower=$(echo "$1" | tr '[:upper:]' '[:lower:]')

  body=$(mktemp)
  code=$(curl -sS -o "$body" -w '%{http_code}' \
    "https://api.nuget.org/v3-flatcontainer/$id_lower/index.json" 2>/dev/null || echo "000")

  if [ "$code" = "200" ] && grep -q "\"$VERSION\"" "$body"; then
    rm -f "$body"
    return 0
  fi

  rm -f "$body"
  return 1
}

push_one() {
  local file="$1" name attempt=1 output status
  name=$(basename "$file")

  while :; do
    set +e
    output=$(dotnet nuget push "$file" --source "$SOURCE" --api-key "$NUGET_ORG_FHI_PUBLISH" 2>&1)
    status=$?
    set -e
    printf '%s\n' "$output"

    [ "$status" -eq 0 ] && return 0

    # Not the reused-tag case: that was ruled out before any push happened. Here it means our
    # own attempt landed, or the package is mid-indexing.
    if printf '%s' "$output" | grep -qiE '409|already exists'; then
      echo "    $name is already on nuget.org — counting it as pushed."
      return 0
    fi

    if [ "$attempt" -ge "$MAX_ATTEMPTS" ]; then
      echo "::error::Giving up on $name after $MAX_ATTEMPTS attempts. Re-running this workflow is safe: it will skip whatever already published and push only what is missing."
      return 1
    fi

    echo "    attempt $attempt failed, retrying in $((attempt * 10))s"
    sleep $((attempt * 10))
    attempt=$((attempt + 1))
  done
}

echo "Checking what is already on nuget.org for $VERSION"

missing=()
present=()
for pkg in "${PACKAGES[@]}"; do
  if already_published "$pkg"; then
    present+=("$pkg")
    echo "  $pkg $VERSION is already published"
  else
    missing+=("$pkg")
    echo "  $pkg $VERSION is not published yet"
  fi
done

if [ "${#missing[@]}" -eq 0 ]; then
  echo "::error::Every package is already published at $VERSION. Versions on nuget.org cannot be replaced — tag a new version rather than reusing this one."
  exit 1
fi

if [ "${#present[@]}" -gt 0 ]; then
  echo
  echo "Resuming a partly finished publish: ${present[*]} already went out, pushing the rest."
fi

echo
for pkg in "${missing[@]}"; do
  file="$DIR/$pkg.$VERSION.nupkg"
  if [ ! -f "$file" ]; then
    echo "::error::$file was not built, so $VERSION cannot be completed."
    exit 1
  fi
  echo "Pushing $pkg $VERSION"
  push_one "$file"
done

echo
echo "All packages are on nuget.org at $VERSION."
