using System.Reflection;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class DetailSectionIdsTest
{
    /// <summary>Every string constant the class declares, public and internal alike.</summary>
    private static IReadOnlyList<(string Name, string Value)> Constants() =>
        [.. typeof(DetailSectionIds)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (field.Name, (string)field.GetRawConstantValue()!))];

    [Fact]
    public void Constants_Always_ThenStartWithTheMuninExplorerPrefix()
    {
        // A bare id shares the host page's namespace, so a host's own id="source" and ours were one
        // element to the browser (Fhi.Metadata-uobxg). Read by reflection so a constant added later
        // cannot skip the prefix; the count stops this passing on a class that lost its fields.
        var constants = Constants();

        Assert.NotEmpty(constants);
        Assert.All(constants, constant =>
            Assert.True(constant.Value.StartsWith("munin-explorer-", StringComparison.Ordinal),
                        $"{constant.Name} = \"{constant.Value}\" is not under the munin-explorer- prefix."));
    }

    [Fact]
    public void ReserveGroupId_Always_ThenTheIdIsUnderTheGroupPrefix()
    {
        Assert.Equal("munin-explorer-section-om-registeret",
                     DetailSectionIds.ReserveGroupId("om-registeret", new HashSet<string>(StringComparer.Ordinal)));
    }
}
