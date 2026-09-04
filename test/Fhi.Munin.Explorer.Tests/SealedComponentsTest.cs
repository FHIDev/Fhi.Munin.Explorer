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
/// <para>
/// <c>IComponent</c> rather than <c>ComponentBase</c>, and abstract types included: a public
/// abstract component is the derivable base the policy says does not exist, and a type implementing
/// <c>IComponent</c> directly mounts the same way. A component written as a bare <c>.razor</c> with
/// no code-behind cannot say <c>sealed</c> at all, so the way to answer this test for one is to give
/// it a code-behind partial — every component here already has one.
/// </para>
/// </remarks>
public class SealedComponentsTest
{
    [Fact]
    public void Components_Always_ThenEveryPublishedOneIsSealed()
    {
        var unsealed = typeof(IMuninExplorerClient).Assembly
            .GetExportedTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type)
                           && !type.IsInterface
                           && !type.IsSealed)
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal([], unsealed);
    }
}
