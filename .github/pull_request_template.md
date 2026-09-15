## What and why

<!-- What changes, and what it is for. Mechanism detail belongs in the commit message. -->

## Bead

<!-- Work is tracked in the Munin beads workspace, not this repo's issues.
     Uncomment the line below and put the real bead id in it: the closer only reads
     "Closes-Bead: <id>" as a plain line of its own, never inside a comment.
     It closes the bead a few minutes after this PR merges. Only partly done?
     Write "Bead: <id>" instead — "Closes-Bead" closes it whether or not the
     acceptance criteria are met. -->

<!-- Closes-Bead: Fhi.Metadata-xxxxx -->

## Changelog

<!-- Anything that touches src/ needs a fragment: changelog.d/<slug>.md, one line saying what
     changed for someone embedding the package and what they have to do about it. CI checks it.
     Format and examples: changelog.d/README.md. Docs, samples, tests and CI need none. -->

- [ ] Added `changelog.d/<slug>.md`, or this PR does not change `src/`

## Host-compatibility check

The RCL has to render in helsedata's Optimizely CMS (legacy Blazor Server, no router)
*and* in a modern Blazor Web App. Confirm anything that touched the component library:

- [ ] No `@page` — the CMS owns routing; the explorer is one parameterised root component
- [ ] No `@rendermode` in the RCL — the host decides at the mount site
- [ ] No CSS and no `.razor.css` shipped from the RCL — styling comes from Stiler. A new file
      under the RCL's `wwwroot` carries its own entry in `scripts/assert-package-contents.sh`,
      in the same commit
- [ ] New `EventCallback` parameters, if any, are documented as requiring a fully interactive mount
- [ ] Verified in `samples/LegacyHost`, not only `samples/ModernHost`

<!-- The banned-API guard catches host-specific *types* at build time. It cannot catch
     the four points above — those are design rules, which is why they are a checklist. -->
