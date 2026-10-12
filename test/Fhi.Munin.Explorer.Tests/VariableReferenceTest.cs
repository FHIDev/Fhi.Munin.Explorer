using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Which values of «Variabelreferanse» count as an address to link to (Fhi.Metadata-ot99a).</summary>
public class VariableReferenceTest
{
    private static Dictionary<string, string?> Bag(string? value) => new() { [VariableReference.Key] = value };

    [Theory]
    [InlineData("https://eksempel.no/variabler", "https://eksempel.no/variabler")]
    [InlineData("  http://eksempel.no/variabler  ", "http://eksempel.no/variabler")]
    public void Of_WhenTheValueIsAWebAddress_ThenItIsTheAddress(string stored, string expected) =>
        Assert.Equal(expected, VariableReference.Of(Bag(stored)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("se nettsiden vår")]
    [InlineData("eksempel.no/variabler")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://eksempel.no/variabler")]
    public void Of_WhenTheValueIsNotAnHttpAddress_ThenThereIsNoReference(string? stored) =>
        Assert.Null(VariableReference.Of(Bag(stored)));

    [Fact]
    public void Of_WhenTheKeyIsAbsentOrTheBagIsNull_ThenThereIsNoReference()
    {
        Assert.Null(VariableReference.Of(new Dictionary<string, string?>()));
        Assert.Null(VariableReference.Of(null));
    }
}
