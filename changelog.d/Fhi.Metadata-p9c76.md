category: Fixed

- **A failed search now offers a way out instead of only a sentence.** Both failures reported in
  the explorer's alert region — the result list and the filter counts — gain a retry button of
  their own, inside that region, so a reader no longer has to reload the host's page to get past
  one. The button re-sends the request that failed: the page they were turning to, the ordering
  they asked for and the query the rows came from, rather than a fresh search from whatever is in
  the box. None is offered on a 429, where the sentence beside it says to wait; and once there is
  nothing left to retry the button stays where it is, inert, so it cannot take a keyboard user's
  focus with it. The labels are in both languages and follow the `Language` parameter. Hosts need
  no change: the buttons wear `hd-button-square button-square--ghost`, which
  `Fhi.Helsedata.Stiler` already defines. (Fhi.Metadata-p9c76)
