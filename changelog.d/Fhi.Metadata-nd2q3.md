category: Added
- The reader can choose how many rows a page holds. Three buttons beside the pager — 10, 20 and
  50, Runa's own values, so the two explorers behave alike for the same person — with a matching
  `PageSizeChanged` callback in the same shape as `PageChanged`. `PageSize` is therefore two-way
  now: a host that mirrors it into its URL keeps the choice on a shared link, and one that ignores
  the callback still gets a working control and loses the choice on reload.
- Choosing a size returns the reader to page 1 and raises `PageChanged` with it. A change of size
  renumbers the rows, so keeping the page number would leave someone on page 3 of 15 looking at an
  arbitrary part of the result without anything on screen saying they had been moved. Sizes outside
  1–100 are still clamped rather than refused, and the control reads through the same clamp.
- A failed size change can be retried like any other failed request, and the retry sends the size
  the reader asked for rather than the one the rollback restored. Without that it would refetch the
  old size, succeed and clear the error, reporting a change that never happened — from the one
  control a reader cannot press again once a single-page result has taken the pager away.
