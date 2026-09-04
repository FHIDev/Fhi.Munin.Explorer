category: Changed

- **BREAKING for hosts that read Munin's own timestamps: they are `DateTimeOffset?` now.**
  `opprettet` and `sistOppdatert` on `KildeDetail`, `DatasamlingDetail` and `KildeSummary`, and
  `createdAt`, `updatedAt` and `addedAt` on the variable-list types. The wire format is unchanged —
  every `[JsonPropertyName]` is untouched — and so is what the component renders.
- **The half that does not compile is the easy half.** A host reading one into a non-nullable local
  is told by the compiler. What compiles unchanged and behaves differently is anything using a
  lifted operator: `kilde.LastUpdated > cutoff` is `false` against a null, so rows a host used to
  keep now drop out silently; `OrderByDescending(k => k.LastUpdated)` sorts nulls first, so the
  ends of a list swap; and `$"{kilde.LastUpdated}"` renders an empty string where it rendered a
  date. Worth grepping for those three shapes rather than trusting a clean build. A host that does
  not recompile at all gets a `MissingMethodException` on the getter. (Fhi.Metadata-se0by)
