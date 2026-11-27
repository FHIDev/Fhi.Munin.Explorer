category: Fixed
- **A code link says it is loading while the code is looked up.** `VariableExplorer` drew nothing while it asked
  Munin for the variable a `?variabel=` link names. It now shows «Henter variabler …» in a `role="status"`
  paragraph until the variable opens.
- **One variable whose code finds nothing no longer turns code links off for the others.** A code that Munin
  answers with nothing (a one-letter fold naming two variables, or a hidden variable) now keeps only that
  variable's links by id.
- **A variable's code is asked at most twice while Munin keeps failing.** Before, every render asked again and
  logged another error.
- **Leaving or closing the explorer cancels its pending code lookups.** Before, a lookup the reader had left ran
  on to the end and its answer was thrown away.
  (Fhi.Metadata-l9l2n.188)
