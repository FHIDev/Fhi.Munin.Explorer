category: Fixed
- **`VariableExplorerWithUrlState` passes the explorer's own parameters through.** It forwarded only
  the state it mirrors, so `IsAuthenticated` and `HeadingLevel` never reached the explorer: a host
  mounting it lost the save button for signed-in readers, and its heading level, with no way to set
  either. (Fhi.Metadata-l1f2s)
