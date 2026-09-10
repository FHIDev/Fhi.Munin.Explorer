category: Changed
- **A kildetype facet's `DisplayName` is resolved prose in the request's language, not the raw enum
  name.** `GET /api/explorer/filters` used to answer `SentraltHelseregister` there, and
  `KildetypeFacet.DisplayName` was documented as such — so a host was told to supply prose of its
  own. It no longer has to: the API resolves the label and follows `Accept-Language`, giving
  `Sentralt helseregister` under `nb` and `Central health registry` under `en`. Two consequences for
  a host that was reading it. Key off `Value`, which is unchanged and language-independent, wherever
  identity matters — `DisplayName` now differs between languages. And the facet list is ordered by
  that resolved label rather than by the value, so `kildeTyper` arrives in a different order in each
  language; a host mirroring the API's order elsewhere on the page inherits that. This package is
  one such host. The words it puts on a kildetype are still its own, looked up by `Value` — those
  did not change — but it draws the kildetype facet, and the kilde headings grouped under it, in
  the order the API sent. Under `nb` that is the order it always was; an English mount now sorts
  them by English prose, so `Central health registry` heads a list that `Other data source` used
  to. (Fhi.Metadata-iv9xp)
