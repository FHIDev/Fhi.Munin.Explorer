category: Notes for hosts

- **Every root element now carries the package version in `data-munin-explorer-version`, so you can
  tell which version is serving a page from the browser alone.** Read it with
  `document.querySelector("[data-munin-explorer-version]").dataset.muninExplorerVersion` — signed in
  or not, on any page that mounts one. The value is the assembly's `AssemblyInformationalVersion`,
  so it carries the prerelease suffix and the commit behind the `+`. Read it from the rendered DOM
  rather than from `curl`: an interactive mount is not prerendered.
- **`munin-explorer-version` is part of an attribute name and not a class**, so no stylesheet rule
  is possible for it and none is wanted. (Fhi.Metadata-sqbei)
