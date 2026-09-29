// Chromium's launch options, shared by every scan that drives a browser so the copies cannot drift.
// Imports nothing from playwright: geometry-scan and tab-stop-scan load it late to report TOOLING.

// PLAYWRIGHT_BROWSER_CHANNEL=msedge runs an installed browser, because `playwright install chromium`
// cannot complete on Node 26 (Fhi.Metadata-wgwa0). Opt-in and unset in CI: a channel renders a
// different engine build, so a geometry number from one is not interchangeable with the other.
export const launchOptions = () => {
  const channel = process.env.PLAYWRIGHT_BROWSER_CHANNEL;
  // A container's small /dev/shm kills a long-lived Chromium mid-scan (Fhi.Metadata-yfmg0).
  const args = ['--disable-dev-shm-usage'];
  return channel ? { channel, args } : { args };
};
