category: Added
- `VariableExplorer` gained a `HeadingLevel` parameter (1–6, default `2`) that sets the
  level of its own title. Pass the level that follows on from the heading above the mount point:
  a component that emits an `h2` on a page whose last heading was an `h4` breaks the outline
  screen-reader users navigate by. Values outside 1–6 are clamped.
