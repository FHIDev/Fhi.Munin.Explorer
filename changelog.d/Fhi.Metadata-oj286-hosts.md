category: Notes for hosts

- The `munin-explorer-breadcrumb__clear` class name is gone: the control it dressed, the × that
  emptied the hierarchy, is no longer rendered. A host carrying a rule for it can drop it.
  `munin-explorer-breadcrumb` keeps its name and now carries `role="navigation"` and the trail's
  own accessible name — but it holds one child where it held two, so a rule that reached the ×
  through a descendant selector, or laid the wrapper out expecting a second box beside the list,
  wants checking against the markup rather than assuming nothing moved. (Fhi.Metadata-oj286)
