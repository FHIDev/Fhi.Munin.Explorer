category: Notes for hosts

- **`munin-explorer-kilder__count--zero` is new, and a host that styles it must dim rather than
  hide.** It joins `munin-explorer-kilder__count` on a kilder-table cell whose count is nought. A
  host that defines no rule for it loses nothing but the emphasis — the cell still reads `0`. A host
  that writes one owes the digit legible contrast: `display: none`, `visibility: hidden` and
  replacing the value with a dash all take away the reader's way of telling a register that counts
  nothing from a field nobody filled in, which the table renders as "Ikke oppgitt". Both sample
  stylesheets show the shape, a colour on the cell and nothing else. (Fhi.Metadata-8vbqf)
