category: Fixed

- **The new-list name field now has an accessible name** - it carried only a placeholder, so a
  screen reader announced an unnamed edit field and the hint vanished the moment the reader started
  typing. It has a visible `<label>` tied to it with `for`/`id` instead. WCAG 2.1 AA, 4.1.2 and
  3.3.2. (Fhi.Metadata-6vbwa)
- **The save and remove buttons say which variable they act on** - a page of results was 25 buttons
  all announcing "Lagre i liste", and a saved list of forty was forty announcing "Fjern", with
  nothing to say which row a screen reader user was standing on. Each is now named from two
  elements — its own words, then the row's name cell — so the words stay in the reader's language
  while the variable's name stays Norwegian and marked as such, which a single `aria-label` string
  could not do. The words on the button are unchanged and come first, so speech input still reaches
  them. A row whose variable has left the catalogue borrows the sentence its name cell shows.
  (Fhi.Metadata-6vbwa)
