## What and why

<!-- What changes, and what it is for. Mechanism detail belongs in the commit message. -->

## Bead

<!-- Work is tracked in the Munin beads workspace, not this repo's issues.
     Use the cross-repository form so merging closes the bead:

       Closes FHIDev/Munin#1234

     Use `Refs` instead of `Closes` when this PR only partly satisfies the bead —
     `Closes` closes it on merge whether or not the acceptance criteria are met. -->

Bead: Fhi.Metadata-
Closes FHIDev/Munin#

## Host-compatibility check

The RCL has to render in helsedata's Optimizely CMS (legacy Blazor Server, no router)
*and* in a modern Blazor Web App. Confirm anything that touched the component library:

- [ ] No `@page` — the CMS owns routing; the explorer is one parameterised root component
- [ ] No `@rendermode` in the RCL — the host decides at the mount site
- [ ] No CSS, `wwwroot` or `.razor.css` shipped from the RCL — styling comes from Stiler
- [ ] New `EventCallback` parameters, if any, are documented as requiring a fully interactive mount
- [ ] Verified in `samples/LegacyHost`, not only `samples/ModernHost`

<!-- The banned-API guard catches host-specific *types* at build time. It cannot catch
     the four points above — those are design rules, which is why they are a checklist. -->
