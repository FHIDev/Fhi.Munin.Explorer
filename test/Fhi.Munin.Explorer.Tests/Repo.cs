namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Where the checkout is, for the handful of checks that read files rather than rendered output —
/// the sample stylesheets, the captured host class names, the component sources.
/// </summary>
/// <remarks>
/// One copy, deliberately. This walk existed twice for a while, once in <see cref="HostClassNames"/>
/// and once beside the guards that read a component's source, and two copies means the next change
/// to how the checkout is found — a renamed solution file, a different sentinel — fixes one of them
/// and leaves the other walking silently to the filesystem root.
/// </remarks>
internal static class Repo
{
    private static readonly Lazy<string> Located = new(() =>
    {
        // Walk up from the test binary rather than trusting the working directory, which differs
        // between `dotnet test`, the IDE runner and CI.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Fhi.Munin.Explorer.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException(
                $"No Fhi.Munin.Explorer.slnx above '{AppContext.BaseDirectory}', so the files these " +
                "checks read cannot be found. Running the tests from outside the checkout?");
    });

    /// <summary>The checkout root: the directory holding <c>Fhi.Munin.Explorer.slnx</c>.</summary>
    internal static string Root => Located.Value;

    /// <summary>A path inside the checkout, built from its segments.</summary>
    internal static string In(params string[] segments) => Path.Combine([Root, .. segments]);
}
