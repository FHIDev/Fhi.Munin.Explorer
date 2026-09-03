category: Changed

- **The count beside a facet value is now its own element in both explorers**, a
  `<span class="munin-explorer-filters__count">` inside the value's `<label>`, where it used to be
  part of the label's text run. The visible text is unchanged — `Biobank (1)`, parentheses and all
  — and so is the checkbox's accessible name, which still holds the count. Hosts that want the
  number dimmed or right-aligned can now style it on its own. (Fhi.Metadata-cgk85)
