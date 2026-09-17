category: Notes for hosts
- **`munin-explorer-page__stuckbar` and three names beside it are new, and `Fhi.Helsedata.Stiler`
  0.1.79 already carries rules for all four.** The others are `munin-explorer-page__stuckbar--on`,
  the shown state the browser module writes, `munin-explorer-page__stuckbar-inner` and
  `munin-explorer-page__stuckbar-name`. A host on
  0.1.79 or later needs to do nothing: the rules are in the same `components/munin-explorer/_page.scss`
  the rest of the chassis landed in, and both sample stylesheets stand in at exactly what that pin
  declares. Handles, all four — but read what an undefined one costs, because it is not the usual
  answer: the bar is **not** a permanently visible band without them, since the markup renders it
  `hidden` and only the module ever takes that off. Undefined, it is a block in ordinary flow at the
  top of the page — which the reader has already scrolled past by the time the module shows it — so
  what is lost is a pinned bar and what is gained is a layout jump. On an older Stiler, either take
  the rules or write `position: sticky; top: 0` for the name yourself. (Fhi.Metadata-35w0p.28)
- **The bar is a repeat, and a host may safely have none of it.** It renders `hidden` with
  `aria-hidden="true"`, and `_content/Fhi.Munin.Explorer/explorer-interop.js` is the only thing that
  shows it — so a Content-Security-Policy that blocks the module, or a reader with JavaScript off,
  gets a page with no bar and no word missing. Do not style it visible: everything in it is already
  on the page above, and a second permanent copy of the page's own title is what the hidden state is
  there to prevent. If you fill `DetailPage.Actions`, note that the row is now drawn twice — once
  above the name block and once inside the bar — so anything in it carrying a DOM `id` of your own
  will be on the page twice. (Fhi.Metadata-35w0p.28)
