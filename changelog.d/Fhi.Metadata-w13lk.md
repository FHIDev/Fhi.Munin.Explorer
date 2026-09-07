category: Fixed

- **A catalogue entry with no name no longer leaves the control that carries it unnamed.** A kilde,
  delkilde, datasamling or variable whose name is empty — reachable two ways: the catalogue can hold
  an empty `preferredTerm`, and since `Fhi.Metadata-o355u` an explicit `navn: null` from the API
  reads as the empty string rather than taking the host's circuit down — drew a row button with no
  text, a checkbox announcing "Velg " with nothing after it, a facet whose checkbox announced only
  its count, a datasamling row whose header cell was empty, and headings with nothing in them. Every
  such control and heading now falls back to the entry's **code** — or its short name, where the
  contract carries no code — which is the identifier already drawn beside it and the one the API
  itself falls back to. Where that stands in for the name, the line that usually repeats it
  underneath is not drawn, and the `lang="no"` marker comes off: a code is not Norwegian prose for a
  screen reader to sound out. (Fhi.Metadata-w13lk)
- **The variable row's disclosure button says which variable it opens.** It answered "Vis hele
  variabelen" for every unnamed variable — a name that satisfies a checker and leaves a reader no
  way to tell one row from the next. It now answers with the variable's code. (Fhi.Metadata-w13lk)
- **An entry with neither a name nor a code reads "Ikke oppgitt".** That is the last arm and it is
  reachable, since a code can be empty exactly as a name can. Two such entries still announce
  identically — a fallback cannot invent an identifier the catalogue does not hold — so the data
  problem underneath is filed separately as `Fhi.Metadata-xku9b`. (Fhi.Metadata-w13lk)
