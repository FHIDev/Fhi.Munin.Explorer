#!/usr/bin/env bash
#
# Fails if the two sample hosts' stylesheets have drifted apart.
#
# samples/ModernHost/wwwroot/host.css and samples/LegacyHost/wwwroot/css/host.css are the same
# file twice. That is what makes the samples worth having in pairs: both hosts style the component
# with identical rules, so any difference a reader sees between them is a difference in the
# hosting model rather than in the CSS.
#
# Both files say so in their header comment, and for a while that was the only thing enforcing it.
# It did not hold: the ~76 lines styling the Data tab's kodeverk list and code table landed in
# LegacyHost's copy alone, so ModernHost rendered an unstyled list — which looks exactly like a
# bug in the component, and gives a reader checking the kodeverk work no way to tell the two
# apart. Nothing failed; the comment just stopped being true.
#
# Usage:
#   scripts/assert-sample-css-in-step.sh
#
# When this goes red, the fix is to copy whichever copy is correct over the other one. There is
# no merging to do: the files are not allowed to differ at all, not even in a comment naming the
# other file, which is why neither of them does.

set -uo pipefail

MODERN="samples/ModernHost/wwwroot/host.css"
LEGACY="samples/LegacyHost/wwwroot/css/host.css"

fail=0
for f in "$MODERN" "$LEGACY"; do
  if [ ! -f "$f" ]; then
    echo "::error::'$f' is missing. The sample hosts each need their own copy of the stylesheet." >&2
    fail=1
  fi
done
[ "$fail" = "0" ] || exit 1

if cmp -s "$MODERN" "$LEGACY"; then
  echo "Sample host stylesheets are in step ($(wc -l < "$MODERN" | tr -d ' ') lines)."
  exit 0
fi

echo "::error::The sample host stylesheets have drifted apart. Copy one over the other:" >&2
echo "  cp $LEGACY $MODERN     # or the other way round" >&2
echo "" >&2
diff -u "$MODERN" "$LEGACY" >&2
exit 1
