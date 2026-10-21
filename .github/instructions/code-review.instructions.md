---
applyTo: "**"
excludeAgent: "cloud-agent"
---

# Copilot Code Review Instructions

`REVIEW.md` at the repository root says what an Important finding is here, how to report (`file:line` evidence, at
most 5 Nits, a summary that starts with a count) and what not to report. Follow it.

## Intentional Patterns (Do Not Flag)

These are deliberate project decisions — do **not** suggest alternatives:

- **Bilingual text in `Texts.cs`** — both languages live in code; there are no `.resx` files
- **Stiler class names are external identifiers** — names such as `persident-trigger` come from `Fhi.Helsedata.Stiler` and are not typos
- **The comment budget** — comments stop at three lines (`AGENTS.md`); CI's `comment budget` job enforces it
- **Changelog fragments** — one English `changelog.d/<slug>.md` per change (`changelog.d/README.md`), not an en/nb pair

## Focus Review On

- **Accessibility** — labels, focus, keyboard use, contrast
- **Render-tree sequence numbers** — `RenderTreeBuilder` sequences must be literal and increase in source order across branches
- **Async UI state** — a handler that awaits and then writes state must not apply a stale result after sign-out, a reload, a language change or a newer request
- **The host contract** — `[Obsolete]` messages and public XML docs ship to hosts as IntelliSense; they must be true

## Do Not Comment On

- **Comment length or the three-line ceiling** — the `comment budget` job fails the PR when a comment is over it
- **Stiler class names as typos**
- **The bead-id format or bead citations inside comments**
- **Limits the data cannot reach** — for example the 100,000-variable export ceiling, against a registry of about 49,000
