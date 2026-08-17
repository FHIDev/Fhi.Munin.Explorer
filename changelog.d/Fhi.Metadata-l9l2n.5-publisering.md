category: Added
- Tag-triggered publishing to nuget.org: push a `v*` tag and the three packages are built,
  tested, asserted and pushed in dependency order. The workflow refuses a tag that is not on
  `main`, a malformed version, and a build whose packed version disagrees with the tag. A push
  that fails partway is finished by re-running it — it publishes only what is still missing,
  and stops if the whole version is already out.
- `scripts/assert-package-contents.sh` checks each package has exactly the intended contents —
  no more and no less — and runs on every PR as well as before publishing. It is what would
  catch a stylesheet appearing in the RCL, which is supposed to carry no CSS at all.
