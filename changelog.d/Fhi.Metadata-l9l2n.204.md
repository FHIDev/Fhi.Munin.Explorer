category: Fixed
- **«Del variabel» and «Kopier lenke» name the row's datasamling by code, the primary one included.** A shared link
  read `?variabel=<code>` alone for the row of the variable's primary datasamling, and `?datasamlingId=<id>` for any
  other, an id that differs between environments, so a link to a variable in many datasamlinger opened the wrong row
  elsewhere. Links now read `?variabel=<code>&datasamling=<code>`, and an arriving datasamling code is matched among the
  variable's datasamlinger as Munin's list import matches one, with its «K_». A code that names none of them opens the
  primary row, and says so when the variable has more than one datasamling and Munin sends their codes. `?variabel=`
  alone, `?datasamlingId=` and `?variabelId=` links keep opening as before, and against a Munin that does not yet send a
  datasamling's code the links are written as before. (Fhi.Metadata-l9l2n.204)
