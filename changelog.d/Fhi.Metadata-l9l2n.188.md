category: Fixed
- **A code link says it is loading while the code is looked up.** `VariableExplorer` drew nothing while it asked
  Munin for the variable a `?variabel=` link names. It now shows «Henter variabler …» («Loading variables …» in
  English) in a `role="status"` paragraph marked with the reader's language, until the variable, or the search for
  its code, opens.
- **One variable whose code finds nothing no longer turns code links off for the others.** When Munin answers a
  variable's code with nothing (a one-letter fold naming two variables, or a hidden variable), only that variable
  is linked by id, in the address and in «Del variabel». A Munin without the code route is asked the code once
  for each variable a reader opens, after a request for the variable when the view that opened it does not have
  its code: at most two requests per variable, besides the detail the view itself reads.
- **Learning a variable's code for the address tries at most twice while it keeps failing.** Before, every render
  tried again and logged another error. Each try is the requests above, so a failing Munin gets at most four for
  one variable. The second try comes at the next render that needs the code, and after it the explorer stops trying
  for that variable for as long as it stays open. A code link the reader arrives by is still looked up at each
  arrival.
- **Leaving a code link's view, or closing the explorer, cancels the pending code lookup.** Leaving cancels the
  lookup of the link's code, and closing cancels every lookup still pending. Before, a lookup the reader had left
  ran on to the end and its answer was thrown away. A cancelled lookup is not logged.
  (Fhi.Metadata-l9l2n.188)
