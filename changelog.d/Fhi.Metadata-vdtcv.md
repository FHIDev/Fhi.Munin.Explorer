category: Added

- **`VariableListItem` carries the display fields the API resolves for it** - code, name, kilde and
  its short name, datasamling, variabelgruppe, datatype, data period and version status, alongside
  the id and the time it was added. Munin began sending these with `Fhi.Metadata-kejyv`; the
  contract here was written before that and read none of them, so a saved list could be drawn with
  an id and a date. (Fhi.Metadata-vdtcv)
- **They are all optional and may be null together**, which means that id has no row in the read
  model — retracted, unpublished, or not yet projected. Such an entry is still returned rather than
  dropped, so a list of 247 does not answer with fewer than it counted, and the caller decides what
  to draw for it.
- **The wire keeps the Norwegian stem** — `variabelCode`, `variabelName`, beside the `variabelId`
  this contract already spelled out. Every field carries an explicit `[JsonPropertyName]`: the
  package deserialises with `JsonSerializerDefaults.Web`, whose camelCase mapping would look for
  `variableName` and quietly find nothing, which reads on screen as an empty list rather than as
  names that did not arrive.
- **Version status is a string, not an enum**, the same way `VariableSummary.VersionStatus` is —
  `JsonSerializerDefaults.Web` carries no string-enum converter, so an enum would need one
  registered by every host.
