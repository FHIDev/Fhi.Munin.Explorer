category: Notes for hosts
- **Turn on `MuninExplorer:Features:CatalogueFormatting` only with a Stiler that styles `munin-explorer-text__list`.**
  The flag's lists wear that class. Stiler today draws an `ol` inside a row panel's `dd` (`.munin-explorer-detail dd
  ol`) as a kilde trail, so without the rule a numbered list there reads as a chevron trail (Fhi.Metadata-7vquc.4).
  Where a description holding a list was a `p`, or a value a `span`, it becomes a `div` with the same class and
  `lang`, since neither may hold a list. On an instrument's page that `div` also carries `style="margin-block: 1em"`,
  the browser's default margin for a `p`, because no host rule spaces its bare `ingress` class. A host that sets its
  own `p` margins, or adds a rule for this ingress, must also beat the inline style (with `!important`) to keep the
  two forms spaced alike.
