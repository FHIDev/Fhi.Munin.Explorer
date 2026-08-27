category: Fixed

- **A failed search now offers a way out instead of only a sentence.** Both failures reported in
  the explorer's alert region — the result list and the filter counts — gain a retry button of
  their own, inside that region, so a reader no longer has to reload the host's page to get past
  one. The button re-sends the request that failed: the page they were turning to, the ordering
  they asked for, the filter they picked and the query the rows came from, rather than a fresh
  search from whatever is in the box. A retried search or filter change brings the facet counts
  back into agreement with the rows it fetched — including after a failed first load, which leaves
  the filter panel off the page entirely until they arrive — while a retried page turn or sort
  leaves them alone, because neither moves them. The host is told what actually moved and nothing
  else, so a retried page turn does not push three spurious history entries at a host that mirrors
  each callback into a URL. None is offered on a 429, where the sentence beside it says to wait;
  and once there is nothing left to retry the button stays where it is, inert, so it cannot take a
  keyboard user's focus with it — until the next fetch started elsewhere settles, answered or
  throttled, which is when a dead offer would otherwise start being announced beside every later
  failure in that atomic region. The labels are in both languages and follow the `Language` parameter. (Fhi.Metadata-p9c76)
