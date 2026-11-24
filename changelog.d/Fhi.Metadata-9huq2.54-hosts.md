category: Notes for hosts
- **`OpenDataWithoutPage` lets a host word the open-data sentence for a kilde with no open-data address.** A string
  parameter on `KildeExplorer` and `VariableExplorer`, passed to every kilde page they open, and on `KildeView`; `{0}` is
  the kilde's name, and no other hole is read. It replaces the sentence up to where «, men hjemmesiden er …» / «, but the
  website is …» follows, so write it to read on into that; a closing full stop or exclamation mark is dropped first.
  Empty or absent gives the package's text, and so does a `KildeSearch` or `VariableSearch` mounted alone. A CMS host
  fills it from a page field, the way it fills `Lede`.
- **BREAKING for hosts that relied on it: `KildeAccessLinks.Helsedata` no longer sets `OpenDataHref`.** The default
  pointed at `/{language}/apne-data/`, which shows no kilde. A host with an open-data page that does list kilder sets
  `MuninExplorer:AccessLinks:OpenDataHref` (or `KildeAccessLinks.OpenDataHref`), and its kilder keep the «Se åpne data»
  button to it. A host with no access links at all (`AccessLinks = null`, or every key empty) now shows the open-data
  sentence, in a «Tilgang og ansvar» block, on a kilde with open data and no address, where it showed nothing before.
