category: Fixed
- **The closed Variabelliste panel is no longer a Tab stop.** It carried `tabindex="0"` beside
  `hidden`, and under Stiler's bare `div { display: block }` a hidden panel stays focusable, so the
  last Tab on the variable explorer page landed on a 0px box. The panel now takes `tabindex="0"`
  only while its tab is open, as the WAI-ARIA tabs pattern has it; the drawer's Data / Om variabelen
  panel was one panel that is never hidden, and is unchanged.
