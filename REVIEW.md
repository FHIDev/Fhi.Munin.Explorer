# Review guidance: Fhi.Munin.Explorer

Read by the pre-PR reviewer before it reviews a diff here, and by Copilot through its instructions. The same
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

Everything else (naming, wording, structure, a smaller way to write it) is a **Nit**.

## How to report

- **Evidence.** Every finding names `file:line` and the input, width or state that makes it go wrong. No finding
  without a place; "consider" with no concrete failure is not a finding.
- **Nit cap.** At most 5 Nits per review; more collapse into "(plus N similar)". After the first review of a pull
  request, report only Important findings.
- **Summary first.** The report starts with a count: "3 findings: 1 Important, 2 Nits", or "No findings".
- **Risk tier.** A `risk:high` pull request (DevInfra `docs/review-risk-tiers.md`) gets every Important item above
  that touches its paths checked explicitly, ✓ or ✗ with `file:line`.

## Do not report

- What CI already checks: build and tests, format, the changelog fragment and assembly checks, the sample
  stylesheets in step, class names listed and noted for hosts, the comment budget, the portability guard, a host
  needing no glue, and the axe scan with the 320px reflow check. The geometry, hostile-host and Tab-walk scans run
  nightly, not on a pull request, so a layout or keyboard change still needs a reviewer's eye.
- Generated files: lockfiles, packed output.
