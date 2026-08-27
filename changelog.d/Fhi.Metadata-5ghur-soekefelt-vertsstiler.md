category: Notes for hosts

- **Check one selector in Stiler if your search field looks wrong after upgrading.** Both
  explorers' search inputs changed from `type="search"` to `type="text"` (see Fixed). The class
  name is unchanged — it is still `searchbox__freetext`, still Stiler's own — so a rule written as
  `.searchbox__freetext { … }` is unaffected, which is how both sample hosts write it. A rule
  written as `input[type="search"].searchbox__freetext { … }` would stop matching. Nothing in this
  repository can see Stiler's compiled `main.css`, so this one is stated rather than verified:
  if the field renders at browser defaults after upgrading, that selector is why, and the fix is a
  one-word change in Stiler rather than anything here. (Fhi.Metadata-5ghur)
