#!/usr/bin/env python3
"""The risk tier of a pull request from the paths it changes (DevInfra-0f5.1, docs/review-risk-tiers.md).

  risk-tier.py --rules .github/risk-tiers.txt [--author LOGIN] [--title TITLE] < changed-files
      prints `tier=low|medium|high`, then each file with its tier and the rule that decided it
  risk-tier.py --rules .github/risk-tiers.txt --examples .github/risk-tiers.examples
      checks every `<tier> <path>` line of the examples file; exits 1 on a mismatch

A rules file has one `<tier> <glob>` per line (# starts a comment). A file takes the first tier, in the order
high, medium, low, that has a glob matching it, and medium when none does. A pull request takes the highest
tier among its files, so it is low only when every file is. `**` crosses directories, `*` and `?` do not.
A Dependabot bump that stays within a major version of 1 or more and touches only package manifests and
lockfiles is low, whatever the rules say. The rules and examples files themselves are always high, so a pull request
cannot lower its own tier by editing them; this rule lives here, in DevInfra, where no caller's pull request reaches.
"""
import argparse
import re
import sys

TIERS = ["low", "medium", "high"]
DEPENDENCY_BOTS = {"dependabot[bot]", "app/dependabot"}
ALWAYS_HIGH = {".github/risk-tiers.txt": "the rules themselves", ".github/risk-tiers.examples": "the rules' examples"}
MANIFEST = re.compile(
    r"(^|/)(package(-lock)?\.json|npm-shrinkwrap\.json|yarn\.lock|pnpm-lock\.yaml|packages\.lock\.json"
    r"|Directory\.Packages\.props|[^/]+\.csproj|go\.(mod|sum)|requirements[^/]*\.txt)$")
DEPENDABOT_VERSIONS = re.compile(r"\bfrom v?(\d+)\.\S* to v?(\d+)\.\S*")


def glob_regex(glob):
    out, i = [], 0
    while i < len(glob):
        if glob.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif glob.startswith("**", i):
            out.append(".*")
            i += 2
        elif glob[i] == "*":
            out.append("[^/]*")
            i += 1
        elif glob[i] == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(glob[i]))
            i += 1
    return re.compile("^" + "".join(out) + "$")


def load_rules(text):
    rules = {tier: [] for tier in TIERS}
    for number, raw in enumerate(text.splitlines(), 1):
        line = raw.split("#", 1)[0].strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) != 2 or parts[0] not in TIERS:
            raise ValueError(f"line {number}: expected '<low|medium|high> <glob>', got {raw!r}")
        if parts[1].startswith(("./", "/")):
            raise ValueError(f"line {number}: a glob is relative to the repository root, without ./ or /: {raw!r}")
        rules[parts[0]].append((parts[1], glob_regex(parts[1])))
    return rules


def file_tier(path, rules):
    """(tier, the glob that decided it, or None for the default)."""
    if path in ALWAYS_HIGH:
        return "high", ALWAYS_HIGH[path]
    for tier in ("high", "medium", "low"):
        for glob, pattern in rules[tier]:
            if pattern.match(path):
                return tier, glob
    return "medium", None


def is_minor_dependency_bump(files, author, title):
    if author not in DEPENDENCY_BOTS or not files or not all(MANIFEST.search(f) for f in files):
        return False
    match = DEPENDABOT_VERSIONS.search(title)
    # 0.x makes no compatibility promise between minors, so only a bump within major 1 or later counts.
    return bool(match) and match.group(1) == match.group(2) and int(match.group(1)) >= 1


def pr_tier(files, rules, author="", title=""):
    """(tier, [(path, tier, glob)])."""
    decided = [(path, *file_tier(path, rules)) for path in files]
    if is_minor_dependency_bump(files, author, title):
        return "low", decided
    if not decided:
        return "medium", decided
    return max((tier for _, tier, _ in decided), key=TIERS.index), decided


def check_examples(text, rules):
    failures = []
    for number, raw in enumerate(text.splitlines(), 1):
        line = raw.split("#", 1)[0].strip()
        if not line:
            continue
        parts = line.split(None, 1)
        if len(parts) != 2 or parts[0] not in TIERS:
            failures.append(f"line {number}: expected '<low|medium|high> <path>', got {raw!r}")
            continue
        expected, path = parts
        actual, glob = file_tier(path, rules)
        if actual != expected:
            failures.append(f"line {number}: {path} is {actual} ({glob or 'no rule'}), expected {expected}")
    return failures


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--rules", required=True)
    parser.add_argument("--examples")
    parser.add_argument("--author", default="")
    parser.add_argument("--title", default="")
    args = parser.parse_args(argv)
    with open(args.rules, encoding="utf-8") as handle:
        rules = load_rules(handle.read())
    if args.examples:
        with open(args.examples, encoding="utf-8") as handle:
            failures = check_examples(handle.read(), rules)
        for failure in failures:
            print(f"::error::{failure}")
        return 1 if failures else 0
    files = [line.strip() for line in sys.stdin if line.strip()]
    tier, decided = pr_tier(files, rules, args.author, args.title)
    print(f"tier={tier}")
    for path, file_tier_, glob in decided:
        print(f"{file_tier_:6} {path}  ({glob or 'no rule: medium'})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
