category: Fixed

- **The disclosure chevron on a Runa variable row is now clickable.** It used to render as a
  decorative sibling of the row's toggle button, so a reader aiming at the chevron hit nothing;
  it now sits inside the button, still `aria-hidden`, so the button's accessible name and
  `aria-expanded` are unchanged and there is exactly one control. (Fhi.Metadata-zqe14)
