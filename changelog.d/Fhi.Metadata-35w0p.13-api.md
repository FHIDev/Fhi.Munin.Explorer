category: Added
- **`DetailPage` takes any other attribute you write on it and puts it on the root element.**
  A new `AdditionalAttributes` parameter, captured the ordinary Blazor way. It exists so a view
  mounted on its own can mark its root — `data-munin-explorer-version` on the saved-list view is the whole of its use here.
  A `class` written through it is dropped rather than honoured: the splat is written before the
  element's own `class`, so `munin-explorer-page` cannot be taken off the root the layout rules key
  on. Pass a view's own root name through `ViewRoot`. (Fhi.Metadata-35w0p.13)
