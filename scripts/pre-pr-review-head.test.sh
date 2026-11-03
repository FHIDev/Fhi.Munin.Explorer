#!/usr/bin/env bash
# The head step of .github/workflows/pre-pr-review-line.yml (DevInfra-0f5.8) against a fake gh: one case per rule.
#   bash scripts/pre-pr-review-head.test.sh
# The step is cut out of the workflow as it stands; the fake answers each API path from a JSON file per case.
set -u
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

awk '/- name: The last review round/ { f = 1 } f && /^        run: \|/ { r = 1; next }
     r && /^          / { print substr($0, 11); next } r && /^ *$/ { print ""; next } r { exit }' \
  "$ROOT/.github/workflows/pre-pr-review-line.yml" > "$WORK/step.sh"
[ -s "$WORK/step.sh" ] || { echo "FAIL: could not cut the head step out of the workflow"; exit 1; }

mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'PY'
#!/usr/bin/env python3
import json, os, subprocess, sys
args = sys.argv[1:]
assert args[0] == "api", args
path, jq, raw = args[1].split("?")[0], None, False
i = 2
while i < len(args):
    if args[i] == "--jq": jq = args[i + 1]; i += 2
    elif args[i] == "-H": raw = "raw" in args[i + 1]; i += 2
    else: i += 1
world = json.load(open(os.environ["FAKE_WORLD"]))
if path not in world:
    sys.stderr.write(f"gh: Not Found (HTTP 404) {path}\n"); sys.exit(1)
val = world[path]
if isinstance(val, dict) and "__http" in val:
    sys.stderr.write(f"gh: Server Error (HTTP {val['__http']})\n"); sys.exit(1)
if raw:
    sys.stdout.write(val); sys.exit(0)
if jq is None:
    print(json.dumps(val)); sys.exit(0)
out = subprocess.run(["jq", "-r", jq], input=json.dumps(val), capture_output=True, text=True)
sys.stdout.write(out.stdout); sys.stderr.write(out.stderr); sys.exit(out.returncode)
PY
chmod +x "$WORK/bin/gh"

# Builds the world for one case. R is the round's head; the main commit M0 is on the base branch.
cat > "$WORK/world.py" <<'PY'
import json, sys
case, out = sys.argv[1], sys.argv[2]
R, X, Y, M, P = (c * 40 for c in "abcde")
SECRET = "SUBJECTMARK-ghp_notarealtoken"
def f(name, patch="@@ -1 +1 @@\n-a\n+b", status="modified", prev=None):
    d = {"filename": name, "status": status, "patch": patch}
    if prev: d["previous_filename"] = prev
    if patch is None: del d["patch"]
    return d
def own(*cs): return [{"sha": s, "parents": [{"sha": p} for p in ps], "commit": {"message": m}} for s, ps, m in cs]
code = [f("src/a.cs")]
more = [f("src/a.cs", "@@ -1 +1,2 @@\n-a\n+b\n+c")]
w = {"repos/o/r/contents/.github/risk-tiers.txt": "high   roller/**\n# Files that steer agents\nmedium lanes/**\n\nmedium src/**\n"}
head = R
exempt = SECRET + "\n\nReview-exempt: nit\n"
def after(sha, files, msg, stats=3):
    w[f"repos/o/r/compare/{R}...{sha}"] = {"status": "ahead"}
    c = {"files": files}
    if stats is not None: c["stats"] = {"additions": stats, "deletions": 0}
    w[f"repos/o/r/commits/{sha}"] = c
w[f"repos/o/r/compare/main...{R}"] = {"files": code}
if case == "covered":
    hs = R
    w[f"repos/o/r/pulls/1/commits"] = own((R, [P], "x"))
elif case == "covered-moved":
    # Main moved the hunk down five lines: the same change, other @@ numbers.
    hs = X
    w[f"repos/o/r/compare/main...{R}"] = {"files": [f("src/a.cs", "@@ -1 +1 @@\n-a\n+b\n@@ -9 +9 @@ M()\n-c\n+d")]}
    w[f"repos/o/r/compare/main...{X}"] = {"files": [f("src/a.cs", "@@ -6 +6 @@\n-a\n+b\n@@ -14 +14 @@ M()\n-c\n+d")]}
    w[f"repos/o/r/pulls/1/commits"] = own((R, [P], "x"), (X, [R], SECRET))
elif case == "docs-differ":
    hs = X
    w[f"repos/o/r/compare/main...{X}"] = {"files": code + [f("docs/x.md")]}
    w[f"repos/o/r/pulls/1/commits"] = own((R, [P], "x"), (X, [R], SECRET))
elif case == "read-fails":
    hs = X
    w[f"repos/o/r/compare/main...{R}"] = {"__http": 500}
    w[f"repos/o/r/compare/main...{X}"] = {"__http": 500}
