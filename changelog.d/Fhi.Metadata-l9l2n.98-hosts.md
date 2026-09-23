category: Notes for hosts
- **`munin-explorer-kilder-scroll--cols-17` and `--cols-18` need a threshold, or the kilder header
  stops sticking with every column on.** With twelve or thirteen optional columns the modifier goes
  past the last threshold Stiler has (`--cols-16` with selection, `--cols-15` without). The fallback
  rule does not catch it either, because the class still contains `--cols-`. So at any width the box
  keeps scrolling sideways and its `thead th` never pins. The sample stylesheets carry estimated
  blocks, at 1916px and 2012px. The measured ones are Stiler's to ship. (Fhi.Metadata-l9l2n.98)
