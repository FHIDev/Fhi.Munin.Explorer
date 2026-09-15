category: Added
- **The package now ships one JavaScript module as a static web asset.**
  `_content/Fhi.Munin.Explorer/explorer-interop.js`, loaded by dynamic import after the first
  render and by nothing else — the same shape `Fhi.Helsedata.Soknader` already ships on this host.
  Nothing rendered depends on it: a host that does not serve it draws exactly the page it drew
  before. It ships no CSS, and that has not changed. (Fhi.Metadata-35w0p.14)
