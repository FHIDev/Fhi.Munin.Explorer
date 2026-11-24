category: Notes for hosts
- **`OpenDataWithoutPage` lets a host word the open-data sentence for a kilde with no open-data address.** A string
  parameter on `KildeExplorer` and `VariableExplorer`, passed to every kilde page they open, and on `KildeView`; `{0}` is
  the kilde's name. It replaces the sentence up to where «, men hjemmesiden er …» / «, but the website is …» follows, so
  write it to read on into that; a closing full stop or exclamation mark is dropped first. Empty or absent gives the
  package's text. A CMS host fills it from a page field, the way it fills `Lede`; one field on the page type serves both
  explorers, each page with its own value per language. A shared value on one settings page would keep the two
  explorers' sentences from drifting apart, at the cost of a lookup outside the page; the per-page field is the
  smaller change.
- **BREAKING for hosts that relied on it: `KildeAccessLinks.Helsedata` no longer sets `OpenDataHref`.** The default
  pointed at `/{language}/apne-data/`, which shows no kilde. A host with an open-data page that does list kilder sets
  `MuninExplorer:AccessLinks:OpenDataHref` (or `KildeAccessLinks.OpenDataHref`), and its kilder keep the «Se åpne data»
  button to it.
