#!/usr/bin/env bash
# Guard for sample-css-declarations.mjs, and specifically for its BORROWED half. That half is all
# asymmetries — missing-selector suppressed, invented-declaration and different-value kept, the
# `font` shorthand read for its size and not for its weight — and every one of them is a decision
# that looks like a bug to the next reader and can be "simplified" away without a single test going
# red. The baseline file is no substitute: it measures the stylesheets, not this engine, and a guard
# reporting zero divergences is indistinguishable from a perfect stylesheet.
#
# Fixtures rather than the real sheets, so this needs no Azure Artifacts credentials and runs in the
# uncredentialed sample-css job beside the guard it tests.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
engine="$here/sample-css-declarations.mjs"
fail=0

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

run () { # sample-css, stiler-css -> divergence keys on stdout, summary on stderr
  printf '%s' "$1" > "$tmp/sample.css"
  printf '%s' "$2" > "$tmp/stiler.css"
  node "$engine" "$tmp/sample.css" "$tmp/stiler.css" 2>"$tmp/summary"
}

reports () { # name, expected-key, sample, stiler
  local out
  out="$(run "$3" "$4")"
  if printf '%s\n' "$out" | grep -qxF -- "$2"; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n     wanted the line: %s\n     got:\n%s\n' "$1" "$2" "${out:-(nothing)}"
    fail=1
  fi
}

silent () { # name, sample, stiler
  local out
  out="$(run "$2" "$3")"
  if [ -z "$out" ]; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n     expected no divergence, got:\n%s\n' "$1" "$out"
    fail=1
  fi
}

# ---------------------------------------------------------------------------------------------
# The borrowed half exists at all. Deleting the `isBorrowed` clause from `isCompared` leaves the
# prefix half green and fails these three.

reports "an INVENTED declaration on a borrowed selector is reported" \
  'invented-declaration||.dropdown-choicepicker__item|white-space' \
  '.dropdown-choicepicker__item { padding: 4px 20px; white-space: nowrap; }' \
  '.dropdown-choicepicker__item { padding: 4px 20px; }'

reports "a WRONG VALUE on a borrowed selector is reported" \
  'different-value||.dropdown-choicepicker__item|padding' \
  '.dropdown-choicepicker__item { padding: 4px 20px; }' \
  '.dropdown-choicepicker__item { padding: 8px 24px; }'

reports "a borrowed declaration Stiler has and the sample omits is reported" \
  'missing-declaration||.searchbox__freetext|color' \
  '.searchbox__freetext { height: 56px; }' \
  '.searchbox__freetext { height: 56px; color: #262045; }'

# The asymmetry that pays for it: the sample stands in for what the component touches and no
# further, so a borrowed selector it never writes must not become a baseline line that can never
# go down. Under the prefix the same gap IS a hole, because the sample is the only stylesheet
# those names have here.

silent "a borrowed selector the sample never writes is NOT reported" \
  '.searchbox__freetext { height: 56px; }' \
  '.searchbox__freetext { height: 56px; } .form-control__label { color: #262045; }'

reports "a PREFIX selector the sample never writes still IS reported" \
  'missing-selector||.munin-explorer-detail|' \
  '.munin-explorer-trail { margin: 0; }' \
  '.munin-explorer-trail { margin: 0; } .munin-explorer-detail { padding: 8px; }'

# A selector only the SAMPLE has is compared in neither direction, whichever family it is in —
# the sample carries its own host chrome and its own palette. The closing banner's unmatched count
# is the only trace, which is why the count is asserted below rather than merely printed.

silent "a selector only the sample has is not reported" \
  '.hostbar { padding: 8px; } .munin-explorer-invented { margin: 0; }' \
  '.searchbox__freetext { height: 56px; }'

# ---------------------------------------------------------------------------------------------
# The `font` shorthand. Stiler's responsive type sets size and line-height through it, and `font`
# is not compared as a value, so a longhand beside it would read as invented against nothing. The
# fix reads the size back OUT of the shorthand, which means agreement is silent and disagreement
# is loud — the previous form dropped both.

silent "a longhand that AGREES with Stiler's font shorthand is silent" \
  '.hd-button-square { font-size: 0.875rem; }' \
  '.hd-button-square { font: normal 14px/160% Graphik, sans-serif; }'

reports "a longhand that CONTRADICTS Stiler's font shorthand is reported" \
  'different-value||.hd-button-square|font-size' \
  '.hd-button-square { font-size: 40px; }' \
  '.hd-button-square { font: normal 14px/160% Graphik, sans-serif; }'

silent "line-height is read out of the shorthand too, and agreement is silent" \
  '.hd-button-square { line-height: 160%; }' \
  '.hd-button-square { font: normal 14px/160% Graphik, sans-serif; }'

