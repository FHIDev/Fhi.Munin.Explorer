category: Changed

- **A kilde or datasamling opens as its own view, not a panel inside a panel inside a row** - it was
  three levels deep and cramped; it now takes over the component's area and offers a way back, which
  is as close to Runa's dedicated page as a component with no router gets. The search, filters, page
  and open row are all still there on return, because none of it is torn down — only hidden. It
  stays a named region so a screen reader moving by landmark still finds it.
- **The datatype column shows a name instead of a code** - "Integer" rather than "2". Resolved from
  the facets the filter panel already loads, so it costs no extra request and no lookup table lives
  in the package. `Accept-Language` now carries the component's own language, since the API resolves
  these names per request culture — without it a component rendering in English would have been
  served Norwegian labels, or the other language's cached body. (Fhi.Metadata-7mqzs)
