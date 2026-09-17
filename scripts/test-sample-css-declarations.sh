#!/usr/bin/env bash
# Guard for sample-css-declarations.mjs, and above all for its BORROWED half. That half is all
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

only_of_kind () { # name, expected-key, sample, stiler: the key is the ONLY line of its kind
  local out got
  out="$(run "$3" "$4")"
  got="$(printf '%s\n' "$out" | grep "^${2%%|*}|" || true)"
  if [ "$got" = "$2" ]; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n     wanted only the line: %s\n     got:\n%s\n' "$1" "$2" "${out:-(nothing)}"
    fail=1
  fi
}

omits_kind () { # name, kind, sample, stiler
  local out
  out="$(run "$3" "$4")"
  if printf '%s\n' "$out" | grep -q "^$2|"; then
    printf '  FAIL  %s\n     wanted no %s line, got:\n%s\n' "$1" "$2" "$out"
    fail=1
  else
    printf '  ok    %s\n' "$1"
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

# A BORROWED selector only the SAMPLE has is compared in neither direction — the sample carries its
# own host chrome and its own palette. The closing banner's unmatched count is the only trace, which
# is why the count is asserted below rather than merely printed.

silent "a borrowed selector only the sample has is not reported" \
  '.hostbar { padding: 8px; }' \
  '.searchbox__freetext { height: 56px; }'

# ---------------------------------------------------------------------------------------------
# unstyled-name. Under the prefix a name only the sample styles renders at browser defaults on
# helsedata, whatever the stand-in draws for it.

reports "a munin-explorer name Stiler never mentions is reported" \
  'unstyled-name||.munin-explorer-invented|' \
  '.hostbar { padding: 8px; } .munin-explorer-invented { margin: 0; }' \
  '.searchbox__freetext { height: 56px; }'

# One line per name, so the Stiler bead that adds the rule deletes one line however many rules the
# sample wrote.
only_of_kind "an unstyled name is one line however many rules name it" \
  'unstyled-name||.munin-explorer-complete-record__fields|' \
  '.munin-explorer-complete-record__fields { display: grid; }
   .munin-explorer-complete-record__fields:hover { margin: 0; }' \
  '.munin-explorer-trail { margin: 0; }'

# A disclosure styled only through its summary still names the disclosure.
reports "a name the sample styles only as an ancestor is reported" \
  'unstyled-name||.munin-explorer-complete-record|' \
  '.munin-explorer-complete-record > summary { cursor: pointer; }' \
  '.munin-explorer-trail { margin: 0; }'

only_of_kind "every name in a selector is read, not just its first or last" \
  'unstyled-name||.munin-explorer-b|' \
  '.munin-explorer-a .munin-explorer-b .munin-explorer-c { margin: 0; }' \
  '.munin-explorer-a { margin: 0; } .munin-explorer-c { margin: 0; }'

omits_kind "a name Stiler styles under another selector spelling is not unstyled" \
  'unstyled-name' \
  '.munin-explorer-meta dl > dt { margin: 0; }' \
  '.munin-explorer-meta dt { margin: 0; }'

reports "a longer name in Stiler does not vouch for its stem" \
  'unstyled-name||.munin-explorer-page|' \
  '.munin-explorer-page { margin-top: 0; }' \
  '.munin-explorer-page__body { display: grid; }'

# ---------------------------------------------------------------------------------------------
# invented-selector (Fhi.Metadata-796cw). Once Stiler styles a name, a selector under it that only
# the sample spells is compared by no other kind, so each of its declarations is a line of its own.

reports "a declaration under a selector Stiler never spells, on a name it styles, is reported" \
  'invented-selector||.munin-explorer-meta kbd|letter-spacing' \
  '.munin-explorer-meta { padding: 0; } .munin-explorer-meta kbd { letter-spacing: 3px; }' \
  '.munin-explorer-meta { padding: 0; }'

for property in letter-spacing color; do
  reports "every declaration of such a rule is its own line: $property" \
    "invented-selector||.munin-explorer-meta kbd|$property" \
    '.munin-explorer-meta { padding: 0; } .munin-explorer-meta kbd { letter-spacing: 3px; color: red; }' \
    '.munin-explorer-meta { padding: 0; }'
done

reports "a prefixed selector with no class name in it is reported too" \
  'invented-selector||[data-host=munin-explorer] kbd|color' \
  '.munin-explorer-meta { padding: 0; } [data-host="munin-explorer"] kbd { color: red; }' \
  '.munin-explorer-meta { padding: 0; }'

reports "a selector Stiler spells only outside the sample's at-rule is still the sample's own" \
  'invented-selector|@media (max-width:767px)|.munin-explorer-meta|padding' \
  '@media (max-width: 767px) { .munin-explorer-meta { padding: 0; } }' \
  '.munin-explorer-meta { margin: 0; }'

omits_kind "a name Stiler never styles is left to unstyled-name" \
  'invented-selector' \
  '.munin-explorer-invented kbd { letter-spacing: 3px; }' \
  '.munin-explorer-meta { padding: 0; }'

omits_kind "a selector naming one unstyled name among styled ones is left to unstyled-name" \
  'invented-selector' \
  '.munin-explorer-meta .munin-explorer-invented { letter-spacing: 3px; }' \
  '.munin-explorer-meta { padding: 0; }'

silent "an empty block and an uncompared property under a sample-only selector report nothing" \
  '.munin-explorer-meta { padding: 0; } .munin-explorer-meta kbd {} .munin-explorer-meta b { font-family: serif; }' \
  '.munin-explorer-meta { padding: 0; }'

silent "a borrowed selector only the sample has is still not an invented-selector" \
  '.munin-explorer-meta { padding: 0; } .hd-button-square kbd { letter-spacing: 3px; }' \
  '.munin-explorer-meta { padding: 0; } .hd-button-square { height: 44px; }'

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

# ---------------------------------------------------------------------------------------------
# The shell half's unstyled-name advice. Fixtures clear its floor of 100 prefix rules; the advice
# must follow an unstyled-name and only that, or a declaration fix reads as a missing Stiler rule.

guard_advises () { # name, yes|no, extra sample css
  local floor="" i err
  for i in $(seq 1 100); do floor+=".munin-explorer-r$i { margin: 0; }"$'\n'; done
  printf '%s' "$floor" > "$tmp/stiler.css"
  printf '%s%s' "$floor" "$3" > "$tmp/sample.css"
  : > "$tmp/known.txt"
  err="$(STILER_MAIN_CSS="$tmp/stiler.css" SAMPLE_CSS_MODERN="$tmp/sample.css" SAMPLE_CSS_LEGACY="$tmp/sample.css" \
    KNOWN_DIVERGENCES="$tmp/known.txt" bash "$here/assert-sample-css-matches-stiler.sh" 2>&1 >/dev/null || true)"
  local said=no
  printf '%s\n' "$err" | grep -q "An unstyled-name is" && said=yes
  if [ "$said" = "$2" ] && printf '%s\n' "$err" | grep -q "new divergence"; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n     wanted advice=%s after a new divergence, got:\n%s\n' "$1" "$2" "$err"
    fail=1
  fi
}

guard_advises "the guard gives unstyled-name advice for an unstyled name" yes \
  '.munin-explorer-new { margin: 0; }'
guard_advises "the guard gives no unstyled-name advice for a declaration divergence" no \
  '.munin-explorer-r1 { padding: 0; }'

guard_says () { # name, extra sample css, baseline, expected exit, expected phrase
  local floor="" i err code
  for i in $(seq 1 100); do floor+=".munin-explorer-r$i { margin: 0; }"$'\n'; done
  printf '%s' "$floor" > "$tmp/stiler.css"
  printf '%s%s' "$floor" "$2" > "$tmp/sample.css"
  printf '%s\n' "$3" > "$tmp/known.txt"
  err="$(STILER_MAIN_CSS="$tmp/stiler.css" SAMPLE_CSS_MODERN="$tmp/sample.css" SAMPLE_CSS_LEGACY="$tmp/sample.css" \
    KNOWN_DIVERGENCES="$tmp/known.txt" bash "$here/assert-sample-css-matches-stiler.sh" 2>&1)" && code=0 || code=$?
  if [ "$code" = "$4" ] && printf '%s\n' "$err" | grep -qF -- "$5"; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s\n     wanted exit %s and "%s", got exit %s:\n%s\n' "$1" "$4" "$5" "$code" "$err"
    fail=1
  fi
}

guard_says "the guard fails on an unlisted invented-selector" \
  '.munin-explorer-r1 kbd { letter-spacing: 3px; }' '' 1 \
  'invented-selector||.munin-explorer-r1 kbd|letter-spacing'
guard_says "the guard passes once that invented-selector is listed" \
  '.munin-explorer-r1 kbd { letter-spacing: 3px; }' 'invented-selector||.munin-explorer-r1 kbd|letter-spacing' 0 \
  'from the 1 divergence(s) listed'

# An empty block draws nothing, so it neither needs a Stiler rule nor vouches for a name.
silent "an empty prefixed block in the sample is not unstyled" \
  '.munin-explorer-trail { margin: 0; } .munin-explorer-empty {}' \
  '.munin-explorer-trail { margin: 0; }'
reports "an empty Stiler block does not vouch for a name" \
  'unstyled-name||.munin-explorer-x|' \
  '.munin-explorer-x { margin: 0; }' \
  '.munin-explorer-x { }'

if [ "$fail" = "0" ]; then
  echo "sample-css-declarations.mjs: every case above holds."
else
  echo "sample-css-declarations.mjs: see the failures above." >&2
fi
exit "$fail"
