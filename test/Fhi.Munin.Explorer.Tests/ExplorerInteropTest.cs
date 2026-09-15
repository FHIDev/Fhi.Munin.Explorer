using System.Text.RegularExpressions;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The package's one JavaScript module: that the path it is fetched from is the one it is published
/// at, that it is imported only where a browser exists, and that it is never load-bearing.
/// </summary>
/// <remarks>
/// Every failure this guards fails at runtime and only in a host. A wrong <c>_content</c> segment
/// is a 404 the import rejects, and an unhandled rejection on a legacy Blazor Server circuit takes
/// the circuit down — so the reader loses the page, not the enhancement (Fhi.Metadata-35w0p.14).
/// </remarks>
public partial class ExplorerInteropTest
{
    /// <summary>The module as it sits in the RCL's <c>wwwroot</c>.</summary>
    private static string ModuleInSource =>
        Repo.In("src", "Fhi.Munin.Explorer", "wwwroot", ExplorerInterop.ModuleFile);

    [Fact]
    public void ModulePath_WhenItIsRead_ThenItNamesTheAssemblyTheSdkPublishesUnder()
    {
        // The segment is the assembly's name because that is what the Razor SDK serves the asset
        // under. Pinned to the literal as well, since a rename moving both together would still
        // break every host whose own markup or content-security-policy names the old path.
        var assembly = typeof(ExplorerInterop).Assembly.GetName().Name;

        Assert.Equal("Fhi.Munin.Explorer", assembly);
        Assert.Equal($"./_content/{assembly}/{ExplorerInterop.ModuleFile}", ExplorerInterop.ModulePath);
    }

    [Fact]
    public void Module_WhenThePathIsResolved_ThenAFileIsReallyThereToServe() =>
        Assert.True(
            File.Exists(ModuleInSource),
            $"{ExplorerInterop.ModulePath} has no file behind it at {ModuleInSource} — every host " +
            "importing it would get a 404.");

    [Fact]
    public void PackagingGuard_WhenItsExpectedEntriesAreRead_ThenTheyNameThisModule()
    {
        // One decision written down twice: assert-package-contents.sh compares the packed entries
        // for exact equality, so renaming the module without editing the script fails CI at pack
        // time with no hint of which of the two moved.
        var guard = File.ReadAllText(Repo.In("scripts", "assert-package-contents.sh"));

        Assert.Contains($"\"staticwebassets/{ExplorerInterop.ModuleFile}\"", guard, StringComparison.Ordinal);
    }

    [Fact]
    public void Wwwroot_WhenItIsRead_ThenItHoldsNoStylesheet()
    {
        // This directory existing does not loosen the packaging rule: the package ships a module
        // and no CSS, and a stylesheet here would compete with the host's own.
        var stylesheets = Directory
            .EnumerateFiles(Path.GetDirectoryName(ModuleInSource)!, "*.css", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal);

        Assert.Equal([], stylesheets);
    }

    [Fact]
    public void ModuleExports_WhenTheSourceIsRead_ThenTheWrapperNamesEveryOne()
    {
        // Nothing to find today: the module is a seam and exports nothing. This is here for the
        // first export, whose name is a literal in C# and a declaration in JS with nothing else
        // holding the two together — and Tolerated answers a renamed one as an ordinary null.
        var wrapper = File.ReadAllText(
            Repo.In("src", "Fhi.Munin.Explorer", "Blazor", nameof(ExplorerInterop) + ".cs"));

        foreach (Match export in Export().Matches(File.ReadAllText(ModuleInSource)))
        {
            var name = export.Groups["name"].Value;

            Assert.True(
                wrapper.Contains($"\"{name}\"", StringComparison.Ordinal),
                $"{ExplorerInterop.ModuleFile} exports '{name}' and {nameof(ExplorerInterop)} names " +
                "no such identifier, so nothing ties the two spellings together.");
        }
    }

    // -----------------------------------------------------------------------
    // Where the import may happen, read off the source

