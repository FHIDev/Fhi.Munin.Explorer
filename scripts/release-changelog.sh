#!/usr/bin/env bash
#
# Assembles the changelog for a release tag and puts it on a branch, ready for a pull request.
#
# Called by .github/workflows/release.yml before it packs, because the package's release notes
# are the assembled section and a package cannot point at notes that do not exist yet. The same
# invocation works locally, which is the only way to rehearse a release without spending a
# version number on the feed:
#
#   scripts/release-changelog.sh 0.2.0 --dry-run
#
# WHY A BRANCH AND NOT MAIN. The MainRules ruleset (checked 2026-09-04) requires a pull request
# for refs/heads/main, lists seven required status checks, and has no bypass actors - so an
# unattended push is refused whoever makes it, and a credential that could bypass it is one this
# public repository deliberately does not hold. The workflow therefore prepares the commit and
# opens the pull request; merging it is the one human step left, and nothing has to be remembered
# for a release to carry its notes.
#
# WHY THE SECTION IS ASSEMBLED ONTO main AND NOT ONTO THE TAG. The tag's own tree predates every
# assembly commit, so a section written on top of it and offered to main would revert the sections
# already released. Base is main; the fragments consumed are the ones the TAG has, so a fragment
# merged after the tag was cut waits for the next release rather than being published under a
# version that never contained it.
#
# Usage:
#   scripts/release-changelog.sh <version> [options]
#
#   --tag-sha SHA   commit being released (default HEAD)
#   --base REF      branch the section is assembled onto (default origin/main)
#   --branch NAME   branch to write it to (default changelog/v<version>)
#   --notes FILE    write the section body here, for PackageReleaseNotes and the GitHub release
#   --remote NAME   remote to push the branch to (default origin)
#   --dry-run       assemble and report, commit and push nothing

set -euo pipefail

VERSION=""
TAG_SHA="HEAD"
BASE="origin/main"
BRANCH=""
NOTES=""
REMOTE="origin"
DRY_RUN=0

while [ $# -gt 0 ]; do
  case "$1" in
    --tag-sha) TAG_SHA="$2"; shift 2 ;;
    --base)    BASE="$2"; shift 2 ;;
    --branch)  BRANCH="$2"; shift 2 ;;
    --notes)   NOTES="$2"; shift 2 ;;
    --remote)  REMOTE="$2"; shift 2 ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) sed -n '2,40p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*)        echo "Unknown option: $1" >&2; exit 2 ;;
    *)         [ -z "$VERSION" ] || { echo "Unexpected argument: $1" >&2; exit 2; }
               VERSION="$1"; shift ;;
  esac
done

if [ -z "$VERSION" ]; then
  echo "Usage: scripts/release-changelog.sh <version> [--tag-sha SHA] [--base REF] [--notes FILE] [--dry-run]" >&2
  exit 2
fi
if ! echo "$VERSION" | grep -Eq '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.]+)?$'; then
  echo "'$VERSION' is not a version this repository can release. Expected MAJOR.MINOR.PATCH, optionally with a prerelease suffix." >&2
  exit 2
fi

command -v pwsh >/dev/null 2>&1 || { echo "pwsh is not on PATH — install PowerShell 7." >&2; exit 2; }

cd "$(git rev-parse --show-toplevel)"

BRANCH="${BRANCH:-changelog/v$VERSION}"
TAG_SHA="$(git rev-parse --verify "${TAG_SHA}^{commit}")"
BASE_SHA="$(git rev-parse --verify "${BASE}^{commit}")"

# The date is the tag commit's, not today's. A re-run days later then assembles byte for byte
# what the first run did, which is what makes re-running safe rather than merely allowed.
DATE="$(git show -s --format=%cs "$TAG_SHA")"

if ! git merge-base --is-ancestor "$TAG_SHA" "$BASE_SHA"; then
  echo "::error::$TAG_SHA is not an ancestor of $BASE ($BASE_SHA), so its changelog cannot be assembled onto it. Merge the commit first, then tag the merge." >&2
  exit 1
fi

# Where to come back to, so a caller that has more to do with this checkout - release.yml packs
# the tag after this - finds it where it left it.
ORIGINAL_HEAD="$(git symbolic-ref -q --short HEAD || git rev-parse HEAD)"
HOLD="$(mktemp -d)"
restore() {
  # shellcheck disable=SC2317
  git checkout -q -f "$ORIGINAL_HEAD" 2>/dev/null || true
  rm -rf "$HOLD"
}
trap restore EXIT

git checkout -q -B "$BRANCH" "$BASE_SHA"

# Fragments that reached main after the tag was cut belong to the next release, not this one.
held=0
while IFS= read -r fragment; do
  [ -n "$fragment" ] || continue
  if ! git cat-file -e "$TAG_SHA:$fragment" 2>/dev/null; then
    mv "$fragment" "$HOLD/"
    held=$((held + 1))
  fi
done < <(find changelog.d -maxdepth 1 -name '*.md' ! -name 'README.md' | sort)
[ "$held" = "0" ] || echo "Held back $held fragment(s) added after $TAG_SHA — they belong to the next release."

ASSEMBLE=(pwsh ./scripts/assemble-changelog.ps1 -Version "$VERSION" -Date "$DATE")
[ -z "$NOTES" ] || ASSEMBLE+=(-NotesOutFile "$NOTES")
"${ASSEMBLE[@]}"

# Restored before the commit, so a held-back fragment is never part of it.
if [ "$held" != "0" ]; then mv "$HOLD"/*.md changelog.d/; fi

assembled=false
if git diff --quiet -- CHANGELOG.md changelog.d; then
  echo "Nothing to record: CHANGELOG.md on $BASE already has the section for $VERSION, or there was nothing to release."
else
  assembled=true
  git add -- CHANGELOG.md changelog.d
  if [ "$DRY_RUN" = "1" ]; then
    echo
    echo "Dry run — this is the commit that would go on $BRANCH:"
    git -c color.ui=never diff --cached --stat
  else
    git -c "user.name=${GIT_COMMITTER_NAME:-github-actions[bot]}" \
        -c "user.email=${GIT_COMMITTER_EMAIL:-41898282+github-actions[bot]@users.noreply.github.com}" \
        commit -q -m "docs(changelog): assemble v$VERSION" \
        -m "Written by .github/workflows/release.yml when v$VERSION was tagged: the section for this version, and the fragments it consumed removed in the same commit." \
        -m "Bead: Fhi.Metadata-l9l2n.44"
    # Force, because a re-run of the tag rebuilds this branch from whatever main is by then. The
    # branch is the workflow's own; the ruleset protects main, which this never pushes to.
    git push -q --force "$REMOTE" "HEAD:refs/heads/$BRANCH"
    echo "Pushed $BRANCH: $(git rev-parse --short HEAD)"
  fi
fi

echo "assembled=$assembled"
echo "branch=$BRANCH"
if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    echo "assembled=$assembled"
    echo "branch=$BRANCH"
  } >> "$GITHUB_OUTPUT"
fi
