category: Notes for hosts
- **Datasamling action placement changes while other detail pages keep their existing order.**
  The existing `munin-explorer-page__actions` row now sits between the datasamling identity block
  and hero facts. Its compact counterpart uses Stiler's existing desktop-only rule; no new class
  names are required. `DetailPage` offers `ActionsAfterHeader` and dedicated `StickyActionText`,
  `StickyActionHref` and `StickyAction` parameters, and never copies arbitrary `Actions` markup.
