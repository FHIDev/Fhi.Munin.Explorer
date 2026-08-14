# Changelog

Notable changes to the published packages. This file is for **consumers** — what changed in
`Fhi.Munin.Explorer.Blazor`, `.Client` and `.Contracts`, and what a host has to do about it.
Internal repository housekeeping belongs in commit messages, not here.

Versions follow [semver](https://semver.org/). While on `0.x` the API surface may still move;
we stay below `1.0.0` until a consuming host is live and the surface has settled. Once at
`1.0.0`, a breaking change means a new major with a deprecation window — a package a partner
service embeds cannot move under them without warning.

## Unreleased

### Added
- First component: `Variabelutforsker` — search and browse published variables from the Munin
  Explorer API. Takes `Sok`, `SokChanged`, `SideStorrelse` and `Sprak` (`"no"` / `"en"`).
- `AddMuninExplorer(...)` registers the data client; the host supplies `ApiBaseUrl`, or sets
  `MuninExplorer:ApiBaseUrl` in configuration.

### Notes for hosts
- The component ships **no CSS**. Styling comes from the host — on helsedata.no that is
  `Fhi.Helsedata.Stiler`. Class names are prefixed `variabelutforsker-`.
- The component sets no render mode. The host decides at the mount site — `render-mode="Server"`
  on the `<component>` tag helper in a legacy Blazor Server host, or `@rendermode` in a modern
  Blazor Web App.
- A host mounting it must contain at least one `.razor` file (an `_Imports.razor` is enough), or
  the Blazor framework script is not served to the project and the circuit never starts.
- The search field talks to the server only when the search is submitted — Enter or the Søk
  button — not on every keystroke. `SokChanged` fires once per search, not once per character.