reports "a contradicting line-height is reported" \
  'different-value||.hd-button-square|line-height' \
  '.hd-button-square { line-height: 4; }' \
  '.hd-button-square { font: normal 14px/160% Graphik, sans-serif; }'

# The bare number in `font: normal 500 16px/1 x` is the WEIGHT, not the size. Reading it as a size
# would silence a real font-size divergence, which is the failure this whole section is about.
reports "a bare weight in the shorthand is not read as the size" \
  'different-value||.hd-button-square|font-size' \
  '.hd-button-square { font-size: 40px; }' \
  '.hd-button-square { font: normal 500 16px/1 Graphik, sans-serif; }'

# Only the size and the line-height are carried. Weight, style, variant and stretch are reset by
# omission and reading that off is a second CSS engine, so a longhand for one of them is compared
# against nothing and reported — which is how `.datasourcecard__heading { font-weight: 500 }`
# against Stiler's `font: normal 21px/160% graphik-medium` became visible.
reports "font-weight beside a shorthand is still reported" \
  'invented-declaration||.datasourcecard__heading|font-weight' \
  '.datasourcecard__heading { font-weight: 500; }' \
  '.datasourcecard__heading { font: normal 21px/160% Graphik, sans-serif; }'

# `font: inherit` carries no size, so a longhand beside it is compared against nothing.
reports "a shorthand with no size in it carries nothing" \
  'invented-declaration||.hd-button-reset|font-size' \
  '.hd-button-reset { font-size: 16px; }' \
  '.hd-button-reset { font: inherit; }'

reports "with no font shorthand at all, a longhand is invented as before" \
  'invented-declaration||.hd-button-square|font-size' \
  '.hd-button-square { font-size: 14px; }' \
  '.hd-button-square { height: 44px; }'

# Scoped to borrowed selectors, and this is the constraint the comment states and nothing enforced.
# The prefix half's baseline lines were each measured against the pinned package; widening the
# shorthand reading would retire some of them by side effect and unmeasured.
reports "the shorthand reading does NOT apply under the munin-explorer prefix" \
  'invented-declaration||.munin-explorer-detail|font-size' \
  '.munin-explorer-detail { font-size: 0.875rem; }' \
  '.munin-explorer-detail { font: normal 14px/160% Graphik, sans-serif; }'

# The reciprocal is deliberately NOT expanded: a longhand Stiler spells out and the sample folds
# into a `font` shorthand still reports, because a baseline line with a reason beats a rule nobody
# can see, and the sample can retire it by writing the longhands.
reports "a longhand Stiler spells out is still missing when the sample writes a shorthand" \
  'missing-declaration||.hd-button-square|font-size' \
  '.hd-button-square { font: normal 500 16px/1 system-ui; }' \
  '.hd-button-square { font-size: 16px; }'

# ---------------------------------------------------------------------------------------------
# The counts the shell half greps for. Each lives on its own line ending in the phrase that names
# it, because a greedy `.*` over two "Stiler rule(s)" phrases on one line decided which number the
# rule-count floor got.

summary_says () { # name, sed-expression, expected
  local got
  got="$(sed -n "$2" "$tmp/summary")"
  if [ "$got" = "$3" ]; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n     wanted %s, got "%s" from:\n%s\n' "$1" "$3" "$got" "$(cat "$tmp/summary")"
    fail=1
  fi
}

run '.hostbar { padding: 8px; }
     .hd-button-square.button-square--primary { color: #fff; }
     .munin-explorer-trail { margin: 0; }' \
    '.button-square--primary { color: #fff; }
     .searchbox__freetext { height: 56px; }
     .munin-explorer-trail { margin: 0; }' >/dev/null

summary_says "the PREFIX rule count is on its own line" \
  's/^# \([0-9]*\) Stiler rule(s) and .* under the prefix$/\1/p' 1
summary_says "the BORROWED rule count is on its own line" \
  's/^# \([0-9]*\) Stiler rule(s) and .* on borrowed class selectors$/\1/p' 2
# `.hostbar` is the sample host's own chrome and `.hd-button-square.button-square--primary` is a
# compound Stiler spells as `.button-square--primary`. Both are compared against nothing, and this
# number is the only thing that says so — missing-selector is off for borrowed names.
summary_says "borrowed rules that matched NOTHING are counted, not folded into the pass" \
  's/^# \([0-9]*\) borrowed sample rule(s) matched no Stiler selector.*$/\1/p' 2

if [ "$fail" = "0" ]; then
  echo "sample-css-declarations.mjs: every case above holds."
else
  echo "sample-css-declarations.mjs: see the failures above." >&2
fi
exit "$fail"
