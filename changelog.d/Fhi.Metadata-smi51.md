category: Fixed
- **An Email field in the details is a mailto link again.** A kilde's E-post read as plain text while
  Hjemmeside beside it was a link (FHIDev/Munin#6896). A field the catalogue types `Email` now links with
  `mailto:` when its whole value is one valid address, by the same rule the access offer already used; anything
  else stays the text it was.
