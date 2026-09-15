category: Notes for hosts
- **An `<h2>` now renders inside the contents nav, and it carries no class of its own.** The nav was
  `<nav aria-label>` wrapping `ul.form-menu__list`; it is now that same nav with a heading in front
  of the list. The element is deliberately bare, because helsedata's own contents nav draws an
  unclassed `<h2>` in exactly this position and `Fhi.Helsedata.Stiler` already styles it there — a
  new class name would have needed a new rule, and a name with no rule renders at the browser's own
  heading size. A host carrying neither gets a default `<h2>` above the list, which is a title above
  the thing it titles rather than anything broken. (Fhi.Metadata-l9l2n.116)
