category: Deprecated
- **`SharedList.CodeLength` is obsolete and will be removed in 3.0.0.** A share code has 6 or 10 characters now, so
  one length no longer describes it; check a code with `SharedList.NormalizeCode` instead. Nothing in the package or
  in helsedata reads the constant. A host that does now gets warning CS0618, which fails the build under
  `TreatWarningsAsErrors`. (Fhi.Metadata-ayqlp.7)
