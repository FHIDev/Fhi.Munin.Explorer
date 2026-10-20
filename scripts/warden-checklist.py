#!/usr/bin/env python3
"""Print the warden rules that apply to this branch's diff, as the Forge's assay picks them.

Source of truth: Robin831/Forge internal/warden, RulesFile.FormatChecklistForDiffWithStats (rules.go),
ported at commit 67459514506b6d9ce6c969fe973b6c582227d823, plus Robin831/Forge#948 (ui and testing routing for C#)
and #950 (cap 60, the sources term). Identical copies live in FHIDev/Munin, Fhi.Munin.Explorer, Heimdall and
Fhi.Munin.DevInfra; change all four together.

Usage (on Windows use `py -3` instead of `python3`):
    python3 scripts/warden-checklist.py [--base origin/main] [--max-rules 60] [--all-rules]

The selection is a port of Robin831/Forge internal/warden (filter.go FilterRulesIndexed,
score.go scoreCandidates/selectRules, rules.go FormatChecklistForDiffWithSelection) and
the diff is chosen as assay does (warden.go): `git diff <base>...HEAD` when the branch has
commits ahead of the base, otherwise `git diff HEAD`. Keep it in step with the Forge when
that code changes; the Forge is the reference.

No dependencies beyond Python 3.9: PyYAML is used when installed, otherwise a reader for
the subset of YAML the Smelter writes.
"""

import argparse
import datetime
import json
import os
import re
import subprocess
import sys

RULES_FILE = ".forge/warden-rules.yaml"
DEFAULT_MAX_RULES = 60
MIN_PATTERN_WORD_HITS = 2
SPECIFICITY_WEIGHT = 1.0
PATTERN_WEIGHT = 0.7
RECENCY_WEIGHT = 0.5
SOURCES_WEIGHT = 0.3

CANONICAL_CATEGORIES = {"ui", "style", "testing", "error-handling", "security", "concurrency", "performance", "other"}


# --- rules file -----------------------------------------------------------------------

def _scalar(text):
    text = text.strip()
    if text == "":
        return ""
    if text.startswith('"'):
        return json.loads(text)
    if text.startswith("'"):
        return text[1:-1].replace("''", "'")
    if text.startswith("[") and text.endswith("]"):
        inner = text[1:-1].strip()
        return [_scalar(p) for p in inner.split(",")] if inner else []
    if re.fullmatch(r"-?\d+", text):
        return int(text)
    return text


def _fold(lines):
    out, para = [], []
    for line in lines:
        if line.strip() == "":
            out.append(" ".join(para))
            para = []
        else:
            para.append(line.strip())
    out.append(" ".join(para))
    return "\n".join(out).strip("\n")


def _literal(lines):
    body = list(lines)
    while body and body[-1] == "":
        body.pop()
    indents = [len(x) - len(x.lstrip(" ")) for x in body if x.strip()]
    cut = min(indents) if indents else 0
    return "\n".join(x[cut:] for x in body)


def _read_rules_minimal(text):
    rules, cur, key, block, list_key, style = [], None, None, None, None, None
    lines = text.splitlines()

    def finish_block():
        nonlocal block, key
        if block is not None and cur is not None:
            if style.startswith("|"):
                cur[key] = _literal(block) + ("" if style == "|-" else "\n")
            else:
                cur[key] = _fold([x.strip() for x in block])
        block = None

    for raw in lines:
        indent = len(raw) - len(raw.lstrip(" "))
        line = raw.strip()
        if block is not None:
            if line == "":
                block.append("")
                continue
            if indent > key_indent:
                block.append(raw if style.startswith("|") else line)
                continue
            finish_block()
        if line == "" or line.startswith("#"):
            continue
        if indent == 0:
            if line in ("rules: []", "rules: [ ]"):
                return []
            if line != "rules:":
                raise ValueError(f"unexpected top-level line: {line!r}")
            continue
        if line.startswith("- ") and list_key is not None and indent > key_indent:
            cur[list_key].append(_scalar(line[2:]))
            continue
        list_key = None
        if line.startswith("- "):
            cur = {}
            rules.append(cur)
            line = line[2:]
            indent += 2
        m = re.match(r"([A-Za-z_][\w-]*):(?:\s+(.*))?$", line)
        if not m or cur is None:
            raise ValueError(f"cannot read line: {raw!r}")
        key, value, key_indent = m.group(1), m.group(2), indent
        if value is None or value == "":
            cur[key] = []
            list_key = key
        elif value in (">-", ">", "|", "|-"):
            block, style = [], value
        else:
            cur[key] = _scalar(value)
    finish_block()
    return rules


