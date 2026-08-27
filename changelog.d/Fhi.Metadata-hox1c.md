category: Fixed

- **The statistics table survives a statistic whose properties arrive as null** - a variable whose
  payload carries an explicit `"additionalProperties": null` on one of its statistics took the view
  down while rendering: the table read that bag straight off the contract, where the non-nullable
  declaration and its initialiser promise something `System.Text.Json` does not keep for an explicit
  null. It reads it as the empty bag it means now, so the row draws the same dash a statistic with
  no numbers already drew. This is the one read the guard added for the kilde detail view did not
  cover, because the statistics table does not go through the shared property rows.
  (Fhi.Metadata-hox1c)
- **And the client now keeps that promise for every collection on every contract** - the same
  explicit null lands the same way in any of them, and the two fixes so far each closed only the
  read the payload happened to reach. The client's serialiser reads a null where a collection is due
  as the empty collection, so `AdditionalProperties`, `PropertyMetadata`, the translation bags and
  every list beside them are non-null because the deserialiser makes them so rather than because a
  property initialiser was hoped to. A host substituting its own `IMuninExplorerClient` deserialises
  with its own options, so the components still coalesce a null bag where they read one.
  (Fhi.Metadata-hox1c)
