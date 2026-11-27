category: Fixed
- **A code link says it is loading while the code is looked up.** `VariableExplorer` drew nothing while it asked
  Munin for the variable a `?variabel=` link names. It now shows «Henter variabler …» («Loading variables …» in
  English) in a `role="status"` paragraph marked with the reader's language, until the variable, or the search for
  its code, opens.
- **One variable whose code finds nothing no longer turns code links off for the others.** A code that Munin
  answers with nothing (a one-letter fold naming two variables, or a hidden variable) now keeps only that
  variable's links by id. A Munin without the code route is asked once for each variable a reader opens.
- **A variable's code is asked at most twice while Munin keeps failing.** Before, every render asked again and
  logged another error.
- **Leaving a code link's view, or closing the explorer, cancels the pending code lookup.** Leaving cancels the
  lookup of the link's code, and closing cancels every lookup still pending. Before, a lookup the reader had left
  ran on to the end and its answer was thrown away. A cancelled lookup is not logged.
  (Fhi.Metadata-l9l2n.188)
