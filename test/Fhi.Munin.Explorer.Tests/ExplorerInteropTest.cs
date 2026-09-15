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
        Assert.Null(await interop.TryReadPageVersionAsync());
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
    /// The wrapper itself is skipped: it declares the method every caller below calls, and a guard
    /// that read its declaration as a call would report the one file that cannot be wrong.
    /// </remarks>
    private static IEnumerable<(string File, string Source)> Callers() =>
        Directory
            .EnumerateFiles(Repo.In("src", "Fhi.Munin.Explorer"), "*.cs", SearchOption.AllDirectories)
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
    /// </remarks>
    private static string Blanked(string source) =>
        Comment().Replace(source, found => new string(' ', found.Length));

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"\bOn(?:Initialized|ParametersSet|AfterRender)(?:Async)?\b")]
    private static partial Regex Lifecycle();

    private static Exception Raise(Type thrown) =>
        thrown == typeof(OperationCanceledException)
            ? new OperationCanceledException()
            : (Exception)Activator.CreateInstance(thrown, "out of reach")!;

    // -----------------------------------------------------------------------
    // Stand-ins for the browser

    /// <summary>A runtime that refuses every call, the way a host without the module does.</summary>
    private sealed class RefusingJsRuntime(Exception thrown) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw thrown;

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) => throw thrown;
    }

    /// <summary>A runtime that hands out one module, counting how often it was asked for it.</summary>
    private sealed class LendingJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        internal int Imports { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Imports++;

            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    /// <summary>A module whose disposal throws, as one on a dropped circuit does.</summary>
    private sealed class RefusingModule(Exception thrown) : IJSObjectReference
    {
        public ValueTask DisposeAsync() => throw thrown;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw thrown;

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) => throw thrown;
    }

    /// <summary>A module that counts its own disposals.</summary>
    private sealed class CountingModule : IJSObjectReference
    {
        internal int Disposals { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposals++;

            return ValueTask.CompletedTask;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);
    }
}
