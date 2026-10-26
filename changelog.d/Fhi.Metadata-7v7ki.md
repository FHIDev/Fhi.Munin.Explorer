category: Fixed
- **A filter pressed while the explorer is loading is applied when the load ends.** It was
  dropped: the box stayed ticked over results that ignored it, and on a cold cache that could
  last ten seconds. The press is now held (the last one wins, and presses made meanwhile
  combine), its box shows it at once, and it is applied as soon as the fetch in flight ends.
  The filter panel also says «Oppdaterer treff og filtre …» while it loads, in a `caption`
  paragraph under its legend.
