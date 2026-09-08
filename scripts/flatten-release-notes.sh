#!/usr/bin/env bash
# Flatten an assembled changelog section into the plain text a NuGet feed can show.
#
# The feed page renders PackageReleaseNotes as PLAIN TEXT, so the markdown that reads well
# in CHANGELOG.md and on the GitHub release arrives as literal ###, ** and backticks, wrapped
# mid-sentence. alpha.10 shipped 27654 characters of it. Only the package is flattened; the
# GitHub release keeps the markdown, which it renders.
#
# One line per entry, taken from the bolded sentence each bullet opens with — changelog.d/README.md
# asks for one and says it is published alone. A bullet without one falls back to its first
# sentence, which is why an unbolded fragment still releases rather than failing the run.
set -euo pipefail

IN="${1:?usage: flatten-release-notes.sh <release-notes.md>}"
[ -s "$IN" ] || { printf 'No changelog entry was assembled for this version.\n'; exit 0; }

awk '
  function flush(   line, lead) {
    if (buf == "") return
    line = buf
    # The LEADING bold is the entry title. Anchored: bold later in a bullet that does not
    # open with one is emphasis inside a sentence, and lifting it would title the entry with
    # a fragment of its own middle.
    if (match(line, /^\*\*[^*]+\*\*/)) {
      lead = substr(line, RSTART + 2, RLENGTH - 4)
    } else {
      lead = line
      sub(/\. .*$/, "", lead)
    }
    gsub(/`/, "", lead)          # code spans
    gsub(/\*\*/, "", lead)       # any stray bold inside the lead
    gsub(/  +/, " ", lead)
    sub(/[[:space:]]+$/, "", lead)
    sub(/[.:,;]+$/, "", lead)
    if (lead != "") print "  * " lead
    buf = ""
  }
  # Blank line BETWEEN categories, never before the first — the notes are read from their
  # first line on the feed, and an empty one there reads as a formatting fault.
  /^### /  { flush(); if (seen++) printf "\n"; printf "%s\n", substr($0, 5); next }
  # Mirrors assemble-changelog.ps1, which TrimStart()s before matching: an indented bullet is
  # a bullet, not a continuation of the one above. [[:blank:]] rather than [ \t] because a
  # POSIX bracket reads that escape literally — it matches "t" and misses a real tab.
  /^[[:space:]]*[-*][[:blank:]]/ {
    flush()
    line = $0
    sub(/^[[:space:]]*[-*][[:blank:]]+/, "", line)
    buf = line
    next
  }
  /^$/     { next }
  {
    # Anything else is a continuation of the bullet being built: wrapped lines, indented or not.
    # Never dropped — a silently missing entry is the failure this script exists to avoid.
    line = $0
    sub(/^[[:space:]]+/, "", line)
    if (buf != "") { buf = buf " " line } else { print "  " line }
  }
  END            { flush() }
' "$IN"
