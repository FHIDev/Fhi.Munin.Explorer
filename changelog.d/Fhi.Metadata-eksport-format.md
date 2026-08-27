category: Fixed

- **Nedlasting av variabelliste virket ikke i det hele tatt** - `ExportListAsync` sendte
  `format` som `"Csv"`/`"Xlsx"`, men API-et staver dem `[JsonStringEnumMemberName("csv"/"xlsx")]`
  og svarer 400 på PascalCase. Hver eneste nedlasting endte i "Kunne ikke laste ned nå". Sender nå
  navnet API-et faktisk tar imot. (Fhi.Metadata-7mx2s)
