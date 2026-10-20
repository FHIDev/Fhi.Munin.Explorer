category: Fixed
- **Keyboard focus is never hidden under the sticky name bar.** Moving through a detail page with Tab or Shift+Tab while the name bar is stuck at the top could leave the focused link wholly under the bar, out of sight (WCAG 2.2, 2.4.11). The page now scrolls a control that keyboard focus lands on clear of the bar, at the widths the explorer is tested at.
