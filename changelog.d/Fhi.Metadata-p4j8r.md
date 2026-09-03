category: Notes for hosts

- **The sample stand-in for `munin-explorer-kilde__delkilde-description` was a pixel larger than
  the rule Stiler ships.** The samples declared 15px where Stiler styles it at `$font-s`, 14px —
  one step below the `headline-xxs` the delkilde name above it wears. The stand-in now carries
  Stiler's own value, so a host reading the samples for this class takes the number the real
  stylesheet uses rather than a re-derived one.
