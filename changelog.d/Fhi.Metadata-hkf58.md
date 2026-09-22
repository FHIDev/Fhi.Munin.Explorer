category: Added
- **A variable names the instrument it was collected with, and the instrument gets a page of its
  own.** `VariableDetail.Instruments` carries the questionnaires and scales the catalogue links a
  variable to, and both the whole-variable view and the result row's panel list them, each name in
  the reader's language and each linking to `?instrumentId=<id>`. That address opens `InstrumentView`
  in place of the result list — code, name, description, validity and the catalogue's own properties
  — with a link on to the variables collected with the instrument. `IMuninExplorerClient` gains
  `GetInstrumentAsync`, which has a default body answering null, so a host implementing the
  interface itself keeps compiling. The instrument page needs Munin's own
  `GET api/explorer/instrument/{id}`, which ships in the Munin release that adds the instrument to a
  variable's detail; against an API older than that the page says the instrument was not found, the
  list on a variable is empty, and nothing else changes.
  (Fhi.Metadata-hkf58)
