category: Notes for hosts
- **The kildeutforsker's hierarchy emits one new class name, `munin-explorer-hierarchy__open`.**
  It is the link beside a datasamling that opens it. A handle rather than a name that carries
  meaning — undefined, it is an ordinary `<a href>` and draws at the browser's own link style, so
  what a rule buys is the separation from the name in front of it. No rule for it ships in
  `Fhi.Helsedata.Stiler` yet. A host on a Blazor `Router` should also know that opening a
  datasamling forces a full page load: `KildeExplorer` reads its query at initialisation, so an
  intercepted client-side navigation would change the address and nothing else.
  (Fhi.Metadata-l9l2n.107)
