category: Fixed
- **The sticky fact bar now follows an in-page jump, not only a scroll.** An `IntersectionObserver`
  reports a crossing, so a press in the contents nav — or any single `window.scrollTo` past the hero
  fact row — left the bar exactly as it was: no bar deep in the page, or a pinned copy of the page
  title over a page already showing its own. The module re-reads the row's position directly on
  `scrollend` and on `hashchange`, so hosts need change nothing. (Fhi.Metadata-14j7i)
