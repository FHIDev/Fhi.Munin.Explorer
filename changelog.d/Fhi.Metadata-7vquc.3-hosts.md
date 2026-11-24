category: Notes for hosts
- **Turn on `MuninExplorer:Features:CatalogueFormatting` only with Stiler 0.1.218 or later.** The flag's lists wear
  the class `munin-explorer-text__list`, and 0.1.218 is the first Stiler that styles it. An earlier Stiler draws an
  `ol` inside a row panel's `dd` (`.munin-explorer-detail dd ol`) as a kilde trail, so without the rule a numbered
  list there reads as a chevron trail (Fhi.Metadata-7vquc.4).
  Where a description holding a list was a `p`, or a value a `span`, it becomes a `div` with the same class and
  `lang`, since neither may hold a list.
