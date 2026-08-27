category: Fixed

- **Downloading a variable list never worked** - `ExportListAsync` sent `format` as `"Csv"`/`"Xlsx"`,
  but the API spells those members `[JsonStringEnumMemberName("csv"/"xlsx")]` and answers PascalCase
  with a 400, so every download ended in the failure message. Now sends the name the API accepts.
  (Fhi.Metadata-7mx2s)
