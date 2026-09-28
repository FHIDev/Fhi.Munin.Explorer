category: Notes for hosts
- **1.0 is a stability promise: from here on, anything a host has to act on is a new major version.**
  The contract covers three things:
  - the components and parameters "What a host mounts" in the README documents;
  - the class names the README lists under the `munin-explorer-` prefix;
  - the detail pages' section ids, which 0.1.0-alpha.15 moved under the same prefix.

  `1.0.x` releases are fixes only. Adding a parameter or a name is `1.x.0`. Removing, renaming or
  retyping one is `2.0.0`, and so is raising the Stiler floor. What the component does is described
  release by release in the `0.1.0-alpha` sections of `CHANGELOG.md`.
- **1.0 is styled for `Fhi.Helsedata.Stiler` 0.1.134 or later.** 0.1.134 gives the detail pages
  one column with a 47em reading measure, and widens Runa's search list to 1760px for every
  reader, signed in or not. Stiler 0.1.114 to 0.1.133 draws everything with the older layouts.
  Below 0.1.114, a variable row has no visible focus ring and its chevron shows no picture.