    [Fact]
    public void Import_WhenTheSourceIsRead_ThenItIsReachedOnlyFromOnAfterRenderAsync()
    {
        // There is no DOM and no JS runtime during prerender, so an import from OnInitialized or
        // OnParametersSet fails for a reason that has nothing to do with the host. The wrapper says
        // so in prose; this is what makes the next caller obey it.
        foreach (var (file, source) in Callers())
        {
            foreach (var call in Calls(source))
            {
                var lifecycle = Lifecycle().Matches(source[..call]);

                Assert.True(
                    lifecycle.Count > 0 && lifecycle[^1].Value == "OnAfterRenderAsync",
                    $"{file} imports the module from " +
                    $"{(lifecycle.Count == 0 ? "no lifecycle method" : lifecycle[^1].Value)} — only " +
                    "OnAfterRenderAsync has a browser to import with.");

                Assert.Contains("firstRender", source[lifecycle[^1].Index..call], StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Import_WhenTheSourceIsRead_ThenSomethingLoadsTheModuleAndNothingWaitsOnTheAnswer()
    {
        // Two halves of one claim. Nothing loading it would leave the theory above vacuous, and a
        // caller reading the answer would have made the module load-bearing — which is the one
        // thing it may not be until a later bead argues for it.
        var callers = Callers().ToList();

        Assert.NotEmpty(callers);

        foreach (var (file, source) in callers)
        {
            foreach (var call in Calls(source))
            {
                var statement = Statement(source, call);

                Assert.True(
                    statement.StartsWith("await ", StringComparison.Ordinal)
                    && !statement.Contains('=', StringComparison.Ordinal),
                    $"{file} keeps the answer to the import: '{statement}'. Nothing may depend on " +
                    "the module, so the call is awaited and its result dropped.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // The fail-soft contract, driven head-on

    [Fact]
    public async Task TryLoadAsync_WhenTheHostServesNoModule_ThenItAnswersFalseRatherThanThrowing()
    {
        // The whole contract in one call: a host serving a cached bundle, or none at all, answers
        // the import with a 404 the browser reports as a JSException, and the page stays up.
        var interop = new ExplorerInterop(new RefusingJsRuntime(new JSException("404")));

        Assert.False(await interop.TryLoadAsync());
        Assert.False(interop.IsLoaded);
    }

    [Theory]
    [InlineData(typeof(JSDisconnectedException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ObjectDisposedException))]
    [InlineData(typeof(OperationCanceledException))]
    public async Task TryLoadAsync_WhenTheBrowserIsOutOfReach_ThenItAnswersFalseRatherThanThrowing(Type thrown)
    {
        // The ways that are not the host's doing: a dropped circuit, a prerender with no JS runtime
        // — InvalidOperationException, which reads like a bug and is the ordinary case here — and a
        // disposed runtime or a cancelled call as the reader navigates away.
        var interop = new ExplorerInterop(new RefusingJsRuntime(Raise(thrown)));

        Assert.False(await interop.TryLoadAsync());
    }

    [Fact]
    public async Task TryLoadAsync_WhenTheImportFailsForAnyOtherReason_ThenItTravelsOn()
    {
        // The other half of Tolerated's contract, and the half a widened clause would take away
        // silently: everything outside the three is a defect, and a defect nothing rethrows is a
        // permanent null with nothing in any host's log.
        var interop = new ExplorerInterop(new RefusingJsRuntime(new ArgumentException("boom")));

        await Assert.ThrowsAsync<ArgumentException>(() => interop.TryLoadAsync());
    }

    [Fact]
    public async Task DisposeAsync_WhenTheImportIsStillInFlight_ThenTheModuleIsReleasedOnArrival()
    {
        // The component can go while its import is unanswered — a host swapping the explorer out
        // of the render tree on a circuit that lives on. Nothing disposes what arrives after that
        // but this, and the reference would sit in the runtime's table for the circuit's life.
        var module = new CountingModule();
        var runtime = new PendingJsRuntime(module);
        var interop = new ExplorerInterop(runtime);

        var loading = interop.TryLoadAsync();

        await interop.DisposeAsync();

        runtime.Answer();

        Assert.False(await loading);
        Assert.False(interop.IsLoaded);
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task DisposeAsync_WhenTheCircuitIsAlreadyGone_ThenItSwallowsTheDisconnection()
    {
        // A reader closing the tab is how a module is normally released, so a throw here is an
        // unhandled exception in the host's log on every such close rather than an incident.
        var interop = new ExplorerInterop(
            new LendingJsRuntime(new RefusingModule(new JSDisconnectedException("circuit gone"))));

        Assert.True(await interop.TryLoadAsync());

        await interop.DisposeAsync();

        Assert.False(interop.IsLoaded);
    }

    [Fact]
    public async Task DisposeAsync_WhenItIsCalledTwice_ThenNothingIsReleasedASecondTime()
    {
        var module = new CountingModule();
        var interop = new ExplorerInterop(new LendingJsRuntime(module));

        Assert.True(await interop.TryLoadAsync());

        await interop.DisposeAsync();
        await interop.DisposeAsync();

        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task TryLoadAsync_WhenItIsCalledTwice_ThenTheModuleIsImportedOnce()
    {
        var runtime = new LendingJsRuntime(new CountingModule());
        var interop = new ExplorerInterop(runtime);

        Assert.True(await interop.TryLoadAsync());
        Assert.True(await interop.TryLoadAsync());

        Assert.Equal(1, runtime.Imports);
    }

    // -----------------------------------------------------------------------
    // Reading the source

    private const string Load = "TryLoadAsync(";

    /// <summary>The files under <c>src/</c> that import the module, with their source.</summary>
    /// <remarks>
    /// Razor as well as C#: an <c>@code</c> block is a lifecycle method like any other, and a walk
    /// that read only <c>.cs</c> would leave both theories green while the rule went unenforced.
    /// The wrapper itself is skipped — it declares the method, and that is not a call.
    /// </remarks>
    private static IEnumerable<(string File, string Source)> Callers() =>
        Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), "*.*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".razor")
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .Any(segment => segment is "bin" or "obj"))
            .Where(path => Path.GetFileName(path) != nameof(ExplorerInterop) + ".cs")
            .Select(path => (File: Path.GetFileName(path), Source: Blanked(System.IO.File.ReadAllText(path))))
            .Where(read => read.Source.Contains(Load, StringComparison.Ordinal))
            .OrderBy(read => read.File, StringComparer.Ordinal);

    /// <summary>Where each import sits in <paramref name="source"/>.</summary>
    private static IEnumerable<int> Calls(string source)
    {
        for (var at = source.IndexOf(Load, StringComparison.Ordinal); at >= 0;
             at = source.IndexOf(Load, at + Load.Length, StringComparison.Ordinal))
        {
            yield return at;
        }
    }

    /// <summary>The statement holding the import at <paramref name="call"/>, trimmed.</summary>
    private static string Statement(string source, int call)
    {
        var opens = source.LastIndexOfAny([';', '{', '}', '\n'], call) + 1;
        var closes = source.IndexOf(';', call);

        return source[opens..(closes < 0 ? source.Length : closes + 1)].Trim();
    }

    /// <summary>
    /// <paramref name="source"/> with every comment replaced by blanks of the same length.
    /// </summary>
    /// <remarks>
    /// Blanks rather than removal so the offsets stay true. Without this the guard reads the prose
    /// beside a call — the wrapper's own "not from OnInitialized" — as the method holding it.
    /// Razor's own comments go first, through the one copy of that stripping the suite has.
    /// </remarks>
    private static string Blanked(string source) =>
        Comment().Replace(RazorSource.WithoutComments(source), found => new string(' ', found.Length));

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"\bexport\s+(?:async\s+)?(?:function\*?|const|let|var|class)\s+(?<name>[A-Za-z_$][\w$]*)")]
    private static partial Regex Export();

    [GeneratedRegex(@"\bOn(?:Initialized|ParametersSet|AfterRender)(?:Async)?\b")]
    private static partial Regex Lifecycle();

    private static Exception Raise(Type thrown) =>
        thrown == typeof(OperationCanceledException)
            ? new OperationCanceledException()
            : (Exception)Activator.CreateInstance(thrown, "out of reach")!;
}
