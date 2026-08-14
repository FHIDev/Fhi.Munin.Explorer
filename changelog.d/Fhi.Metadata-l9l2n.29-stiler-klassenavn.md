category: Changed
- `Variabelutforsker` now emits `Fhi.Helsedata.Stiler`'s own class names instead of invented
  `variabelutforsker-*` ones, and lists results as `datasourcecard`s rather than in a table —
  the shape helsedata's datakildeutforsker already uses. On helsedata.no the component is
  styled by the site it is embedded in; nothing has to be added to Stiler for it. Hosts outside
  that estate must provide `form-element__label`, `searchbox__freetext*`, `hd-button-square` /
  `button-square--primary`, `headline`, `caption`, `infobox` and `datasourcecard*`; the two
  sample hosts show a working approximation.
