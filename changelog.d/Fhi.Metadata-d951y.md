category: Fixed
- **Local testing documentation correctly names the variables that drive geometry assertions.**
  The `running-locally.md` guide previously stated hand-written counts for the widths and states measured by `check-hostile-host.sh`. These counts had gone stale as the scripts evolved. The text now directs readers to `GEOMETRY_WIDTHS` and `TARGETS` as the sources of truth, ensuring the documentation stays accurate without manual updates. (Fhi.Metadata-d951y)
