// Reads two stylesheets and reports where the sample stand-in's DECLARATIONS disagree with
// Fhi.Helsedata.Stiler's, for the selectors the component's markup puts on a page: the ones under
// the `munin-explorer` prefix the package owns, and the BORROWED ones it wears from Stiler.
//
// The whole point is that it compares declarations and not selectors. `assert-sample-css-in-step.sh`
// asks whether a name has a rule declaring SOMETHING, which is a question the samples answered
// green while ~40 real divergences stood: a rule carrying half of Stiler's declarations, or the
// right property carrying the wrong value, passes it. So nothing below asks whether a selector
// exists except as a way of finding the declarations to compare.
//
// Driven by scripts/assert-sample-css-matches-stiler.sh, which resolves which Stiler to read.
// Invoked directly it takes two paths and prints the divergences it finds:
//
//   node scripts/sample-css-declarations.mjs <sample.css> <stiler-main.css> [--detail]
//
// Output is one divergence per line, in the baseline file's format:
//
//   <kind>|<at-rule context>|<selector>|<property>
//
// so a run can be reconciled against test/sample-css-known-divergences.txt by nothing cleverer
// than sort and comm — which is what the shell half does.

import { readFileSync } from "node:fs";

// ---------------------------------------------------------------------------------------------
// Parsing
//
// A hand-rolled tokeniser rather than a CSS library, for the reason the rest of this repository
// pins or avoids npm at check time: an unpinned resolve turns an unrelated pull request red on the
// day upstream publishes. The input is two files we can read, one machine-generated from scss and
// one hand-written in a single house style, and neither uses the parts of CSS this skips.
//
// What it handles: comments, strings, at-rules holding rules (`@media` and friends), and rule
// groups. What it does NOT handle is CSS nesting — `.a { .b { } }` — for the same reason
// assert-sample-css-in-step.sh does not: neither file nests today. A nested rule would arrive with
// the parent's declarations attached to a selector reading `color: red; & .b`, which is wrong
// loudly rather than quietly — the parent turns up as a missing selector.

function stripComments(css) {
  // Strings first, so a `/*` inside `content: "/*"` does not open a comment. Neither file does
  // that today, but the failure mode is silent truncation up to the next `*/`, which would delete
  // rules and report every one of them missing.
  let out = "";
  let i = 0;
  while (i < css.length) {
    const c = css[i];
    if (c === '"' || c === "'") {
      const quote = c;
      let j = i + 1;
      while (j < css.length && css[j] !== quote) {
        j += css[j] === "\\" ? 2 : 1;
      }
      out += css.slice(i, Math.min(j + 1, css.length));
      i = j + 1;
    } else if (c === "/" && css[i + 1] === "*") {
      const end = css.indexOf("*/", i + 2);
      out += " ";
      i = end === -1 ? css.length : end + 2;
    } else {
      out += c;
      i += 1;
    }
  }
  return out;
}

// Splits on a delimiter at nesting depth 0 — outside (), [] and quotes. Needed twice: for the
// commas in a selector group, where `:not(a, b)` must not split, and for the semicolons between
// declarations, where `url(data:...;base64,...)` must not.
function splitTopLevel(text, delimiter) {
  const parts = [];
  let depth = 0;
  let quote = null;
  let current = "";
  for (let i = 0; i < text.length; i += 1) {
    const c = text[i];
    if (quote) {
      current += c;
      if (c === "\\") {
        current += text[i + 1] ?? "";
        i += 1;
      } else if (c === quote) {
        quote = null;
      }
      continue;
    }
    if (c === '"' || c === "'") {
      quote = c;
      current += c;
      continue;
    }
    if (c === "(" || c === "[") depth += 1;
    if (c === ")" || c === "]") depth -= 1;
    if (c === delimiter && depth === 0) {
      parts.push(current);
      current = "";
      continue;
    }
    current += c;
  }
  parts.push(current);
  return parts;
}

