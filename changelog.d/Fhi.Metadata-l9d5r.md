category: Fixed
- A multilingual catalogue field now shows **every** language it holds rather than the reader's
  alone, each on its own line and each named. The bag the catalogue stores these in is open while
  the page offers two languages, so a slot in any third was unreachable by construction — no
  toggle on the page could ever have selected it. Fields holding both Norwegian and English are
  the common case today: 39 of them across some 20 kilder.
- A value in a language this package cannot name is now marked with the language tag the
  catalogue used, instead of the reader's. Marking it with the reader's left `lang` off the
  element altogether, so the text inherited the host's and a Norwegian page announced German as
  Norwegian to a screen reader (WCAG 3.1.2). The same fix covers an English-only value on a
  Norwegian page, which is reachable in today's catalogue rather than hypothetical.
