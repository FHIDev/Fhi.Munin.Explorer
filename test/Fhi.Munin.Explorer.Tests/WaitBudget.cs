using System.Runtime.CompilerServices;
using Bunit;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>What every <c>WaitForAssertion</c> and <c>WaitForElement</c> in this assembly is given.</summary>
/// <remarks>
/// bUnit's 1s default covers a thread-pool hop plus a render, and reproduced under a two-core load
/// the wait reported "Check count: 0" — it never got a slice. A longer budget only delays a real
/// failure's report; it cannot make a broken component pass. (Fhi.Metadata-p7est)
/// </remarks>
internal static class WaitBudget
{
    internal static readonly TimeSpan Allowed = TimeSpan.FromSeconds(5);

    // Assembly-wide because BunitContext.DefaultWaitTimeout is static: a base class would set the
    // same global from every test, and a class written without it would still be free to skip.
    [ModuleInitializer]
    internal static void Raise() => BunitContext.DefaultWaitTimeout = Allowed;
}
