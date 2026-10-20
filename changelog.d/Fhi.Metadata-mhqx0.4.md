category: Fixed
- **Keyboard focus is never hidden under the sticky name bar.** Moving backwards through a kilde page with Shift+Tab, with the name bar stuck at the top, could leave the focused link wholly under the bar, out of sight (WCAG 2.2, 2.4.11). The page now scrolls a focused control clear of the bar, at every width.
