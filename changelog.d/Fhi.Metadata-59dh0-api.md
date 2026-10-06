category: Added
- **`VariableFilter.VariabelgruppeScopes` selects a variabelgruppe under one owner.** Each `VariabelgruppeScope` pairs a group with the kilde, delkilde or datasamling it was chosen under. It travels as `variabelgruppeScopes=<vgId>:<ownerId>`, the API's own parameter, is part of `ExplorerUrlState.QueryKeys`, and is capped at 50 as the API requires.
