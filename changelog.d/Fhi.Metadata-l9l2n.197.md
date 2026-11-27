category: Changed
- **A kilde's website comes from `landingPage` before `Hjemmeside`.** Where «Søk om tilgang» and the open-data
  sentence fall back to the kilde's own site, and both fields are set (in practice a research project, the kildetype
  `landingPage` applies to), the explorer now takes `landingPage`, the researcher's own field, as Munin's HealthDCAT
  export does. Like the export, it passes over a value that is not a web page, so «Søk om tilgang» no longer opens an
  e-mail address stored as the website. A register, which has no `landingPage`, is unchanged.
