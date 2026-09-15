category: Notes for hosts
- **A host with a strict Content-Security-Policy has one new path to allow.**
  `_content/Fhi.Munin.Explorer/explorer-interop.js` is fetched as an ES module after the first
  render. A host already calling `UseStaticFiles()` or `MapStaticAssets()` serves it with no
  change; a host that blocks it loses nothing, because the explorer is fully usable without it
  and the refusal is caught rather than left to break the circuit. (Fhi.Metadata-35w0p.14)
