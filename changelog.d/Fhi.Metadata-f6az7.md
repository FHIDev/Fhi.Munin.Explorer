category: Changed

- **The column picker is now real checkboxes, and its trigger sits on the right.** Each optional
  column is an `<input type="checkbox">` inside a `label.form-control`, where it used to be a
  `<button aria-pressed>` - a checkbox reads as a multi-select and a pressed button as a toolbar
  toggle, and assistive technology announces them differently. The trigger gained helsedata's own
  two icons, `icon-layout` leading and a chevron that follows the open state, and no longer emits
  an inline `style="position:relative"`: Stiler positions `.munin-explorer__dropdown` itself. Both
  explorers draw this control from one copy, so the kildeutforsker and the variabelutforsker
  change together. (Fhi.Metadata-f6az7)
