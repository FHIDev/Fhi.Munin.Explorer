category: Fixed
- A kildekodeverk the Explorer API resolves no name for is now drawn by its code values
  instead of as "Ukjent navn" above an internal Munin reference and a collapsed "Vis koder"
  button. Up to eight codes appear inline as the link's identity; beyond that a preview and a
  "Vis alle (N)" control open the same full code list the existing toggle opens. Those codes
  are fetched when the panel opens rather than on a press, so hosts see one extra request per
  nameless link per variable opened — named links are still fetched only when asked for. While
  the fetch is out the line says so; if it fails or the API publishes no codes, the reference
  and the control come back, so nothing becomes unidentifiable. Administrativt and helsefaglig
  kodeverk links are unchanged.
