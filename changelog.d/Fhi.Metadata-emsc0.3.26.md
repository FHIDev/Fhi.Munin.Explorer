category: Fixed
- **Removing the only row on a later page of a saved list no longer leaves an empty page.** A signed-in reader who
  pressed «Fjern» on the one row of page 2 or later was shown «Denne listen er tom.» under a count that still named the
  remaining variables, with focus on the list menu. The view now steps back to the last page that has rows, and focus
  goes to that page's last row, as in the signed-out browser list.