elif case == "head-incomplete":
    # The round read the head itself: covered even when patches are missing and the commit list is too long.
    hs = R
    w[f"repos/o/r/compare/main...{R}"] = {"files": [f("img/x.png", None)] * 300}
    w[f"repos/o/r/pulls/1/commits"] = own(*[(c * 40, [P], "x") for c in "0123456789"] * 25)
elif case == "bad-head":
    head, hs = "abcSECRETHEAD", R
elif case.startswith("exempt") or case.startswith("plain") or case.startswith("docs"):
    hs = X
    files = {"exempt-ok": more, "exempt-big": more, "exempt-high": [f("roller/x.sh")],
             "exempt-rename": [f("src/b.cs", None, "renamed", "src/a.cs")], "exempt-agent": [f("AGENTS.md")],
             "exempt-nostats": more, "plain": more, "exempt-steer": [f("lanes/robin.md")],
             "exempt-renamed-edit": [f("src/b.cs", "@@ -1 +1 @@\n-a\n+b", "renamed", "src/a.cs")],
             "exempt-comments": more, "docs-nested-claude": [f("docs/x/CLAUDE.md")],
             "docs-agents-changelog": [f(".agents/CHANGELOG.md")], "docs-nested-dotclaude": [f("api/.claude/CHANGELOG.md")],
             "docs-plugin-changelog": [f(".claude-plugin/CHANGELOG.md")], "docs-dotlike": [f("src/x.claude/CHANGELOG.md")], "exempt-nested-agent": [f("api/.claude/x.md")],
             "exempt-dotagents": [f(".agents/skills/x.md")],
             "docs-claude-changelog": [f(".claude/agents/CHANGELOG.md")], "exempt-workflow": [f(".github/workflows/x.yml")], "docs-ok": [f("docs/x.md")],
             "docs-3000": [f(f"docs/{i}.md") for i in range(3000)]}[case]
    w[f"repos/o/r/compare/main...{X}"] = {"files": more if not case.startswith("docs") else code + [f("src/hidden.cs")]}
    msg = SECRET if case in ("plain", "docs-ok", "docs-3000", "docs-nested-claude", "docs-claude-changelog", "docs-agents-changelog", "docs-nested-dotclaude", "docs-plugin-changelog", "docs-dotlike") else exempt
    if case == "exempt-comments": msg = SECRET + "\n\nReview-exempt: comments\n"
    if case == "exempt-workflow": w.pop("repos/o/r/contents/.github/risk-tiers.txt")
    after(X, files, msg, {"exempt-big": 25, "exempt-nostats": None}.get(case, 3))
    w[f"repos/o/r/pulls/1/commits"] = own((R, [P], "x"), (X, [R], msg))
elif case == "two-exempt":
    hs = Y
    w[f"repos/o/r/compare/main...{Y}"] = {"files": more}
    after(X, more, exempt); after(Y, more, exempt)
    w[f"repos/o/r/pulls/1/commits"] = own((R, [P], "x"), (X, [R], exempt), (Y, [X], exempt))
elif case.startswith("merge"):
    # R, then a merge M of the main commit P, then a small exempt nit Y.
    hs = Y
    w[f"repos/o/r/compare/main...{Y}"] = {"files": more}
    mfiles = {"merge-ok": code, "merge-edit": more,
              "merge-into-docs": code + [f("docs/x.md", "@@ -0,0 +1 @@\n+k", "renamed", "roller/x.yaml")],
              "merge-side": code,
              "merge-moved": [f("src/a.cs", "@@ -40 +40 @@ void Other()\n-a\n+b")],
              "merge-octopus": code, "merge-docs-only": code + [f("docs/y.md")], "merge-nopatch": code + [f("img/x.png", None)]}[case]
    w[f"repos/o/r/compare/main...{M}"] = {"files": mfiles}
    w[f"repos/o/r/compare/main...{P}"] = {"status": "ahead" if case == "merge-side" else "behind"}
    w[f"repos/o/r/compare/{R}...{M}"] = {"status": "ahead"}
    after(Y, more, exempt)
    parents = [R, P, "f" * 40] if case == "merge-octopus" else [R, P]
    w[f"repos/o/r/compare/main...{'f' * 40}"] = {"status": "ahead"}
    w[f"repos/o/r/pulls/1/commits"] = own((R, [P], "x"), (M, parents, SECRET), (Y, [M], exempt))
w["head"], w["hs"] = head, hs
w[f"repos/o/r/compare/{R}...{hs}"] = w.get(f"repos/o/r/compare/{R}...{hs}", {"status": "ahead" if hs != R else "identical"})
json.dump(w, open(out, "w"))
PY

