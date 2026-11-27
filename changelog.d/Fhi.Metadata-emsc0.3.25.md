category: Changed
- **Signing in saves the browser list's rows Munin accepts, and keeps only the refused ones.** When Munin refuses
  some rows of the browser list «Lagre i Mine variabellister» saves (a row whose datasamling is no longer its
  variable's own), and names the refused (variable, datasamling) pairs, as Munin does since Fhi.Metadata-emsc0.3.14,
  the other rows are added to the new list in one more request. The saved rows leave the browser, the refused ones
  stay, and the alert names the list and how many rows of how many variables could not be saved, beside «Fjern
  disse radene». Before, one refused row meant nothing was saved. A later press makes a new list and never adds to
  the part-filled one. A Munin that names only variables, or a refusal of every row, saves nothing, as before.
  Behind `MuninExplorer:Features:CodePicker`, as the browser list is. (Fhi.Metadata-emsc0.3.25)
