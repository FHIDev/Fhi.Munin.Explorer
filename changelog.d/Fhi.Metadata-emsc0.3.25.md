category: Changed
- **Saving the browser list signed in keeps the rows Munin accepts, and leaves only the refused ones.** When Munin
  refuses some of the rows that «Lagre i Mine variabellister» saves (a row whose datasamling is no longer its
  variable's own) and names the refused (variable, datasamling) pairs, as current Munin does, the other rows are
  added to the new list in one more request. The saved rows leave the browser and the refused ones stay. The alert
  names the list and says how many rows of how many variables could not be saved, beside «Fjern disse radene»
  («Fjern denne raden» for one row); when the saved rows cannot leave the browser, it says instead that the list was
  saved but is still in this browser, and how many rows could not be saved and are only there. Before, one refused row meant nothing was saved. A later press makes a new
  list and never adds to the part-filled one. A Munin that names only variables, or a refusal of every row, saves
  nothing, as before. Behind `MuninExplorer:Features:CodePicker`, as the browser list is. (Fhi.Metadata-emsc0.3.25)
