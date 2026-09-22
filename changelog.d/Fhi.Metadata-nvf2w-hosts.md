category: Notes for hosts
- **`KildeExplorer` now owns `?search=`, `?kildetype=`, `?kategori=`, `?tilgangsniva=`,
  `?databehandler=`, `?columns=` and `?selected=` on the page it is mounted on.** Until this
  version it carried `?search=` through untouched. A host that means something else by one of
  these keys on that page mounts `KildeSearch` and owns the query string itself.
  (Fhi.Metadata-nvf2w)
