category: Notes for hosts
- **Avansert søk is off until `MuninExplorer:Features:AdvancedSearch` is `true`.** Off, the button, the form and the
  condition chips are not drawn, and a link's `vilkar` conditions are dropped; `FilterChanged` then reports the
  filter without them, so a host mirroring it into the address bar drops them too. It needs a Munin API with
  `GET api/explorer/variables/vilkar`; against an older one the form says avansert søk is unavailable. The form wears
  only Stiler names the explorer already uses (`form-fieldset`, `form-element__label`, `caption`, `infobox`,
  `hd-button-square`) and the existing chip names, so no new rule is needed. A host with its own
  `IMuninExplorerClient` gets no fields until it implements `GetConditionFieldsAsync`.