def load_rules(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    try:
        import yaml  # type: ignore
    except ImportError:
        return _read_rules_minimal(text)
    data = yaml.safe_load(text) or {}
    return data.get("rules") or []


def _source_list(r):
    s = r.get("source")
    if s is None or s == "":
        return []
    return [str(x) for x in s] if isinstance(s, list) else [str(s)]


# --- doublestar.Match ------------------------------------------------------------------

_glob_cache = {}


def _segment_regex(seg):
    out, i = [], 0
    while i < len(seg):
        c = seg[i]
        if c == "*":
            while i + 1 < len(seg) and seg[i + 1] == "*":
                i += 1
            out.append("[^/]*")
        elif c == "?":
            out.append("[^/]")
        elif c == "[":
            j = seg.find("]", i + 1)
            if j == -1:
                raise ValueError("bad glob")
            body = seg[i + 1:j]
            if body.startswith(("!", "^")):
                body = "^" + body[1:]
            out.append("[" + body.replace("\\", "\\\\") + "]")
            i = j
        elif c == "{":
            depth, j = 1, i + 1
            while j < len(seg) and depth:
                depth += {"{": 1, "}": -1}.get(seg[j], 0)
                j += 1
            if depth:
                raise ValueError("bad glob")
            alts = _split_alternatives(seg[i + 1:j - 1])
            out.append("(?:" + "|".join(_glob_body(a) for a in alts) + ")")
            i = j - 1
        elif c == "\\" and i + 1 < len(seg):
            i += 1
            out.append(re.escape(seg[i]))
        else:
            out.append(re.escape(c))
        i += 1
    return "".join(out)


def _split_alternatives(s):
    parts, depth, cur = [], 0, []
    for c in s:
        if c == "," and depth == 0:
            parts.append("".join(cur))
            cur = []
            continue
        depth += {"{": 1, "}": -1}.get(c, 0)
        cur.append(c)
    parts.append("".join(cur))
    return parts


def _glob_body(pattern):
    segs = pattern.split("/")
    out = ""
    for n, seg in enumerate(segs):
        last = n == len(segs) - 1
        if seg == "**":
            if last:
                out = out[:-1] + "(?:/.*)?" if out.endswith("/") else out + ".*"
            else:
                out += "(?:[^/]+/)*"
            continue
        out += _segment_regex(seg) + ("" if last else "/")
    return out


def _expand_braces(pattern):
    i, depth, start = 0, 0, -1
    while i < len(pattern):
        c = pattern[i]
        if c == "\\":
            i += 2
            continue
        if c == "{":
            if depth == 0:
                start = i
            depth += 1
        elif c == "}" and depth:
            depth -= 1
            if depth == 0:
                head, tail = pattern[:start], pattern[i + 1:]
                out = []
                for alt in _split_alternatives(pattern[start + 1:i]):
                    out.extend(_expand_braces(head + alt + tail))
                return out
        i += 1
    if depth:
        raise ValueError("bad glob")
    return [pattern]


def glob_match(pattern, path):
    rx = _glob_cache.get(pattern)
    if rx is None:
        try:
            alts = _expand_braces(pattern)
            rx = re.compile("(?:" + "|".join(_glob_body(a) for a in alts) + r")\Z", re.S)
        except (ValueError, re.error):
            rx = False
        _glob_cache[pattern] = rx
    return bool(rx) and rx.match(path) is not None


# --- filter.go ------------------------------------------------------------------------

def categories_for_file(path):
    lower = path.lower()
    base = os.path.basename(lower)
    if lower.endswith("_test.tsx") or lower.endswith("tests.cs"):
        return {"testing", "other"}
    if lower.endswith(".cs"):
        cats = {"error-handling", "security", "concurrency", "performance", "style", "other"}
        # Blazor UI C# and test projects, as Forge #948 routes them.
        dirs = os.path.dirname(lower).replace("\\", "/").split("/")
        if lower.endswith(".razor.cs") or any(d in ("components", "pages", "shared") for d in dirs):
            cats.add("ui")
        orig = os.path.dirname(path).replace("\\", "/").split("/")
        if any(d.lower() in ("test", "tests") or d.lower().endswith(".tests") or (d.endswith("Tests") and len(d) > 5) for d in orig):
            cats.add("testing")
        return cats
    if lower.endswith(".tsx") or lower.endswith(".ts"):
        return set(CANONICAL_CATEGORIES)
    if lower.endswith(".css"):
        return {"ui", "style", "other"}
    if (lower.endswith((".yaml", ".yml", ".ps1")) or base == "dockerfile"
            or base.startswith("dockerfile.") or base.endswith(".dockerfile")):
        return {"other", "security"}
    return set(CANONICAL_CATEGORIES)


def aggregate_categories(changed):
    if not changed:
        return set(CANONICAL_CATEGORIES)
    out = set()
    for f in changed:
        out |= categories_for_file(f)
    return out


def match_path_glob(patterns, changed):
    return any(glob_match(p, f) for p in patterns for f in changed)


def extract_words(text):
    seen, out = set(), []
    for w in re.split(r"[^a-z0-9]+", (text or "").lower()):
        if len(w) >= 4 and w not in seen:
            seen.add(w)
            out.append(w)
    return out


def pattern_word_hits(pattern, diff_lower):
    words = extract_words(pattern)
    return sum(1 for w in words if w in diff_lower), len(words)


def pattern_grep_passes(hits, words):
    if words == 0:
        return True
    return hits >= min(MIN_PATTERN_WORD_HITS, words)


# --- score.go -------------------------------------------------------------------------

def _is_glob_meta(c):
    return c in "*?[]{}!"


def glob_weight(glob):
    glob = glob.strip()
    if not glob:
        return 0.0
    weight = 0.0
    for seg in glob.replace("\\", "/").split("/"):
        if not seg:
            continue
        if not any(_is_glob_meta(c) for c in seg):
            weight += 1
        elif any(not _is_glob_meta(c) and c != "." for c in seg):
            weight += 0.5
    return weight


def specificity(rule, changed):
    paths = rule.get("paths") or []
    if not paths:
        return 0.0
    best, broad = 0.0, 0
    for p in paths:
        if glob_weight(p) == 0:
            broad += 1
        if not any(glob_match(p, f) for f in changed):
            continue
        n = 1 - 1 / (1 + glob_weight(p))
        best = max(best, n)
    return best / (1 + broad)


def _parse_added(rule):
    try:
        return datetime.datetime.strptime(str(rule.get("added", "")).strip(), "%Y-%m-%d")
    except ValueError:
        return None


def _tie_key(rule):
    return "\x00".join([
        str(rule.get("id", "")), str(rule.get("category", "")), str(rule.get("pattern", "")),
        str(rule.get("check", "")), str(rule.get("added", "")),
        ",".join(rule.get("paths") or []), ",".join(_source_list(rule)),
    ]).encode("utf-8")


def _ns(delta):
    return (delta.days * 86400 + delta.seconds) * 1_000_000_000 + delta.microseconds * 1000


def sources_score(rule):
    """How many review comments taught the rule: 0 for one, 1 - 1/n for more (score.go sourcesScore)."""
    src = rule.get("source")
    n = len(src) if isinstance(src, list) else (1 if src else 0)
    return 0.0 if n <= 1 else 1 - 1 / n


def select(rules, diff, changed, max_rules, use_all):
    diff_lower = diff.lower()
    stats = {"total": len(rules), "paths": 0, "category": 0, "matched": 0, "emitted": 0}
    cands = []
    cats = aggregate_categories(changed)
    for i, r in enumerate(rules):
        if not use_all:
            if r.get("paths"):
                if not changed or not match_path_glob(r["paths"], changed):
                    continue
            stats["paths"] += 1
            if r.get("category") in CANONICAL_CATEGORIES and r["category"] not in cats:
                continue
            stats["category"] += 1
        h, w = pattern_word_hits(r.get("pattern", ""), diff_lower)
        if not use_all and not pattern_grep_passes(h, w):
            continue
        cands.append({"rule": r, "index": i, "hits": h, "words": w})
    if use_all:
        stats["paths"] = stats["category"] = len(rules)
    stats["matched"] = len(cands)

    for c in cands:
        c["spec"] = specificity(c["rule"], changed)
        c["pat"] = 0.5 if c["words"] <= 0 else c["hits"] / c["words"]
        c["added"] = _parse_added(c["rule"])
    dated = [c["added"] for c in cands if c["added"]]
    oldest, newest = (min(dated), max(dated)) if dated else (None, None)
    span = _ns(newest - oldest) if dated else 0
    for c in cands:
        if c["added"] is None:
            c["rec"] = 0.5
        elif span <= 0:
            c["rec"] = 0.5  # one shared date says nothing: neutral, as Forge #950 does
        else:
            c["rec"] = float(_ns(c["added"] - oldest)) / float(span)
        # Recency and sources together are capped at recency's weight (Forge #950 secondaryScore), so sources
        # settles near-ties but never lifts a broad rule past a scoped one.
        secondary = min(RECENCY_WEIGHT * c["rec"] + SOURCES_WEIGHT * sources_score(c["rule"]), RECENCY_WEIGHT)
        c["total"] = SPECIFICITY_WEIGHT * c["spec"] + PATTERN_WEIGHT * c["pat"] + secondary

    epoch = datetime.datetime(1, 1, 1)
    cands.sort(key=lambda c: (-c["total"], -_ns((c["added"] or epoch) - epoch), _tie_key(c["rule"])))
    if max_rules > 0:
        cands = cands[:max_rules]
    stats["emitted"] = len(cands)
    return cands, stats


# --- git (warden.go) ------------------------------------------------------------------

def git(*args):
    try:
        p = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    except FileNotFoundError:
        sys.exit("warden-checklist: git is not on PATH")
    return p.returncode, p.stdout


def branch_diff(base):
    rc, diff = git("diff", "HEAD")
    if rc != 0:
        rc, diff = git("diff")
        if rc != 0:
            sys.exit("warden-checklist: git diff failed; no checklist was produced")
    rc, log = git("log", "--oneline", f"{base}..HEAD")
    if rc == 0 and log.strip():
        rc, d = git("diff", f"{base}...HEAD")
        if rc == 0:
            diff = d
    return diff


def changed_files(diff):
    seen, out = set(), []
    for line in diff.splitlines():
        if not line.startswith("diff --git "):
            continue
        rest = line[len("diff --git "):]
        idx = rest.rfind(" b/")
        if idx == -1:
            continue
        p = rest[idx + 3:]
        if p and p not in seen:
            seen.add(p)
            out.append(p)
    return out


def main():
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--base", default="origin/main", help="branch to diff against (default origin/main)")
    ap.add_argument("--max-rules", type=int, default=DEFAULT_MAX_RULES,
                    help="cap, as assay's warden.max_rules_per_review (default 60; 0 = no cap)")
    ap.add_argument("--all-rules", action="store_true", help="skip the path/category/pattern filters (use_all_rules)")
    ap.add_argument("--rules", default=RULES_FILE, help=f"rules file (default {RULES_FILE})")
    ap.add_argument("--diff-file", help="read the diff from a file instead of git (for tests)")
    args = ap.parse_args()
    rules_path = os.path.abspath(args.rules) if args.rules != RULES_FILE else None

    if args.diff_file:
        if not os.path.exists(args.diff_file):
            sys.exit(f"warden-checklist: no diff file at {args.diff_file}")
        with open(args.diff_file, encoding="utf-8", errors="replace") as f:
            diff = f.read()
    else:
        rc, top = git("rev-parse", "--show-toplevel")
        if rc != 0:
            sys.exit("warden-checklist: not inside a git repository")
        os.chdir(top.strip())
        rc, _ = git("rev-parse", "--verify", "--quiet", f"{args.base}^{{commit}}")
        if rc != 0:
            sys.exit(f"warden-checklist: base {args.base!r} does not resolve; run `git fetch` or pass --base")
        diff = branch_diff(args.base)
    if rules_path is None:
        rc, top = git("rev-parse", "--show-toplevel")
        rules_path = os.path.join(top.strip(), RULES_FILE) if rc == 0 else RULES_FILE
    args.rules = rules_path
    if not os.path.exists(args.rules):
        sys.exit(f"warden-checklist: no rules file at {args.rules}")
    rules = load_rules(args.rules)
    changed = changed_files(diff)
    chosen, s = select(rules, diff, changed, args.max_rules, args.all_rules)

    for n, c in enumerate(chosen, 1):
        r = c["rule"]
        print(f"{n}. [ ] {r.get('id', '?')}: Check: {r.get('check', '')} (pattern: {r.get('pattern', '')})")
    cap = str(args.max_rules) if args.max_rules > 0 else "none"
    if args.all_rules:
        line = f"rules funnel: {s['total']} on file, filters bypassed (use_all_rules), {s['matched']} ranked, {s['emitted']} emitted (cap {cap})"
    else:
        line = (f"rules funnel: {s['total']} on file, {s['paths']} after paths, {s['category']} after category, "
                f"{s['matched']} matched, {s['emitted']} emitted (cap {cap})")
    if chosen:
        print()
    print(line)
    if not chosen:
        print("No warden rule applies to this diff.")


if __name__ == "__main__":
    main()
