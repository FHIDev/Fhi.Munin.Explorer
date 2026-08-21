category: Added

- The whole-variable view now shows the variable's version history: one row per version with its
  name, status and validity period, each expanding to that version's description and dates. It is
  built from the detail payload the host already has, so it costs no extra request and needs no
  host wiring — mount `VariableView` as before and the section appears when the variable has
  versions.