// Every rule in the sheet, flattened: one entry per selector in a group, carrying the at-rule
// context it sits inside. `context` is "" at the top level.
function parseRules(css) {
  const rules = [];

  function walk(text, context) {
    let i = 0;
    let buffer = "";
    while (i < text.length) {
      const c = text[i];
      if (c === "{") {
        let depth = 1;
        let j = i + 1;
        let quote = null;
        while (j < text.length && depth > 0) {
          const d = text[j];
          if (quote) {
            if (d === "\\") j += 1;
            else if (d === quote) quote = null;
          } else if (d === '"' || d === "'") quote = d;
          else if (d === "{") depth += 1;
          else if (d === "}") depth -= 1;
          j += 1;
        }
        const head = buffer.trim();
        const body = text.slice(i + 1, j - 1);
        if (head.startsWith("@")) {
          // An at-rule holding RULES is recursed into. `@font-face` and `@keyframes` hold
          // declarations instead and their heads are not selectors, so they are dropped; neither
          // carries a munin-explorer name in either file.
          if (/^@(media|supports|container|layer)\b/i.test(head)) {
            walk(body, context ? `${context} ${normaliseContext(head)}` : normaliseContext(head));
          }
        } else if (head) {
          for (const selector of splitTopLevel(head, ",")) {
            const s = normaliseSelector(selector);
            if (s) rules.push({ context, selector: s, declarations: parseDeclarations(body) });
          }
        }
        buffer = "";
        i = j;
        continue;
      }
      buffer += c;
      i += 1;
    }
  }

  walk(stripComments(css), "");
  return rules;
}

function parseDeclarations(body) {
  const declarations = [];
  for (const chunk of splitTopLevel(body, ";")) {
    const text = chunk.trim();
    if (!text) continue;
    const colon = text.indexOf(":");
    if (colon === -1) continue;
    const property = text.slice(0, colon).trim().toLowerCase();
    const value = text.slice(colon + 1).trim();
    if (!property || !value) continue;
    declarations.push({ property, value });
  }
  return declarations;
}

// ---------------------------------------------------------------------------------------------
// Normalisation
//
// The two files say the same thing in different words on purpose, and every difference below is
// one of those rather than a divergence. Stiler's main.css is compiled from scss, so its lengths
// are absolute and its colours are `rgb()`; the sample is written by hand against a palette it
// declares itself, so its lengths are `rem` and its colours are `var(--token)`. Comparing raw text
// would report a couple of hundred of those and bury every real one.

// `@media all and (max-width: 1280px)` and `@media (max-width:1280px)` are the same query — `all`
// is the default media type — and Stiler's compiler emits the long form for some rules and the
// short one for others, so without this the same breakpoint arrives as two contexts and every rule
// under one of them reads as a whole missing block.
function normaliseContext(head) {
  return head
    .replace(/\s+/g, " ")
    .replace(/\s*:\s*/g, ":")
    .replace(/\(\s*/g, "(")
    .replace(/\s*\)/g, ")")
    .replace(/^@media\s+all\s+and\s+/i, "@media ")
    .trim()
    .toLowerCase();
}

