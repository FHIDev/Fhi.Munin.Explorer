# Review guidance: Fhi.Munin.Explorer

Read by the pre-PR reviewer before it reviews a diff here, and by the independent review. The same
skeleton is used in Munin, Heimdall and DevInfra (DevInfra-0f5.2); each repository fills in its own failures.

## What failure looks like here

Explorer is a Razor component package that hosts (helsedata.no and others) embed. A finding is **Important** when
the change could:

- **Break a host.** A changed public type, parameter, class name or default that a host compiles against or
  styles, without a "Notes for hosts" fragment; a component that no longer mounts from the package alone.
- **Break geometry or accessibility.** A layout that overlaps, clips or scrolls at a width the geometry scans
  cover, a control without an accessible name, a focus trap, lost keyboard reach, contrast below WCAG AA, or a
  sticky bar that hides the focused control.
- **Drift from Stiler.** Styles that stop matching Fhi.Helsedata.Stiler, or sample stylesheets that fall out of
  step with each other.
- **Send the host's token somewhere else.** The bearer-token handler, the client's base address, or the token
  contracts.
- **Publish the wrong package.** The package csproj, `Directory.Build.props`, the release and packing scripts.
- **State what the code does not do.** A claim in the PR body, a changelog fragment or a doc the change touches, about
  behaviour, security, a command, a number, a file, a flag or an endpoint, that no code or test backs (the doc-claim
  check, DevInfra `docs/review-risk-tiers.md`). It is reported after the first review too, outside the nit cap.

Everything else (naming, wording, structure, a smaller way to write it) is a **Nit**.

## How to report

- **Evidence.** Every finding names `file:line` and the input, width or state that makes it go wrong. No finding
  without a place; "consider" with no concrete failure is not a finding.
- **Nit cap.** At most 5 Nits per review; more collapse into "(plus N similar)". After the first review of a pull
  request, report only Important and new Pre-existing findings.
- **Pre-existing.** Pre-existing is a scope, not a severity: the finding keeps its severity, Important or Nit. A
  finding is **Pre-existing** when no line of the pull request's diff (`git diff origin/main...HEAD`) touches the code
  it is about, and no changed line, removed guard, new or changed caller, or changed input, default or config alters
  what that code does or how it is reached. Moved or renamed lines count as changed. A confirming round judges against
  the whole pull request's diff, not only the commits it reads. The reviewer gives the label, with the unchanged
  code's `file:line` and why nothing in the diff reaches it; the author does not turn a finding into a Pre-existing
  one, and when two reviewers give it different severities the more severe stands. A Pre-existing finding asks for no
  fix in this pull request and starts no new round. It does not count against the nit cap, or in the review line's N
  or «left». The pull request body lists each one: `Pre-existing: <file:line> – <bead id, proposal, or none: why>`. A
  Pre-existing Important finding always gets a follow-up bead, or a bead proposal to the owner where the owner files
  beads (the owner decides on filing).
- **No repeats.** A later round of the pre-PR review is given the earlier rounds' findings, the ones rejected as wrong
  included, and does not report them again. It names an earlier finding only when its fix did not work, and says so.
- **Summary first.** The report starts with a count: "3 findings: 1 Important, 2 Nits", "3 findings: 1 Important,
  2 Nits (+1 Pre-existing Important)", or "No findings".
- **Risk tier.** A `risk:high` pull request (DevInfra `docs/review-risk-tiers.md`) gets every Important item above
  that touches its paths checked explicitly, ✓ or ✗ with `file:line`.

## Do not report

- What CI already checks: build and tests, format, the changelog fragment and assembly checks, the sample
  stylesheets in step, class names listed and noted for hosts, the comment budget, the portability guard, a host
  needing no glue, and the axe scan with the 320px reflow check. The geometry, hostile-host and Tab-walk scans run
  nightly, not on a pull request, so a layout or keyboard change still needs a reviewer's eye.
- Generated files: lockfiles, packed output.
