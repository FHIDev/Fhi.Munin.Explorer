// Which Varde «Tilgjengelighet» page each axe state scans, so a run can report per page (DevInfra-og3.42).
// The keys are Varde's (varde/config/a11y-pages.json); a state not listed here fails test-axe-pages.mjs.

const PAGES = {
  'explorer/variabelutforsker': [
    'variables-list', 'utforsker-list', 'variables-facets', 'facet-cap', 'filters-level-lines', 'explorer-search-code',
    'filters-node-icons-off', 'variables-shared-name', 'variables-filters-collapsed', 'explorer-tabs',
    'explorer-save-all-confirm', 'explorer-save-all-too-many', 'explorer-saved-all', 'search-download-open',
    'tree-collapsed', 'tree-populated', 'tree-empty-results', 'tree-no-match', 'variables-advanced-form',
    'variables-advanced-chips',
  ],
  'explorer/variabel': [
    'variable-detail', 'variable-detail-long-name', 'variable-statistics', 'variable-statistics-suppressed',
    'variable-detail-about', 'variable-whole', 'variable-page', 'variable-stuckbar',
    'variable-kilde-hierarchy', 'variable-kilde-hierarchy-icons-off',
  ],
  'explorer/variabelliste': [
    'explorer-list-tab', 'list-row-panel', 'list-actions-menu', 'list-actions-and-columns', 'list-download-and-columns',
    'list-row-notes', 'list-row-picker', 'list-row-picker-off',
  ],
  'explorer/kildeutforsker': [
    'kilder-list', 'kildeutforsker-list', 'kilder-ticked', 'kilder-filters-collapsed', 'kilde-facet-ticked',
    'kilder-expanded', 'kilder-columns', 'kilder-counts', 'kilder-every-column', 'kilde-facets',
  ],
  'explorer/kilde': [
    'kilde-drilldown', 'kilde-stuckbar', 'kilde-hierarchy-collapsed', 'kilde-hierarchy-expanded', 'kilde-hierarchy-open',
    'kilde-hierarchy-metadata', 'kilde-hierarchy-deep', 'kilde-hierarchy-empty', 'kilde-hierarchy-error',
  ],
  'explorer/datasamling': ['kilde-datasamling', 'variable-datasamling'],
};

export const pageOfState = new Map(Object.entries(PAGES).flatMap(([page, list]) => list.map(state => [state, page])));

// A plain address with no state: the explorer it mounts.
const PATH_PAGES = { '/': 'explorer/variabelutforsker', '/utforsker': 'explorer/variabelutforsker', '/kilder': 'explorer/kildeutforsker' };

/** The Varde page key for one scanned target, or null when nothing names it. */
export function pageOf(url, state) {
  if (state !== null) return pageOfState.get(state) ?? null;
  return PATH_PAGES[new URL(url).pathname.replace(/\/+$/, '') || '/'] ?? null;
}

/** One results line: what a scan of one state at one width found, under the page it belongs to. */
export function resultLine(url, state, width, violations) {
  return { page: pageOf(url, state), state, width, violations };
}

/** The line axe-scan writes last, and only when it reached its own verdict. */
export const scanFinishedLine = { scan: 'finished' };

/** The WCAG success criteria an axe violation's tags name: wcag143 is 1.4.3; level tags like wcag2aa are not criteria. */
export function criteriaOf(tags) {
  return [...new Set(tags.map(tag => /^wcag(\d)(\d)(\d{1,2})$/.exec(tag)).filter(Boolean).map(([, a, b, c]) => `${a}.${b}.${c}`))].sort();
}