fails=0
check() {  # case, expected exit (0 or 1), a text the log must hold, a text the summary must hold or ''
  local c=$1 want=$2 text=$3 sum=$4 rc=0 head hs body
  python3 "$WORK/world.py" "$c" "$WORK/$c.json"
  head=$(jq -r .head "$WORK/$c.json"); hs=$(jq -r .hs "$WORK/$c.json")
  body=$(printf 'Pre-PR review: ran, no findings\n<!-- review-registration {"v":2,"rounds":[{"round":1,"head":"%s"}]} -->\n\xf0\x9f\xa4\x96 Generated with [Claude Code](https://claude.com/claude-code)\n' "$head")
  : > "$WORK/$c.sum"
  PATH="$WORK/bin:$PATH" FAKE_WORLD="$WORK/$c.json" BODY="$body" REPO=o/r PR=1 BASE_REF=main HEAD_SHA="$hs" \
    CREATED=2026-11-11T00:00:00Z HEAD_ROUNDS_FROM=2026-11-10T00:00:00Z GITHUB_STEP_SUMMARY="$WORK/$c.sum" \
    bash -e "$WORK/step.sh" > "$WORK/$c.log" 2>&1 || rc=$?
  local ok=yes
  [ "$rc" -eq "$want" ] || ok=no
  grep -qF -- "$text" "$WORK/$c.log" || ok=no
  [ -z "$sum" ] || grep -qF -- "$sum" "$WORK/$c.sum" || ok=no
  if grep -qE 'SUBJECTMARK|SECRETHEAD' "$WORK/$c.log" "$WORK/$c.sum"; then ok=no; echo "  $c: a commit subject or the head reached the output"; fi
  [ "$(grep -c '^### Review-exempt' "$WORK/$c.sum")" -le 1 ] || ok=no
  if [ "$c" = two-exempt ] && [ "$(grep -c '| exempt |' "$WORK/$c.sum")" -ne 2 ]; then ok=no; fi
  if [ "$ok" = yes ]; then echo "ok   $c"; else echo "FAIL $c (exit $rc, want $want)"; sed 's/^/     /' "$WORK/$c.log"; fails=$((fails + 1)); fi
}

check covered          0 "covered."                                  ''
check head-incomplete  0 "The last round read the head"             ''
check docs-agents-changelog 1 "came after the last round and changes more than docs" ''
check docs-nested-dotclaude 1 "came after the last round and changes more than docs" ''
check docs-plugin-changelog 1 "came after the last round and changes more than docs" ''
check exempt-nested-agent 1 "changes agent instructions (api/.claude/x.md)" "refused: changes agent instructions"
check exempt-dotagents 1 "changes agent instructions (.agents/skills/x.md)" "refused: changes agent instructions"
check docs-dotlike     0 "docs only."                                 ''
check bad-head         1 "is not a full commit id"                    ''
check exempt-ok        0 "declared exempt, 3 lines in 1 files."      "| exempt |"
check exempt-big       1 "changes 25 lines, more than 20"            "refused: changes 25 lines"
check exempt-high      1 "touches a high-risk path (roller/x.sh)"    "refused: touches a high-risk path"
check exempt-rename    1 "renames a file or changes one with no patch" "refused: renames a file"
check exempt-agent     1 "changes agent instructions (AGENTS.md)"    "refused: changes agent instructions"
check exempt-steer     1 "changes agent instructions (lanes/robin.md)" "refused: changes agent instructions"
check two-exempt       0 "Every commit after the last round is covered." "| exempt |"
check covered-moved    0 "covered."                                  ''
check docs-differ      0 "only docs changed"                          ''
check read-fails       1 "Reading GitHub failed"                      ''
check merge-moved      1 "changes the pull request's own lines outside docs" ''
check exempt-renamed-edit 1 "renames a file or changes one with no patch" "refused: renames a file"
check exempt-comments  0 "declared exempt, 3 lines in 1 files."      "| exempt |"
check exempt-workflow  1 "changes agent instructions (.github/workflows/x.yml)" "refused: changes agent instructions"
check merge-octopus    1 "which is not on main"                       ''
check merge-nopatch    1 "GitHub gave no complete patch to compare"   ''
check docs-claude-changelog 1 "came after the last round and changes more than docs" ''
check merge-docs-only  0 "no change of its own."                      ''
check docs-nested-claude    1 "came after the last round and changes more than docs" ''
check exempt-nostats   1 "has no line count"                          "refused: has no line count"
check plain            1 "came after the last round and changes more than docs" ''
check docs-ok          0 "docs only."                                 ''
check docs-3000        1 "came after the last round and changes more than docs" ''
check merge-ok         0 "no change of its own."                      ''
check merge-edit       1 "changes the pull request's own lines outside docs" ''
check merge-into-docs  1 "changes the pull request's own lines outside docs" ''
check merge-side       1 "which is not on main"                       ''

[ "$fails" -eq 0 ] || { echo "pre-pr-review-head: $fails failed"; exit 1; }
echo "pre-pr-review-head: OK"
