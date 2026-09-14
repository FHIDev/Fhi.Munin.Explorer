category: Added
- **`DetailPage` takes any other attribute you write on it and puts it on the root element.**
  A new `AdditionalAttributes` parameter, captured the ordinary Blazor way. It exists so a view
  mounted on its own can mark its root — `data-munin-explorer-version` on the saved-list view is the whole of its use here — and
  a `class` written through it would win over `ViewRoot`, which is the parameter for a view's own
  root name. (Fhi.Metadata-35w0p.13)
