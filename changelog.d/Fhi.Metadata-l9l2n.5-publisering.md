category: Added
- Tag-triggered publishing to `Fhi.Helsedata.no`, helsedata's internal Azure Artifacts feed:
  push a `v*` tag and the package is built, tested, asserted and pushed. Nothing goes to
  nuget.org. The workflow refuses a tag that is not on `main`, a malformed version, and a build
  whose packed version disagrees with the tag. It also refuses a version that is already on the
  feed: the feed does allow one to be deleted, but whoever restored it keeps what they got, so a
  version number that has gone out is spent.
- `scripts/assert-package-contents.sh` checks the package has exactly the intended contents —
  no more and no less — and runs on every PR as well as before publishing. It is what would
  catch a stylesheet appearing in the RCL, which is supposed to carry no CSS at all.
