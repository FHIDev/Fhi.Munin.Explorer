category: Notes for hosts

- **A host stylesheet with a bare element rule un-hides everything this package marks `[hidden]`.**
  The browser's own `[hidden] { display: none }` loses to any author rule of equal specificity, so a
  reset carrying `div { display: block }` leaves the folded filter panel on screen while the toggle
  still says "Vis filtre". Put `[hidden] { display: none }` back for the elements you dress; both
  sample stylesheets now do. (`Fhi.Metadata-fih3y`)
