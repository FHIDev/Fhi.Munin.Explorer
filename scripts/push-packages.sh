#!/usr/bin/env bash
#
# Pushes the built packages to the internal feed, in dependency order, so that a failure part way
# through can be recovered by re-running rather than by hand.
#
# The problem this solves. The three packages are published together under one version. A plain
# loop over `dotnet nuget push` aborts on the first failure, so a network blip while pushing the
# second package leaves the first one live on the feed and the other two missing — and while
# this feed does allow a version to be deleted, deleting is not the way out: anyone who restored
# the first package in the meantime keeps it, and pushing a different build under the same version
# leaves two things claiming to be it. The half-done state is finished, not undone.
# Re-running a plain loop does not finish it either: the first package now exists,
# its push fails with a 409, and the run stops again before reaching the ones that are actually
# missing.
#
# So the run starts by asking the feed what is already there:
#
#   * none published  — a normal first publish.
#   * some published  — a previous run got part way. Push what is missing, skip what is not.
#   * all published   — this version is already out. That is a reused tag, and it stops here,
#                       because the alternative is reporting success for a push that did not
#                       happen and never noticing the tag meant to ship something new.
#
# Within a run, a 409 from a package we believed was missing is treated as success: it means our
# own attempt landed and we did not see the answer, or the feed has not finished indexing it.
# That is not the reused-tag case — that one was already ruled out above.
#
# Usage:
#   NUGET_PUBLISH_TOKEN=<pat-or-entra-token> scripts/push-packages.sh <artifacts-dir> <version>

set -euo pipefail

DIR="${1:?artifacts directory required}"
VERSION="${2:?version required}"
: "${NUGET_PUBLISH_TOKEN:?a credential for the feed must be in the environment}"

# The internal feed, which is where helsedata's own packages come from and what their Optimizely
# project already restores from. Overridable so a fork or a dry run can point somewhere else.
FEED_BASE="${NUGET_PUBLISH_FEED:-https://fhi.pkgs.visualstudio.com/Fhi.Helsedata/_packaging/Fhi.Helsedata.no/nuget}"
SOURCE="$FEED_BASE/v3/index.json"

# The NuGet protocol's own "which versions of this package exist" endpoint. Asking the protocol
# rather than the host's API means this reads the same against any v3 feed.
FLAT="$FEED_BASE/v3/flat2"

# Azure Artifacts authenticates with the credential as the HTTP basic PASSWORD and ignores the
# username, so the same token works whether it is a PAT or an Entra access token from OIDC —
# which is what lets the workflow choose without this script caring. --api-key is still required
# by dotnet nuget push, and still ignored by the feed; "az" is the conventional filler.
SOURCE_NAME="helsedata-internal"

# Dependency order. Contracts is what the other two depend on, and a feed can take a moment
# to index a new package - pushing it first means a consumer who restores during that window is
# never told the dependency does not exist.
PACKAGES=(
  Fhi.Munin.Explorer.Contracts
  Fhi.Munin.Explorer.Client
  Fhi.Munin.Explorer.Blazor
)

# A config file of our own, and both commands are pointed at it.
#
# Registering into the default user config does not work here: this repository's own nuget.config
# starts with <clear />, so a source defined further up the chain is discarded before the push
# ever sees it, and --source helsedata-internal fails to resolve from the repository root. That is
# also why the dry run did not catch it - it stopped at the pre-flight, before any push.
#
# Temporary rather than in the tree, because this file holds the credential in clear text. It is
# written outside the checkout so it cannot be committed by accident, and removed on the way out
# whether the run succeeded or not.
CONFIG=$(mktemp -t nuget-push-XXXXXX.config)
trap 'rm -f "$CONFIG"' EXIT

cat > "$CONFIG" <<'XML'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
XML

dotnet nuget add source "$SOURCE" \
  --name "$SOURCE_NAME" \
  --username unused \
  --password "$NUGET_PUBLISH_TOKEN" \
  --store-password-in-clear-text \
  --configfile "$CONFIG" >/dev/null

MAX_ATTEMPTS=5

# How many of the packages we set out to push turned out to be on the feed already. Used after
# the loop to tell "we finished a half-done publish" apart from "none of this was new".
conflicts=0

# Is this exact version already on the feed? Answers "no" when it cannot tell — a failed query
# must not be able to skip a push. The 409 handling below is what covers that case safely.
already_published() {
  local id_lower body code
  id_lower=$(echo "$1" | tr '[:upper:]' '[:lower:]')

  body=$(mktemp)
  # -u with an empty username: the feed reads the password and ignores the rest. An unpublished
  # package answers 404 here, which is the "not yet" this function reports.
  code=$(curl -sS -o "$body" -w '%{http_code}' \
    -u ":$NUGET_PUBLISH_TOKEN" \
    "$FLAT/$id_lower/index.json" 2>/dev/null || echo "000")

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
    output=$(dotnet nuget push "$file" --source "$SOURCE_NAME" --api-key az --configfile "$CONFIG" 2>&1)
    status=$?
    set -e
    printf '%s\n' "$output"

    [ "$status" -eq 0 ] && return 0

    # Already on the feed. Usually that means our own attempt landed and we lost the answer, or
    # the package is mid-indexing — so the version is in the state we want and the run carries
    # on. It is recorded rather than just accepted, because if *every* package we believed was
    # missing says this, we did not publish anything and the caller has to decide whether that
    # was a reused tag. See the tally after the push loop.
    if printf '%s' "$output" | grep -qiE '409|already exists'; then
      echo "    $name is already on the feed — counting it as pushed."
      conflicts=$((conflicts + 1))
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

echo "Checking what is already on the feed for $VERSION"

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
  echo "::error::Every package is already published at $VERSION. The feed does allow a version to be deleted, but re-pushing the same number would leave whoever already restored it holding a different package under the same version — tag a new version instead."
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

# The pre-flight check above is the first line of defence against a reused tag, but it can be
# wrong: if the feed is unreachable it reports everything as missing, and a version that is
# entirely published would then be pushed, 409 on every package, and finish green — which is
# exactly the "reported success for a push that never happened" outcome this script exists to
# prevent.
#
# So the same question gets asked again from what actually happened. Every package we believed
# was missing came back already-published, and we had not knowingly published any of them
# earlier in this version: nothing here was new.
if [ "$conflicts" -eq "${#missing[@]}" ] && [ "${#present[@]}" -eq 0 ]; then
  echo "::error::Nothing was published. Every package was already on the feed at $VERSION, which the pre-flight check did not see — most likely the feed was unreachable when it ran. Tag a new version rather than reusing this one."
  exit 1
fi

if [ "$conflicts" -gt 0 ]; then
  echo "$conflicts package(s) were already published and were not pushed again."
fi

echo "All packages are on the feed at $VERSION."
