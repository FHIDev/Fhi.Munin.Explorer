category: Added

- **The Data tab groups the kodeverk by kind and can show their codes** - Runa's arrangement: a
  heading per Kildekodeverk / Administrativt kodeverk / Helsefaglig kodeverk, one line per link
  under it, and a "Vis koder" control on every link the API serves codes for. Pressing it fetches
  the code list and draws Verdi, Navn, Gyldig fra and Gyldig til. Codes are asked for only when a
  reader presses, and kept once fetched, so collapsing and re-opening a list costs no second
  request — Kommunenummer alone is 885 codes and most readers open none of them.
- **A kodeverk the API resolved no name for says so, instead of showing its reference as its name**
  - the panel used to fall back to the reference, so a variable whose only link had no resolved name
  read "Kildekodeverk: 2336". It now reads "Ukjent navn" with "Referanse: 2336" underneath, and the
  reference is on every line, named or not, because it is what a reader can look the kodeverk up by.
- **`IMuninExplorerClient.GetKodeverkCodesAsync(variableId, kodeverkType, kodeverkReference)`** -
  new, with `KodeverkCodes` and `KodeverkCode` in `Fhi.Munin.Explorer.Contracts`. A host that
  implements the interface itself has one more member to supply. It answers null where the
  catalogue publishes no codes — every `HelsefagligKodeverk` link, and any reference the upstream
  register does not know — and throws on a fault, the same split the rest of the interface follows.
  A type or reference carrying a dot segment is refused with an `ArgumentException` instead of being
  sent: no escaping survives one, because `Uri` unescapes `%2E` before it removes dot segments, so
  the value would resolve against the base address as a different endpoint entirely.
- **Two more DOM handles, and the package's first `<table>`** - `variable-explorer-kodeverk` (with
  `__item`, `__name`, `__reference`) and `variable-explorer-codes` (with `__table`). Neither Stiler
  nor helsedata's variable page has a kodeverk section to borrow names from, so a host mounting the
  component supplies the arrangement itself; `samples/LegacyHost` has a worked stand-in. The table
  is a real `<table>` because four columns of code values have no honest alternative shape — an
  unstyled table still aligns its columns, which is what makes an element safe where an invented
  class name is not. (Fhi.Metadata-jtjfm)
