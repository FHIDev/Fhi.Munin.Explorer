category: Notes for hosts
- **A host now answers `_content/Fhi.Munin.Explorer/version.json` with the package version it serves.** The build writes
  `{"package":"Fhi.Munin.Explorer","version":"…"}` from the package's own version, as a second static
  web asset beside `explorer-interop.js`. No host code is needed, and nothing rendered depends on it;
  it exists so the version a site actually runs can be read from outside.
