using Bunit;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The bUnit context every component test in this suite renders in: bUnit's own, with a browser
/// that answers an unplanned call instead of throwing.
/// </summary>
/// <remarks>
/// <para>
/// Strict is bUnit's default, and under it any JS call a test has not planned is an exception —
/// so which component happens to reach the browser becomes a fact every test class that renders it
/// has to know. Several already do: the address bar's <c>history.replaceState</c>, the focus calls,
/// the module import, and since <c>Fhi.Metadata-35w0p.28</c> the sticky bar's observer, which
/// <c>DetailPage</c> reaches for and so every detail view reaches for.
/// </para>
/// <para>
/// Loose is also what makes a bUnit render the JS-ABSENT case, which several tests rely on: the
/// module is never really imported here, nothing rendered depends on it, and a page that needs it
/// to be whole fails rather than passing on a stand-in. Plan a call explicitly — with
/// <c>JSInterop.SetupModule</c> — where a test is about what reached the browser.
/// </para>
/// </remarks>
public abstract class ExplorerTestContext : BunitContext
{
    protected ExplorerTestContext() => JSInterop.Mode = JSRuntimeMode.Loose;
}
