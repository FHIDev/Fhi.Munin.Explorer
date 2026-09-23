category: Notes for hosts
- **Stiler does carry a breadcrumb rule, and the detail pages' trail wears it; the two search
  trails deliberately do not.** The documentation shipped with the package said Stiler had no
  breadcrumb rule that could be read back off its compiled stylesheet, and gave that as the reason
  the hierarchy trail over the results and the variable panel's kilde trail carry no class. Stiler
  defines `.breadcrumbs` — with `__list`, `__list-item`, `__homelink`, `__divider` and
  `__last-crumb` — as global unscoped classes, and `DetailTrail` wears the list, list-item, divider
  and last-crumb names. The real reason those two trails stay unclassed is that their steps do not
  navigate: the hierarchy trail's steps narrow the search, and the kilde trail's one button
  discloses the kilde in place, so the breadcrumb vocabulary does not describe either. No markup
  changed: both trails emit the same
  unclassed `<ol>` as before, and the chevrons between their steps are still a host's to draw.
  (Fhi.Metadata-7uqq8)
