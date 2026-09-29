category: Notes for hosts

- **Stiler 0.1.140 keeps hidden toolbar actions out of the Tab order.** It ensures `.munin-explorer-page__stuckbar[hidden]` has `display: none`. Hosts supplying their own stylesheet need the same rule: `div { display: block }` overrides the browser's default hiding and can leave the compact collection action in the Tab order while hidden from screen readers.
