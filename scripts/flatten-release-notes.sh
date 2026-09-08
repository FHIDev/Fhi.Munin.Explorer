#!/usr/bin/env bash
# Flatten an assembled changelog section into the plain text a NuGet feed can show.
#
# The feed page renders PackageReleaseNotes as PLAIN TEXT, so the markdown that reads well
# in CHANGELOG.md and on the GitHub release arrives as literal ###, ** and backticks, wrapped
# mid-sentence. alpha.10 shipped 27654 characters of it. Only the package is flattened; the
# GitHub release keeps the markdown, which it renders.
#
# One line per entry, taken from the bolded lead every fragment starts with (changelog.d/README.md
# asks for a bolded title). A bullet without one falls back to its first sentence.
set -euo pipefail

IN="${1:?usage: flatten-release-notes.sh <release-notes.md>}"
[ -s "$IN" ] || { printf 'No changelog entry was assembled for this version.\n'; exit 0; }

awk '
  function flush(   line, lead) {
    if (buf == "") return
    line = buf
    # The bolded lead is the entry title. Prefer it; fall back to the first sentence.
    if (match(line, /\*\*[^*]+\*\*/)) {
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
  /^### / { flush(); cat = substr($0, 5); printf "\n%s\n", cat; next }
  /^- /   { flush(); buf = substr($0, 3); next }
  /^[[:space:]]+[^[:space:]]/ {                 # wrapped continuation of the current bullet
    if (buf != "") { line = $0; sub(/^[[:space:]]+/, "", line); buf = buf " " line }
    next
  }
  /^$/    { next }
  { flush() }
  END     { flush() }
' "$IN"
