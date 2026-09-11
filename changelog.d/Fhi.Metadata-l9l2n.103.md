category: Added
- **The kilder table's scroll box now says how many columns it is holding.** Beside
  `munin-explorer-kilder-scroll` the box wears `munin-explorer-kilder-scroll--cols-N`, where N is
  the number of header cells the table actually rendered rather than the number of choices the
  column picker offers — the picker reaches ten of them and the table draws four or five more that
  it cannot. A stylesheet that has to vary the box by how wide the table is now has something to
  select on, which is a question only the package can answer. (Fhi.Metadata-l9l2n.103)