// NOT lowercased: class names are case-sensitive and the package emits `__dataCollection`.
// Attribute values are unquoted, because Stiler compiles `[aria-disabled="true"]` to
// `[aria-disabled=true]` and the two select identically.
// The four level-2 pseudo-elements are spelled both ways in the wild and select identically; the
// sample writes `::after` and Stiler's compiler emits `:after`, so without this every one of them
// is a missing selector on one side and an invented one on the other.
function normaliseSelector(selector) {
  return selector
    .replace(/\s+/g, " ")
    .replace(/\s*([>+~])\s*/g, " $1 ")
    .replace(/\[\s*([^\]=]+?)\s*=\s*(["'])(.*?)\2\s*\]/g, "[$1=$3]")
    .replace(/\[\s*([^\]]*?)\s*\]/g, "[$1]")
    .replace(/(^|[^:]):(before|after|first-line|first-letter)\b/gi, "$1::$2")
    .trim();
}

const NAMED_COLOURS = new Map([
  ["white", "rgb(255,255,255)"],
  ["black", "rgb(0,0,0)"],
  ["red", "rgb(255,0,0)"],
  ["silver", "rgb(192,192,192)"],
  ["gray", "rgb(128,128,128)"],
  ["grey", "rgb(128,128,128)"],
]);

function hexToRgb(hex) {
  let h = hex.slice(1);
  if (h.length === 3 || h.length === 4) h = [...h].map((c) => c + c).join("");
  if (h.length !== 6 && h.length !== 8) return null;
  const n = (at) => parseInt(h.slice(at, at + 2), 16);
  const [r, g, b] = [n(0), n(2), n(4)];
  if (h.length === 6) return `rgb(${r},${g},${b})`;
  const a = Math.round((n(6) / 255) * 1000) / 1000;
  return `rgba(${r},${g},${b},${a})`;
}

// `var(--token)` against the custom properties the same file declares. The sample declares its
// palette in `:root` and reads it bare everywhere; Stiler's compiled sheet has the literal. Both
// sides are resolved the same way, so a token both files declare still compares by value.
function resolveVars(value, tokens, depth = 0) {
  if (depth > 8 || !value.includes("var(")) return value;
  const resolved = value.replace(/var\(\s*(--[A-Za-z0-9_-]+)\s*(?:,([^]*))?\)/g, (match, name, fallback) => {
    if (tokens.has(name)) return tokens.get(name);
    if (fallback !== undefined) return fallback.trim();
    return match;
  });
  return resolved === value ? value : resolveVars(resolved, tokens, depth + 1);
}

function trimNumber(n) {
  return String(Math.round(n * 1000) / 1000);
}

// `\203a` -> `›`. A CSS escape is one to six hex digits, optionally closed by a single space that
// is part of the escape and not part of the string.
function decodeCssEscapes(text) {
  return text.replace(/\\([0-9a-fA-F]{1,6})\s?/g, (_m, hex) => String.fromCodePoint(parseInt(hex, 16)));
}

function normaliseValue(rawValue, tokens) {
  let value = resolveVars(rawValue, tokens, 0).trim();

  // `!important` is part of what a declaration DOES — `display: none !important` is the whole
  // point of the header-row rule below 1280px — so it is kept, normalised in spelling and
  // position rather than dropped.
  let important = false;
  value = value.replace(/!\s*important\s*$/i, () => {
    important = true;
    return "";
  });

  value = value
    .replace(/\s+/g, " ")
    .replace(/\s*,\s*/g, ",")
    .replace(/\(\s+/g, "(")
    .replace(/\s+\)/g, ")")
    // `grid-row: 3 / span 99` and `3/span 99`, and `font: 14px/1.4` — the slash is a separator and
    // the space around it is never significant.
    .replace(/\s*\/\s*/g, "/")
    // A quoted string is compared by what it CONTAINS, so `content: "\203a"` and `content: "›"`
    // are one value: the sample writes the escape and Stiler's compiler emits the character.
    .replace(/(["'])(.*?)\1/g, (_m, _q, inner) => decodeCssEscapes(inner))
    .trim()
    .toLowerCase();

  value = value.replace(/#[0-9a-f]{3,8}\b/g, (hex) => hexToRgb(hex) ?? hex);
  value = value.replace(/\b[a-z]+\b/g, (word) => NAMED_COLOURS.get(word) ?? word);

  // Lengths to px, so `2rem` and `32px` are one value. 16px per rem is the browser default and
  // neither file changes the root font size: the sample's `:root` sets only custom properties, and
  // Stiler's own `html` rule sets no font-size. This also folds `0px`/`0rem`/`0%` to `0`, which is
  // how `margin: 0` and `margin: 0px` stop being a divergence.
  value = value.replace(/(-?\d*\.?\d+)(rem|em)\b/g, (_m, n) => `${trimNumber(parseFloat(n) * 16)}px`);
  value = value.replace(/(-?\d*\.?\d+)(px|%|s|ms|deg|fr|vh|vw)\b/g, (_m, n, unit) =>
    parseFloat(n) === 0 ? "0" : `${trimNumber(parseFloat(n))}${unit}`,
  );
  value = value.replace(/(^|[\s(,])\.(\d)/g, "$10.$2");
  value = value.replace(/\brgba?\(([^)]*)\)/g, (_m, inner) => {
    const parts = inner.split(",").map((p) => p.trim());
    if (parts.length === 4 && parseFloat(parts[3]) === 1) return `rgb(${parts.slice(0, 3).join(",")})`;
    return `rgb${parts.length === 4 ? "a" : ""}(${parts.join(",")})`;
  });

  return important ? `${value} !important` : value;
}

function customProperties(rules) {
  const tokens = new Map();
  for (const rule of rules) {
    for (const { property, value } of rule.declarations) {
      if (property.startsWith("--")) tokens.set(property, value);
    }
  }
  for (const [name, value] of tokens) tokens.set(name, resolveVars(value, tokens, 0));
  return tokens;
}

// ---------------------------------------------------------------------------------------------
// Comparison

// Two families of selector reach a page through this component, and the comparison below is
// asymmetric between them. A rule is OURS when its selector names something under the prefix the
// package owns; a COMPOUND selector counts, because
// `.munin-explorer .hd-button-square.button-square--ghost` is a rule Stiler wrote for the
// component and the sample owes it.
function isOurs(selector) {
  return /(^|[^A-Za-z0-9_-])munin-explorer/.test(selector);
}

// A BORROWED rule is one the sample writes for a class name that is Stiler's — the searchbox, the
// buttons, the choicepicker — and `compare` says which half of the comparison it gets. Element,
// universal and `:root` selectors are not borrowed rules: the sample restyles `body` and declares
// a palette of its own, and neither is a claim about what Stiler draws for the component.
function isBorrowed(selector) {
  return !isOurs(selector) && /\.[A-Za-z_-]/.test(selector);
}

function isCompared(selector) {
  return isOurs(selector) || isBorrowed(selector);
}

// Properties deliberately NOT compared, and why. Each is something the sample CANNOT reproduce
// rather than something it gets wrong, so comparing it would mean a permanent baseline entry that
// can never go down — which is the shape of a check nobody reads.
//
//   font-family  Stiler ships the Graphik typeface and this repository cannot redistribute it,
//                which the stand-in's own header says. Every font-family in the sample is a
//                stand-in by construction, so every one of them would be reported forever.
//   font         The shorthand, for the same reason: it carries a family.
//   src          `@font-face` only, and the fonts are not ours.
//
// EVERYTHING ELSE IS COMPARED: geometry, colour, spacing, display, position, z-index, overflow,
// borders, radii, shadows, transforms, transitions, content, and every custom property.
//
// Shorthands are compared AS WRITTEN and not expanded: `border: none` and `border-width: 0` are
// not reconciled, and `padding: 8px 0` does not answer for `padding-left: 8px`. Expanding them
// correctly is a second CSS engine. The consequence is that this can report a divergence where the
// two sheets say the same thing in different shorthands — it errs towards reporting rather than
// towards silence, and such a line is a legitimate baseline entry with a note saying so.
//
// SPECIFICITY AND SOURCE ORDER ARE NOT COMPARED either. Two rules can both be present, both
// declare the same property, and still draw differently because one wins the cascade — which is
// exactly Fhi.Metadata-cuo0e. What this catches of that is the selector TEXT differing, which is
// how cuo0e presents; what it does not catch is two identical selectors in a different order.
const NOT_COMPARED = new Set(["font-family", "font", "src"]);

const FONT_LONGHANDS = new Set([
  "font-size",
  "font-weight",
  "font-style",
  "font-variant",
  "font-stretch",
  "line-height",
]);
const EMPTY = new Set();

function index(rules) {
  const map = new Map();
  for (const rule of rules) {
    if (!isCompared(rule.selector)) continue;
    const key = `${rule.context}|${rule.selector}`;
    if (!map.has(key)) {
      map.set(key, { context: rule.context, selector: rule.selector, declarations: new Map() });
    }
    const entry = map.get(key);
    // Later wins, which is what the cascade does for two rules of equal specificity in one sheet.
    for (const { property, value } of rule.declarations) entry.declarations.set(property, value);
  }
  return map;
}

export function compare(samplePath, stilerPath) {
  const sampleRules = parseRules(readFileSync(samplePath, "utf8"));
  const stilerRules = parseRules(readFileSync(stilerPath, "utf8"));

  const sampleTokens = customProperties(sampleRules);
  const stilerTokens = customProperties(stilerRules);

  const sample = index(sampleRules);
  const stiler = index(stilerRules);

  const divergences = [];
  for (const [key, stilerRule] of stiler) {
    const sampleRule = sample.get(key);
    if (!sampleRule) {
      // A selector the sample simply does not write. Under the prefix that is a hole — the sample
      // is the only stylesheet those names have here. On a BORROWED selector it is not: the sample
      // stands in for the parts of the design system the component touches and no more, and
      // demanding it mirror all of `.hd-button-square` would be a baseline that can never go down.
      if (!isOurs(stilerRule.selector)) continue;
      divergences.push({
        kind: "missing-selector",
        context: stilerRule.context,
        selector: stilerRule.selector,
        property: "",
        detail: `Stiler declares ${stilerRule.declarations.size} property(ies) here; the sample has no rule for this selector at all.`,
      });
      continue;
    }
    for (const [property, stilerValue] of stilerRule.declarations) {
      if (NOT_COMPARED.has(property)) continue;
      const want = normaliseValue(stilerValue, stilerTokens);
      if (!sampleRule.declarations.has(property)) {
        divergences.push({
          kind: "missing-declaration",
          context: stilerRule.context,
          selector: stilerRule.selector,
          property,
          detail: `Stiler declares '${property}: ${want}'; the sample's rule for the same selector does not declare it.`,
        });
        continue;
      }
      const got = normaliseValue(sampleRule.declarations.get(property), sampleTokens);
      if (got !== want) {
        divergences.push({
          kind: "different-value",
          context: stilerRule.context,
          selector: stilerRule.selector,
          property,
          detail: `Stiler says '${property}: ${want}'; the sample says '${got}'.`,
        });
      }
    }
  }

  // The fourth kind, and the one a borrowed selector fails by: a declaration the sample INVENTS.
  // `.munin-explorer-detail`'s border-top is one — Stiler's `_detail.scss` says in as many words
  // that there is deliberately none there — and `.dropdown-choicepicker__item`'s `white-space:
  // nowrap` was another, which is what makes this the check the borrowed half needed
  // (Fhi.Metadata-l9l2n.105). A stand-in that draws something the real stylesheet does not is as
  // misleading as one that draws nothing.
  //
  // Reported only for selectors Stiler ALSO has. A whole selector the sample invents is not
  // reported: the sample carries its own palette and its own host chrome, and neither is a claim
  // about what Stiler draws.
  for (const [key, sampleRule] of sample) {
    const stilerRule = stiler.get(key);
    if (!stilerRule) continue;
    // `font` is not compared, so a longhand the sample spells out reads as invented wherever
    // Stiler sets it through the shorthand — nine such lines on Stiler's responsive type alone.
    // Borrowed selectors only: three baseline lines under the prefix are font longhands, and
    // suppressing those here would rewrite by side effect a list that is all hand edits.
    const shorthanded =
      isBorrowed(sampleRule.selector) && stilerRule.declarations.has("font") ? FONT_LONGHANDS : EMPTY;
    for (const [property] of sampleRule.declarations) {
      if (NOT_COMPARED.has(property)) continue;
      if (shorthanded.has(property)) continue;
      if (stilerRule.declarations.has(property)) continue;
      divergences.push({
        kind: "invented-declaration",
        context: sampleRule.context,
        selector: sampleRule.selector,
        property,
        detail: `The sample declares '${property}: ${normaliseValue(sampleRule.declarations.get(property), sampleTokens)}'; Stiler's rule for the same selector does not.`,
      });
    }
  }

  divergences.sort((a, b) => keyOf(a).localeCompare(keyOf(b)));

  // Counted apart: the shell half floors the PREFIX count to catch a parser gone stale, and the
  // couple of thousand borrowed rules Stiler carries would hide that inside one number.
  const ours = (map) => [...map.values()].filter((r) => isOurs(r.selector)).length;
  return {
    divergences,
    sampleRuleCount: ours(sample),
    stilerRuleCount: ours(stiler),
    sampleBorrowedCount: sample.size - ours(sample),
    stilerBorrowedCount: stiler.size - ours(stiler),
  };
}

export function keyOf(d) {
  return `${d.kind}|${d.context}|${d.selector}|${d.property}`;
}

const [samplePath, stilerPath, mode] = process.argv.slice(2);
if (samplePath && stilerPath) {
  const result = compare(samplePath, stilerPath);
  for (const d of result.divergences) {
    console.log(mode === "--detail" ? `${keyOf(d)}\t${d.detail}` : keyOf(d));
  }
  console.error(
    `# ${result.divergences.length} divergence(s) across ${result.stilerRuleCount} Stiler rule(s) and ${result.sampleRuleCount} sample rule(s) under the prefix, ` +
      `plus ${result.stilerBorrowedCount} Stiler rule(s) and ${result.sampleBorrowedCount} sample rule(s) on borrowed class selectors`,
  );
} else {
  console.error("usage: node scripts/sample-css-declarations.mjs <sample.css> <stiler-main.css> [--detail]");
  process.exit(2);
}
