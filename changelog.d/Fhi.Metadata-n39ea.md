category: Fixed

- **`VariableView` no longer writes an English reader a Norwegian ordinal dot.** Its sidebar dates
  read "20. Sep 2022" whatever language the host asked for; they now read "20 Sep 2022" in English
  and "20. sep. 2022" in Norwegian. The abbreviated month stays — the sidebar is narrow enough that
  a spelled-out one wraps — and the kilde and datasamling views still spell theirs out.
  (Fhi.Metadata-n39ea)
- **`KildeView.Sections` is documented as what it actually receives.** The XML docs that ship with
  the package said the kilde explorer passes its datasamling hierarchy through this slot and that
  the variable explorer passes a datasamling section: the hierarchy is drawn by the view itself, and
  the variable explorer passes nothing. (Fhi.Metadata-x8sd9)
