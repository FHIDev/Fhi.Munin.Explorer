using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Nothing in the package hands text to the renderer as markup: every heading and every catalogue string is built
/// with <c>OpenElement</c> and <c>AddContent</c>, so none of it is parsed as HTML (Fhi.Metadata-oiips.70).
/// </summary>
public partial class MarkupStringGuardTest
{
    [Fact]
    public void Package_WhenScannedForMarkupSinks_ThenNoneIsFound()
    {
        var offenders = Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".razor", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(file => MarkupSinks(File.ReadAllText(file)).Count > 0)
            .Select(file => Path.GetRelativePath(Repo.Root, file));

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("@((MarkupString)$\"<h2>{T.Title}</h2>\")")]
    [InlineData("var m = (Microsoft.AspNetCore.Components.MarkupString)text;")]
    [InlineData("var m = new MarkupString (text);")]
    [InlineData("MarkupString m = new(text);")]
    [InlineData("builder.AddMarkupContent(3, text);")]
    public void MarkupSinks_WhenTheSourceWritesASink_ThenItIsFound(string source) =>
        Assert.NotEmpty(MarkupSinks(source));

    [Theory]
    [InlineData("/// and no <see cref=\"MarkupString\"/> exist anywhere in the path.")]
    [InlineData("// a MarkupString would parse this")]
    [InlineData("@* drawn without a MarkupString *@")]
    [InlineData("/* AddMarkupContent( is the sink */ builder.AddContent(1, text);")]
    [InlineData("builder.AddContent(1, text);")]
    public void MarkupSinks_WhenOnlyACommentNamesASink_ThenNothingIsFound(string source) =>
        Assert.Empty(MarkupSinks(source));

    /// <summary>The sinks <paramref name="source"/> writes outside its comments.</summary>
    internal static IReadOnlyList<string> MarkupSinks(string source)
    {
        var code = Comments().Replace(source, " ");
        return [.. Sinks().Matches(code).Select(match => match.Value)];
    }

    [GeneratedRegex(@"@\*.*?\*@|/\*.*?\*/|//[^\n]*", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Comments();

    [GeneratedRegex(@"\bMarkupString\b|\bAddMarkupContent\s*\(", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sinks();
}
