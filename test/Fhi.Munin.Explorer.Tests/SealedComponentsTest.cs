using System.Reflection;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Every component this package publishes is sealed, and this is what says so.
/// </summary>
/// <remarks>
/// The reason lives in AGENTS.md under "Components are sealed" rather than on eight classes; what
/// lives here is the check, because the decision was reached once and the sealing was silence
/// before it — six were sealed, two were not, and nothing recorded which of those was deliberate
/// (Fhi.Metadata-l9l2n.43). A component added unsealed is not a compile error, and the audience
/// that would notice is a host, after publication.
/// </remarks>
public class SealedComponentsTest
{
    [Fact]
    public void Components_Always_ThenEveryPublishedOneIsSealed()
    {
        var unsealed = typeof(IMuninExplorerClient).Assembly
            .GetExportedTypes()
            .Where(type => typeof(ComponentBase).IsAssignableFrom(type)
                           && !type.IsAbstract
                           && !type.IsSealed)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal([], unsealed);
    }
}
