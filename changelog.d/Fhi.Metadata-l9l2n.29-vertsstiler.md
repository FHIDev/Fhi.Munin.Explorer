category: Notes for hosts
- The host no longer needs a visually-hidden rule. Stiler has no global screen-reader-only
  helper, so nothing in the markup depends on one: the results list is named with `aria-label`
  rather than a clipped `<caption>`, and a missing value is written out as "Ikke oppgitt" for
  everyone rather than shown as an em dash and whispered to assistive technology. What is still
  the host's to get right is a visible focus indicator on the search field and the Søk button
  (WCAG 2.4.7) and text and non-text contrast (WCAG 1.4.3, 1.4.11) — the package ships no CSS,
  so it cannot supply either. Both are listed on the doc comment on `Variabelutforsker`.
