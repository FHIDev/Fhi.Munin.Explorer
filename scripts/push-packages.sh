#!/usr/bin/env bash
#
# Pushes the built package to the internal feed.
#
# This was three times longer when the explorer shipped as three packages. Most of that length was
# not about pushing: it was about the three of them going out together — dependency order so a
# consumer never met a package whose dependency had not arrived, a tally of which ones landed, and
# a recovery path for the state where some were published and some were not. One package cannot
# reach that state, so none of it is here any more.
#
# What is left is what is still true of one package:
#
#   * A version that has gone out is spent. The feed does allow one to be deleted, but anyone who
#     restored it keeps what they got, so pushing a different build under the same number gives two
#     builds one name. The run therefore refuses a version that is already on the feed rather than
#     quietly reporting success.
#
#   * A push can fail for reasons that have nothing to do with us. Those are retried.
#
#   * A push that fails with "already exists" is not a failure: it means our own attempt landed and
#     we did not see the answer. That is accepted, and reported as what it was.
#
# Usage:
#   NUGET_PUBLISH_TOKEN=<pat-or-entra-token> scripts/push-packages.sh <artifacts-dir> <version>
#
# Set NUGET_PUBLISH_DRY_RUN=1 to do everything except the push itself: authenticate, ask the feed
# what exists, check the file is there, and stop.
#
# That switch exists because I published to this shared feed twice by accident while checking this
# script — both times by picking a version I reasoned the pre-flight would refuse, and being wrong
# about why. The second time the version WAS on the feed, but under the old package ids, and the
# merged id had never been published. Reasoning about whether a push will be refused is not a
# safeguard. Not being able to push is.

set -euo pipefail

DIR="${1:?artifacts directory required}"
VERSION="${2:?version required}"
: "${NUGET_PUBLISH_TOKEN:?a credential for the feed must be in the environment}"

PACKAGE=Fhi.Munin.Explorer

# The internal feed, which is where helsedata's own packages come from and what their Optimizely
# project already restores from. Overridable so a fork or a dry run can point somewhere else.
FEED_BASE="${NUGET_PUBLISH_FEED:-https://fhi.pkgs.visualstudio.com/Fhi.Helsedata/_packaging/Fhi.Helsedata.no/nuget}"
SOURCE="$FEED_BASE/v3/index.json"

# The NuGet protocol's own "which versions of this package exist" endpoint. Asking the protocol
# rather than the host's API means this reads the same against any v3 feed.
FLAT="$FEED_BASE/v3/flat2"

SOURCE_NAME="helsedata-internal"
MAX_ATTEMPTS=5

# A config file of our own, and both commands are pointed at it.
#
# Registering into the default user config does not work here: this repository's own nuget.config
# starts with <clear />, so a source defined further up the chain is discarded before the push ever
# sees it, and --source by name fails to resolve from the repository root.
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

# Is this exact version already on the feed? Answers "no" when it cannot tell — a failed query must
# not be able to skip a push. The "already exists" handling below is what covers that case safely.
already_published() {
  local id_lower body code
  id_lower=$(echo "$PACKAGE" | tr '[:upper:]' '[:lower:]')

  body=$(mktemp)
  # -u with an empty username: the feed reads the password and ignores the rest. An unpublished
  # package answers 404 here, which is the "not yet" this reports.
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

echo "Checking whether $PACKAGE $VERSION is already on the feed"

if already_published; then
  echo "::error::$PACKAGE $VERSION is already published. The feed does allow a version to be deleted, but re-pushing the same number would leave whoever already restored it holding a different package under the same version — tag a new version instead."
  exit 1
fi

FILE="$DIR/$PACKAGE.$VERSION.nupkg"

if [ ! -f "$FILE" ]; then
  echo "::error::$FILE was not built, so $VERSION cannot be published."
  exit 1
fi

if [ "${NUGET_PUBLISH_DRY_RUN:-}" = "1" ]; then
  echo "Dry run: everything up to the push succeeded, and the push was not attempted."
  echo "  would push: $FILE"
  echo "  to:         $SOURCE"
  exit 0
fi

attempt=1

while :; do
  set +e
  output=$(dotnet nuget push "$FILE" --source "$SOURCE_NAME" --api-key az --configfile "$CONFIG" 2>&1)
  status=$?
  set -e
  printf '%s\n' "$output"

  if [ "$status" -eq 0 ]; then
    echo "$PACKAGE $VERSION is on the feed."
    exit 0
  fi

  # Already there. The pre-flight said it was not, so this is almost certainly our own earlier
  # attempt landing without us seeing the answer. The version is in the state we wanted.
  #
  # A here-string for the reason spelled out over the same construct in
  # check-changelog-fragment.sh: a printf killed by SIGPIPE reports 141 under `pipefail`, which
  # here would read a 409 as some other failure and retry a push that had landed (Fhi.Metadata-v198s).
  if grep -qiE '409|already exists' <<< "$output"; then
    echo "$PACKAGE $VERSION was already on the feed — a previous attempt landed."
    exit 0
  fi

  if [ "$attempt" -ge "$MAX_ATTEMPTS" ]; then
    echo "::error::Giving up after $MAX_ATTEMPTS attempts. Re-running this workflow is safe: it refuses a version that is already out and otherwise pushes the one that is missing."
    exit 1
  fi

  echo "  attempt $attempt failed, retrying in $((attempt * 10))s"
  sleep $((attempt * 10))
  attempt=$((attempt + 1))
done
