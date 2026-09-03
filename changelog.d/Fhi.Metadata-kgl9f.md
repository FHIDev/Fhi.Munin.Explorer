category: Notes for hosts

- **Two more sample stand-ins drew at their widest size on every screen.**
  `datasourcecard__heading` was pinned flat at 21px where helsedata.no lets it follow the stepped
  base (21 / 18 / 16), and `datasourcecard__info` was flat at 16px where the real class steps
  16 / 14 / 13. The heading now takes the base like production does and keeps only the weight
  substitution; the info line gets the same two breakpoints as `caption`, which it matches at
  every width.
