category: Fixed

- **The statistics table survives a statistic whose properties arrive as null** - a variable whose
  payload carries an explicit `"additionalProperties": null` on one of its statistics took the view
  down while rendering: the table read that bag straight off the contract, where the non-nullable
  declaration and its initialiser promise something `System.Text.Json` does not keep for an explicit
  null. It reads it as the empty bag it means now, so the row draws the same dash a statistic with
  no numbers already drew. This is the one read the guard added for the kilde detail view did not
  cover, because the statistics table does not go through the shared property rows.
  (Fhi.Metadata-hox1c)
