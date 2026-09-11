category: Fixed
- **The variable panel's kildesti no longer ends on one datasamling when the variable sits in
  several.** The trail read the primary `datasamlingName` alone, so a variable in nineteen
  datasamlinger was written up under one — not visibly incomplete, but confidently singular. The
  last step now counts them ("19 datasamlinger"), and a "Datasamlinger" list beside the trail names
  every one with its own validity period, which is what the whole-variable view one press further in
  has always shown. A variable in exactly one still reads as that name, and a payload carrying no
  list still falls back to the primary name. (Fhi.Metadata-l9l2n.108)
