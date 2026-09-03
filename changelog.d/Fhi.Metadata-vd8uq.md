category: Notes for hosts

- **The sample hosts rendered every unstyled string at 16px, where helsedata.no's inherited base
  steps 21px / 18px / 16px.** Stiler sets the base on `body` and steps it at 2881px and 767px;
  the samples carried only the smallest value, flat, so anything that does not declare its own
  size read 2px small on an ordinary desktop and 5px small above 2881px. The root font-size is
  untouched, so `rem` values are unaffected — only the inherited size moves. A host reading the
  samples for its own base rule should take the two breakpoints with it.
