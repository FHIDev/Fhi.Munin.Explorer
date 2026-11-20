using System.Text.RegularExpressions;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The package's C# and Razor write no <c>MarkupString</c> and no <c>AddMarkupContent</c> (Fhi.Metadata-oiips.70).
/// A text scan, not a parser: the PR names the shapes it cannot see.
/// </summary>
public partial class MarkupStringGuardTest
{
    [Fact]
    public void Package_WhenScannedForMarkupSinks_ThenNoneIsFound()
    {
        var scanned = Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".razor", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Repo.Root, file))
            .Where(relative => !relative.Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .ToList();

        Assert.Contains(Path.Combine("src", "Fhi.Munin.Explorer", "Blazor", "VariableListFilters.razor"), scanned);
        Assert.DoesNotContain(scanned, relative => MarkupSinks(File.ReadAllText(Path.Combine(Repo.Root, relative))).Count > 0);
    }

    [Theory]
    [InlineData("@((MarkupString)$\"<h2>{T.Title}</h2>\")")]
    [InlineData("var m = (Microsoft.AspNetCore.Components.MarkupString)text;")]
    [InlineData("var m = new MarkupString (text);")]
    [InlineData("MarkupString m = new(text);")]
    [InlineData("builder.AddMarkupContent(3, text);")]
    [InlineData("// note\nbuilder.AddMarkupContent(1, t);")]
    [InlineData("/* c */ builder.AddMarkupContent(1, t);")]
    [InlineData("@* c *@ @((MarkupString)x)")]
    [InlineData("<a href=\"https://x\">@((MarkupString)y)</a>")]
    [InlineData("var u = \"https://x\"; var m = (MarkupString)y;")]
    [InlineData("var g = \"/*\";\nvar m = (MarkupString)y;\n/* x */")]
    [InlineData("var p = @\"C:\\dir\\\"; var m = (MarkupString)y;")]
    [InlineData("var q = '\"'; var m = (MarkupString)y;")]
    [InlineData("Action<int, string> add = builder.AddMarkupContent;")]
    public void MarkupSinks_WhenTheSourceWritesASink_ThenItIsFound(string source) =>
        Assert.NotEmpty(MarkupSinks(source));

    [Theory]
    [InlineData("/// and no <see cref=\"MarkupString\"/> exist anywhere in the path.")]
    [InlineData("// a MarkupString would parse this")]
    [InlineData("@* drawn without a MarkupString *@")]
    [InlineData("/* AddMarkupContent( is the sink */ builder.AddContent(1, text);")]
    [InlineData("builder.AddContent(1, text);")]
    [InlineData("var s = \"MarkupString\"; // AddMarkupContent(")]
    public void MarkupSinks_WhenNoCodeWritesASink_ThenNothingIsFound(string source) =>
        Assert.Empty(MarkupSinks(source));

    /// <summary>The sinks <paramref name="source"/> writes outside its comments and string literals.</summary>
    internal static IReadOnlyList<string> MarkupSinks(string source)
    {
        var code = CommentsAndLiterals().Replace(source, " ");
        return [.. Sinks().Matches(code).Select(match => match.Value)];
    }

    // One pass, so a comment opener inside a string, or a quote inside a comment, is read as what it is.
    [GeneratedRegex(@"\$?@\$?""(?:[^""]|"""")*""|""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])'|@\*.*?\*@|/\*.*?\*/|//[^\n]*", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommentsAndLiterals();

    [GeneratedRegex(@"\bMarkupString\b|\bAddMarkupContent\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sinks();
}
