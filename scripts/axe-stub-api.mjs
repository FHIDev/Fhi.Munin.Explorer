// The API the accessibility scan reads, served from the contract-drift fixtures.
//
// runa.munin.skytest.fhi.no is geo-filtered: Norwegian traffic is admitted and a GitHub runner is
// not (#127). A scan pointed there renders an empty shell, and axe reports no violations in a page
// with nothing on it — which is what this gate did until Fhi.Metadata-wr31i.
//
// The fixtures are reused rather than copied: one set for a human to re-capture when a drift
// report asks for it, instead of a second set here that nothing would ever look at again.
//
// Usage:  node scripts/axe-stub-api.mjs <port>
import { createServer } from 'node:http';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const port = Number(process.argv[2]);
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const fixtures = join(root, 'test', 'Fhi.Munin.Explorer.Tests', 'Testdata');

if (!Number.isInteger(port) || port <= 0) {
  console.error('usage: node axe-stub-api.mjs <port>');
  process.exit(2);
}

// One entry per route the explorer calls, longest first so `variables/{id}` cannot swallow
// `variables/{id}/timeline`. The literal is the one route with no fixture, and it answers with
// what the client would have fallen back to anyway: an empty vocabulary.
const routes = [
  [/^\/api\/explorer\/kilder\/e358db40-0efa-47bb-893a-40ee00ccde12$/, 'kilde-med-delkilder.json'],
  [/^\/api\/explorer\/variables\/[^/]+\/kodeverk\/[^/]+\/[^/]+\/codes$/, 'kodeverk-codes.json'],
  [/^\/api\/explorer\/variables\/[^/]+\/timeline$/, 'timeline.json'],
  [/^\/api\/explorer\/variables\/[^/]+$/, 'variable.json'],
  [/^\/api\/explorer\/variables$/, 'variables.json'],
  [/^\/api\/explorer\/filters$/, 'filters.json'],
  [/^\/api\/explorer\/kilder\/egenskaper$/, '[]'],
  [/^\/api\/explorer\/kilder\/[^/]+\/hierarchy$/, 'hierarchy.json'],
  [/^\/api\/explorer\/kilder\/[^/]+$/, 'kilde.json'],
  [/^\/api\/explorer\/kilder$/, 'kilder.json'],
  [/^\/api\/explorer\/datasamling\/[^/]+$/, 'datasamling.json'],
  [/^\/api\/explorer\/my\/lists\/[^/]+\/variables$/, 'my-list-variables.json'],
  [/^\/api\/explorer\/my\/lists$/, 'my-lists.json'],
];

// Read at startup: a renamed fixture should stop the stub here, where the message is about a
// missing file, rather than per request as a 404 the page renders as an empty panel.
const bodies = new Map();
for (const [pattern, source] of routes) {
  bodies.set(pattern, source.startsWith('[') ? source : readFileSync(join(fixtures, source), 'utf8'));
}

// The hierarchy capture belongs to Tromsø, which the short list capture omits. Make that source
// reachable without relabelling another source's hierarchy or changing the captured JSON files.
const study = JSON.parse(readFileSync(join(fixtures, 'kilde-med-delkilder.json'), 'utf8'));
const listRoute = routes.find(([, source]) => source === 'kilder.json')[0];
const countCollections = node => node.datasamlinger.length +
  (node.delkilder ?? node.children ?? []).reduce((count, child) => count + countCollections(child), 0);
bodies.set(listRoute, JSON.stringify([
  ...JSON.parse(bodies.get(listRoute)),
  { ...study, navn: study.preferredTerm, aktiv: true, harVariabelbeskrivelse: study.totalVariables > 0,
    datasamlingCount: countCollections(study), delkildeCount: study.delkilder.length },
]));

// The one route whose fixture cannot be served verbatim. my-list-variables.json is a real capture:
// 247 entries reported, two of them kept. Served as-is for every page, it says "page 1 of 3" every
// time, and VariableListState walks every page of the active list — so the walk never advances and
// the circuit asks forever (measured at ~7000 requests a second). The counts are made to agree with
// the entries actually being served instead.
const listVariables = /^\/api\/explorer\/my\/lists\/[^/]+\/variables$/;

function pagedListVariables(body, query) {
  const page = Math.max(1, Number(query.get('page') ?? 1) || 1);
  const size = Math.max(1, Number(query.get('pageSize') ?? 100) || 100);
  const items = JSON.parse(body).items ?? [];
  const slice = items.slice((page - 1) * size, page * size);

  return JSON.stringify({
    items: slice,
    totalCount: items.length,
    page,
    size,
    totalPages: Math.max(1, Math.ceil(items.length / size)),
  });
}

// A fetch held open for a while, one request per hold asked for and none unless asked. It is how
// scripts/state-scan.mjs stages a press the component DROPS - `if (_loading) return` - which no
// browser can stage on its own: the request is the HOST's, made over the circuit, so Playwright's
// route interception never sees it and cannot slow it down.
//   POST /__stub/hold-next?path=/api/explorer/variables&ms=6000
//   GET  /__stub/hold-next  ->  {"held":[{"path":...,"ms":...}]}
//   DELETE /__stub/hold-next  ->  drops every unspent hold, for a staging that armed one and threw
// Read back rather than assumed spent: a hold nothing ever asked for would leave a press that was
// never in flight looking exactly like one that was.
const held = [];

function control(url, request, response) {
  if (request.method === 'POST') {
    const path = url.searchParams.get('path');
    const ms = Number(url.searchParams.get('ms'));
    if (path === null || !path.startsWith('/') || !Number.isInteger(ms) || ms <= 0) {
      response.writeHead(400, { 'content-type': 'application/json' }).end('{"error":"path,ms"}');
      return;
    }
    held.push({ path, ms });
  }

  if (request.method === 'DELETE') {
    held.length = 0;
  }

  response.writeHead(200, { 'content-type': 'application/json' }).end(JSON.stringify({ held }));
}

function serve(url, request, response) {
  const path = url.pathname;
  const route = routes.find(([pattern]) => pattern.test(path));

  if (route === undefined) {
    // Loud, because a route nobody serves renders as an empty panel that axe is happy with.
    console.error(`stub: no fixture for ${request.method} ${path}`);
    response.writeHead(404, { 'content-type': 'application/json' }).end('null');
    return;
  }

  const body = listVariables.test(path)
    ? pagedListVariables(bodies.get(route[0]), url.searchParams)
    : bodies.get(route[0]);

  response.writeHead(200, { 'content-type': 'application/json' }).end(body);
}

const server = createServer((request, response) => {
  const url = new URL(request.url, 'http://localhost');

  if (url.pathname === '/__stub/hold-next') {
    control(url, request, response);
    return;
  }

  const holding = held.findIndex(one => one.path === url.pathname);
  if (holding < 0) {
    serve(url, request, response);
    return;
  }

  const [{ ms }] = held.splice(holding, 1);
  console.error(`stub: holding ${request.method} ${url.pathname} for ${ms}ms, as asked`);
  setTimeout(() => serve(url, request, response), ms);
});

server.listen(port, '127.0.0.1', () => console.log(`stub: serving the Testdata fixtures on ${port}`));
