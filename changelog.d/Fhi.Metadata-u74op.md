category: Fixed
- A kilde whose payload carries an explicit `"additionalProperties": null` no longer takes the
  detail view down. `KildeView`, `VariableView` and the variable panel's property rows all read a
  bag declared non-nullable with an initialiser that `System.Text.Json` overwrites with null, and
  the resulting `NullReferenceException` was thrown while rendering — past the point where a host
  could report it as a failed load. Null is now read as "no curated properties", the same answer
  the kilde list already gave.
