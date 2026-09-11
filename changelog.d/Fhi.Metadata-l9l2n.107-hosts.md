category: Notes for hosts
- **The kildeutforsker's hierarchy emits one new class name, `munin-explorer-hierarchy__open`.**
  It is the link that opens a datasamling in the tree. A handle rather than a name that carries
  meaning — undefined, it is an ordinary `<a href>` and draws at the browser's own link style, so
  what a rule buys is putting it back on the node's own line, since it is written after the
  node's `<details>` rather than inside the `<summary>`. Both sample stylesheets show that rule.
  No rule for it ships in `Fhi.Helsedata.Stiler` yet. A host on a Blazor `Router` should also know
  that opening a datasamling remounts `KildeSearch`: the press is intercepted, `KildeExplorer`
  reads the new address and rebuilds rather than forcing a page load, so the open kilde is fetched
  again and its hierarchy comes back collapsed. (Fhi.Metadata-l9l2n.107)
