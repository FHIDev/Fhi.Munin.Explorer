// The package's one browser module, imported by ExplorerInterop and by nothing else.
//
// Importing it must stay free of side effects — no listeners, no DOM writes — because every
// caller carries on without it and a module that changed the page on import would not be optional.

/**
 * The package version the mounted explorer reports, read off the `data-munin-explorer-version`
 * every view root carries. Null when no explorer is on the page.
 *
 * @returns {string | null}
 */
export function packageVersion() {
  const root = document.querySelector('[data-munin-explorer-version]');

  return root === null ? null : root.getAttribute('data-munin-explorer-version');
}
