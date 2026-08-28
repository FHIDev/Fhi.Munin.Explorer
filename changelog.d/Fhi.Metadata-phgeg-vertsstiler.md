category: Notes for hosts
- The page-size buttons now carry `aria-disabled` while a fetch is running, so the existing
  `.munin-explorer-pagination-content [aria-disabled="true"]` rule draws them inert for that
  moment. A host that styles the pager already has this and owes nothing new; one that does not
  will show a control that is inert to the keyboard and to a screen reader but undrawn, the same
  gap the pager's own buttons have without that rule. (Fhi.Metadata-phgeg)
