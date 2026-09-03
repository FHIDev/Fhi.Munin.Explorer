category: Notes for hosts

- **The sample hosts drew Stiler's `headline-3`, `form-element__label` and `caption` at their
  desktop base size on every screen.** Stiler steps all three down at 2881px and again at 767px;
  the stand-ins carried only the base, so the samples rendered them 32px/21px/16px throughout
  where helsedata.no renders 28px/18px/14px on an ordinary desktop and 24px/16px/13px below
  768px. `headline-3` is what the component pins its own title with, so a host checking heading
  hierarchy in a sample was checking a title up to 8px too large. A host taking its rules from
  the samples should take both breakpoints with them.
