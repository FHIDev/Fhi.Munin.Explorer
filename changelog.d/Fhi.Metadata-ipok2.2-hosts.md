category: Notes for hosts
- **Avansert søk is off until `MuninExplorer:Features:AdvancedSearch` is `true`.** Off, the button, the form and the
  condition chips are not drawn, and a link's `vilkar` conditions are dropped; `FilterChanged` then reports the
  filter without them, so a host mirroring it into the address bar drops them too. It needs a Munin API with
  `GET api/explorer/variables/vilkar`; against an older one the form says avansert søk is unavailable. The form wears
  names Stiler already defines (`form-fieldset`, `form-element__label`, `caption`, `infobox`,
  `hd-button-square` with `margin-bottom` and `margin-right`, `form-group` on the form and `form-element
  input__container` around each label and its control, `input__field` on its selects and its text and date fields, `input__field--small` on
  the text one) and the existing chip names, plus two names of its own: `munin-explorer-advanced-search` on the box
  around the form, which wants a white background, a 1px border, 8px radius, 24px padding (15px at the bottom) and
  24px below it, so the «Skjul filtre» toggle under it is not read as one of its buttons; and `munin-explorer-advanced-search__action`
  on «Fjern betingelse» and «Legg til betingelse», which wants `width: 12.75rem; max-width: 100%` so they line up with
  «Verdi», and 15px above «Fjern betingelse» inside a condition (Stiler: Fhi.Metadata-ipok2.9). A condition chip's
  text can be long, so the shared chip rule wants a radius that keeps it inside the chip when it wraps (16px in
  Stiler, the same bead). A host with its own
  `IMuninExplorerClient` gets no fields until it implements `GetConditionFieldsAsync`. The `vilkar` query key is the
  package's from now on, in `VariableFilter.QueryKeys` and `ExplorerUrlState.QueryKeys`, whether the feature is on or
  off: a page that mounts the explorer cannot use a `vilkar` parameter of its own.
