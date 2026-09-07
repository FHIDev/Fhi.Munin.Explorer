category: Fixed

- **A catalogue entry with no name no longer leaves the control that carries it unnamed.** A kilde,
  delkilde, datasamling or variable whose name is empty — reachable two ways: the catalogue can hold
  an empty `preferredTerm`, and since `Fhi.Metadata-o355u` an explicit `navn: null` from the API
  reads as the empty string rather than taking the host's circuit down — drew a row button with no
  text, a checkbox announcing "Velg " with nothing after it, and a heading with nothing in it. Every
  such control and heading now falls back to the entry's **code**, the identifier already drawn
  beside it and the one the API itself falls back to. Where the code stands in for the name, the
  line that usually repeats it underneath is not drawn, and the `lang="no"` marker comes off: a code
  is not Norwegian prose for a screen reader to sound out. (Fhi.Metadata-w13lk)
- **The variable row's disclosure button says which variable it opens.** It answered "Vis hele
  variabelen" for every unnamed variable — a name that satisfies a checker and leaves a reader no
  way to tell one row from the next. It now answers with the variable's code. (Fhi.Metadata-w13lk)
