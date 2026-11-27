using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The bUnit context every component test here renders in, with a browser that answers an unplanned call.</summary>
/// <remarks>Loose renders the JS-absent case several tests rely on, where strict makes every unplanned call an exception. Plan a
/// call with <see cref="PlanExplorerModule"/>, never a second <c>SetupModule</c>; a second context constructs this, not a strict <see cref="BunitContext"/>.</remarks>
public class ExplorerTestContext : BunitContext
{
    public ExplorerTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        PlanExplorerModule();
    }

    /// <summary>Plans the package's module with the press watch answered, so it stops asking on every render.</summary>
    /// <remarks>A loose false re-imports each render and grows the call list while a test reads it (Fhi.Metadata-emsc0.3.27).
    /// A second <c>SetupModule</c> replaces this plan, so tests plan the module here; their own <c>Setup</c> still wins.</remarks>
    protected BunitJSModuleInterop PlanExplorerModule()
    {
        var module = JSInterop.SetupModule(ExplorerInterop.ModulePath);
        module.Setup<bool>("watchPresses").SetResult(true);

        return module;
    }
}
