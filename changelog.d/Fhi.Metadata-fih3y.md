category: Notes for hosts

- **The sample stylesheets now re-hide the facet panel below 1024px, as `Fhi.Helsedata.Stiler`
  does.** A host that copied `samples/*/host.css` got a filter panel that stayed open under the
  breakpoint while its own toggle still said "Vis filtre" — the browser's `[hidden]` rule loses to
  any author rule of equal specificity. (`Fhi.Metadata-fih3y`)
