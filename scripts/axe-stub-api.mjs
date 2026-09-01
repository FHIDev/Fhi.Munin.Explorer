// The API the accessibility scan reads, served from the drift fixtures instead of the network.
//
// runa.munin.skytest.fhi.no is not reachable from a GitHub runner — the nightly contract-drift job
// records API-UNREACHABLE against it — so a scan pointed there renders an empty shell, and axe
// finds no violations in a page with nothing on it (Fhi.Metadata-wr31i).
//
// The fixtures are the contract-drift ones, and that nightly job is what keeps them honest against
// the real API. Reusing them beats a second copy here that nothing would ever re-capture.
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
// `variables/{id}/timeline`. The literal is the one route with no fixture: an empty vocabulary is
// already what the client falls back to when that call 404s.
const routes = [
  [/^\/api\/explorer\/variables\/[^/]+\/timeline$/, 'timeline.json'],
  [/^\/api\/explorer\/variables\/[^/]+$/, 'variable.json'],
  [/^\/api\/explorer\/variables$/, 'variables.json'],
  [/^\/api\/explorer\/filters$/, 'filters.json'],
  [/^\/api\/explorer\/kilder\/egenskaper$/, '[]'],
  [/^\/api\/explorer\/kilder\/[^/]+\/hierarchy$/, 'hierarchy.json'],
  [/^\/api\/explorer\/kilder\/[^/]+$/, 'kilde.json'],
  [/^\/api\/explorer\/kilder$/, 'kilder.json'],
  [/^\/api\/explorer\/datasamling\/[^/]+$/, 'datasamling.json'],
  [/^\/api\/explorer\/my\/lists$/, 'my-lists.json'],
];

// Read at startup: a renamed fixture should stop the stub here, where the message is about a
// missing file, rather than per request as a 404 the page renders as an empty panel.
const bodies = new Map();
for (const [pattern, source] of routes) {
  bodies.set(pattern, source.startsWith('[') ? source : readFileSync(join(fixtures, source), 'utf8'));
}

const server = createServer((request, response) => {
  const path = new URL(request.url, 'http://localhost').pathname;
  const route = routes.find(([pattern]) => pattern.test(path));

  if (route === undefined) {
    // Loud, because a route nobody serves renders as an empty panel that axe is happy with.
    console.error(`stub: no fixture for ${request.method} ${path}`);
    response.writeHead(404, { 'content-type': 'application/json' }).end('null');
    return;
  }

  response.writeHead(200, { 'content-type': 'application/json' }).end(bodies.get(route[0]));
});

server.listen(port, '127.0.0.1', () => console.log(`stub: serving the Testdata fixtures on ${port}`));
