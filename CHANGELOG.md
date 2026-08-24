# Changelog

Notable changes to the published package. This file is for **consumers** — what changed in
`Fhi.Munin.Explorer`, which carries the component, the client that feeds it and the types they
share, and what a host has to do about it. (`.Blazor`, `.Client` and `.Contracts` are namespaces
inside it, not separate packages.) Internal repository housekeeping belongs in commit messages,
not here.

Versions follow [semver](https://semver.org/). While on `0.x` the API surface may still move;
we stay below `1.0.0` until a consuming host is live and the surface has settled. Once at
`1.0.0`, a breaking change means a new major with a deprecation window — a package a partner
service embeds cannot move under them without warning.

**Unreleased changes are not in this file.** Each one lands on its branch as its own file in
[`changelog.d/`](changelog.d/README.md), and `scripts/assemble-changelog.ps1` folds them in under
a version heading at release time. One file per change means two PRs in flight never conflict
over this one. To see what is queued for the next release, read `changelog.d/`.

The package is published to `Fhi.Helsedata.no`, helsedata's internal Azure Artifacts feed, and
not to nuget.org — restore it from there. Nothing has been published yet, so there are no released
versions below.

<!-- assemble-changelog: new version sections are inserted directly below this line, newest first. -->
