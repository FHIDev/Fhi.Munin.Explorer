category: Changed

- **BREAKING for hosts that read Munin's own timestamps: they are `DateTimeOffset?` now.**
  `opprettet` and `sistOppdatert` on `KildeDetail`, `DatasamlingDetail` and `KildeSummary`, and
  `createdAt`, `updatedAt` and `addedAt` on the variable-list types. The wire format is unchanged —
  every `[JsonPropertyName]` is untouched — and so is what the component renders. A host that does
  not recompile at all gets a `MissingMethodException` on the getter, since the return type is part
  of the signature.
- **The half that does not compile is the easy half.** A host reading one into a non-nullable local
  is told by the compiler. Two things compile unchanged and behave differently. `k.LastUpdated <
  cutoff` — a "not touched since" report — was **true** for a kilde whose payload omitted the field,
  because the property held `0001-01-01`, and is **false** now; those rows leave such a report
  silently. `$"{kilde.LastUpdated}"` renders an empty string where it rendered a date. And
  `Min()` over a set that includes one now answers the earliest real date rather than `0001-01-01`.
  `OrderBy` is *not* affected — `null` sorts exactly where `MinValue` did. Nor is `== default`, which
  stays true for the absent case; it is `== DateTimeOffset.MinValue` that stops matching.
  (Fhi.Metadata-se0by)
