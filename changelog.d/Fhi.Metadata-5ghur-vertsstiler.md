category: Notes for hosts

- **Wire `ExploreVariablesRequested` or you get no selection column.** The checkbox column, the
  count and both buttons are drawn only when that callback has a delegate, because the ticks exist
  to reach a page only you can name — a column over a button that leads nowhere would cost the
  reader the work of choosing before telling them there was nothing to choose for. Note what that
  means for a mount point that is not fully interactive: an `EventCallback` serialises to an empty
  delegate across a static-SSR to interactive-island boundary, so the column disappears rather
  than showing a button that silently never fires. Both sample hosts show the wiring — ModernHost
  navigates with its router, LegacyHost forces a load because an MVC view has none.
  (Fhi.Metadata-5ghur)
- **One class name to style if you are not on Stiler**: `munin-explorer-kilder__select`, on the
  checkbox column's header cell and on every row's. The declaration it needs is a **width** — a
  table shares itself out between its columns, so one holding a single checkbox otherwise takes
  the same share as Dataansvarlig and squeezes the eight columns that carry words. Both sample
  hosts' `host.css` carries `width: 1%` for it, right after the kilde list's count rule; a test in
  this repository asserts that the rule is a width and not merely a rule. The boxes themselves
  wear no class — a bare `<input type="checkbox">` is an element every stylesheet already dresses,
  the same call the facet panel makes. (Fhi.Metadata-5ghur)
