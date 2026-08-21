#!/usr/bin/env bash
#
# Fails if a branch changes src/ without adding a changelog fragment.
#
# CHANGELOG.md is edited by every branch on the same few lines, which makes it the most
# reliable merge conflict in a repository. Fragments — one file per change in changelog.d/,
# assembled at release time — remove that conflict, but only if people actually write them,
# which is what this check is for. See changelog.d/README.md.
#
# The rule is deliberately narrow: only src/ triggers it. Changes to docs, samples, tests and
# CI are invisible to someone embedding the package, so they owe the changelog nothing.
#
# Usage:
#   scripts/check-changelog-fragment.sh [base-ref] [head-ref]
#
# Defaults to origin/main..HEAD, so it can be run locally before pushing. CI passes the PR's
# base and head SHAs explicitly.

set -euo pipefail

BASE_REF="${1:-origin/main}"
HEAD_REF="${2:-HEAD}"

if ! MERGE_BASE=$(git merge-base "$BASE_REF" "$HEAD_REF" 2>/dev/null); then
  echo "Cannot find a merge base for '$BASE_REF' and '$HEAD_REF'." >&2
  echo "Fetch the base branch first (git fetch origin main) and try again." >&2
  exit 2
fi

changed=$(git diff --name-only "$MERGE_BASE" "$HEAD_REF")

if ! printf '%s\n' "$changed" | grep -q '^src/'; then
  echo "No changes under src/ — no changelog fragment needed."
  exit 0
fi

# Added or modified: amending an existing fragment on a follow-up push is a legitimate way to
# satisfy this, and a bare rename is not.
#
# [^/]+ rather than .+ on purpose: assemble-changelog.ps1 reads changelog.d/*.md
# non-recursively, so a fragment in a subdirectory would satisfy this check and then be
# silently dropped at release time — the check would be actively misleading.
fragments=$(git diff --name-only --diff-filter=AM "$MERGE_BASE" "$HEAD_REF" \
  | grep -E '^changelog\.d/[^/]+\.md$' \
  | grep -v -x 'changelog.d/README.md' || true)

if [ -n "$fragments" ]; then
  echo "Changelog fragment found:"
  printf '%s\n' "$fragments" | sed 's/^/  /'

  # Existing is not the same as usable. assemble-changelog.ps1 reads the category from LINE 1 and
  # copies every other non-blank line into that section verbatim, so a fragment with a second
  # "category:" in it publishes that line as a bullet under the first category. That shipped once
  # already - xbynn-variable-view.md reached main with two, and this check waved it through
  # because it only ever asked whether a file existed.
  bad=0

  for fragment in $fragments; do
    # Deleted in this branch, or otherwise not on disk: nothing to read, and nothing to complain
    # about either.
    [ -f "$fragment" ] || continue

    first=$(head -n 1 "$fragment")
    count=$(grep -c '^category:' "$fragment" || true)

    case "$first" in
      "category: Added"|"category: Changed"|"category: Fixed"|"category: Security"|\
      "category: Deprecated"|"category: Removed"|"category: Notes for hosts") ;;
      *)
        echo "::error file=$fragment::Line 1 must be a category line, one of: Added, Changed, Fixed, Security, Deprecated, Removed, Notes for hosts. Found: $first"
        bad=1
        ;;
    esac

    if [ "$count" -gt 1 ]; then
      echo "::error file=$fragment::$count category lines. A fragment holds one category; the extra ones are published as literal bullets under the first. Split it into one file per category."
      bad=1
    fi
  done

  [ "$bad" = "0" ] || exit 1

  exit 0
fi

# One clear message beats a clever check. Everything needed to fix it is here.
cat >&2 <<'EOF'
This branch changes src/ but adds no changelog fragment.

Anything that changes src/ changes what a host embedding the package sees, so it needs one
line saying what changed and what the host must do about it.

Fix: create changelog.d/<slug>.md — for example changelog.d/Fhi.Metadata-abc12.md — with

    category: Added
    - What changed, written for someone embedding the package.

Category is one of: Added, Changed, Fixed, Security, Deprecated, Removed, Notes for hosts.
Full format and examples: changelog.d/README.md

If the change genuinely has nothing to tell a host, put [no-changelog] in the PR title.
EOF

exit 1
