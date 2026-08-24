# Contributions

`Fhi.Munin.Explorer` is built and maintained by Folkehelseinstituttet (FHI).

FHI's package guidelines ask every package to name who made it, so that a consumer looking at it
on the feed can tell who stands behind it.

## Maintainers

- **Robin Smith** — <robin.edvard.smith@fhi.no>

## Contributors

Everyone who has authored a commit in this repository, by commit count:

- Robin Smith
- dependabot[bot] — automated dependency updates

Regenerate this list with `git shortlog -sn --no-merges`.

## How the work is organised

Issues live in [beads](https://github.com/FHIDev/Munin/issues) rather than only in this repository,
because the explorer is one part of Munin and its work is planned alongside the rest of it. An issue
here is usually a copy of one there.

Part of the implementation is written by an AI agent under review; commits it authored carry a
`Co-Authored-By` trailer naming the model. Every one of them went through the same pull request,
review and CI as any other change — the trailer records how a change was written, not a different
standard for accepting it.

## Contributing

This package is developed for a specific consumer — helsedata.no — on a fixed timeline, so the
API surface moves with that work and is deliberately below `1.0.0` until it settles.

If you are using this package and something is wrong or missing, open an issue on
[the repository](https://github.com/FHIDev/Fhi.Munin.Explorer/issues). Please say which version you
are on and what you expected, and include the payload if the problem is with what the component drew
— most of what this package renders is decided by the catalogue rather than by the code, so the data
is usually the fastest way to see what happened.
