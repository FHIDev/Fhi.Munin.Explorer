category: Added

- **The kilde table can open a row on its data collections.** A leading control column carries a
  toggle on every kilde that has any; pressing it expands the row in place and lists that kilde's
  datasamlinger, grouped as they are in the catalogue — the kilde's own first, then one group per
  delkilde. Several rows can be open at once, each is fetched once and cached, and an open row
  survives filtering: it is held by the kilde's id, not by its position in the list.
