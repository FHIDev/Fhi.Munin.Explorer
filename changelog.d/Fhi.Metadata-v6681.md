category: Added
- **A hierarchy trail over the results** — kilde → delkilde → datasamling → variabelgruppe, drawn
  above the list whenever any of the four is filtered on. It is the only thing on screen that says
  *where* a deep selection has put the reader: the facet panel holds the same choice as pressed
  buttons in collapsed disclosures, so a kilde chosen three levels down is otherwise visible only
  as the result count changing. Each step is a button that clears every level under it, several
  values on one level read as the first name and `(+n)`, and a `×` beside the trail empties the
  whole hierarchy while leaving every other filter — datatype, kodeverk, dates — in force.
  (Fhi.Metadata-v6681)
- Two class names go with it, both a host's to draw: `variable-explorer-breadcrumb` with its
  `__clear` for the trail's own shape, and the existing `variable-explorer-crumb` for the steps,
  which is the same name the variable panel's kilde trail already uses. A host that draws neither
  gets a numbered list of buttons in the right order with the right names, which is the
  information without the shape that says "path".
