category: Fixed

- **A throttled download names the cause, instead of reading as a plain failure.**
  `ExportListAsync` sent its own request rather than going through the client's shared write
  helper, so the one write added after 429 handling landed never inherited it: a rate-limited
  export arrived as a plain `HttpRequestException`, and the list view answered "kunne ikke laste
  ned" with nothing to say why. It now raises `MuninExplorerRateLimitedException` like every other
  call, and the view tells the reader they have asked too often. (Fhi.Metadata-3gzw5)
