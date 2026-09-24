category: Notes for hosts
- **The opened variable row's panel now draws a heading carrying `munin-explorer-meta__heading`** -
  an `h3` under the default `HeadingLevel` of 2 (one level below the component's own title), at the
  top of `.munin-explorer-meta`. Stiler styles it from 0.1.112 with
  `.munin-explorer-meta .munin-explorer-meta__heading { font: $heading-xxs; margin: 0 0 8px;
  overflow-wrap: anywhere }`. A host without Stiler has to write that rule itself: unstyled, the
  heading takes the browser's `h3` size, and a long name with no spaces in it pushes the page wider
  than a 320px viewport. (Fhi.Metadata-yaco2)
