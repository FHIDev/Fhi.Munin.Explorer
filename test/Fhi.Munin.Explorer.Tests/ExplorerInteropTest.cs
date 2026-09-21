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
        // Every export's name is a literal in C# and a declaration in JS, with nothing else holding
        // the two spellings together. The theories below feed the matcher its own input, since this
        // walk can only ever read the forms the module happens to use.
        var module = File.ReadAllText(ModuleInSource);
        var wrapper = File.ReadAllText(
            Repo.In("src", "Fhi.Munin.Explorer", "Blazor", nameof(ExplorerInterop) + ".cs"));

        Assert.False(
            Opaque().IsMatch(Blanked(module)),
            $"{ExplorerInterop.ModuleFile} exports through `default` or `*`, neither of which names " +
            "anything this can read. Export by name, so the two spellings stay tied together.");

        foreach (var name in Exports(module))
        {
            Assert.True(
                wrapper.Contains($"\"{name}\"", StringComparison.Ordinal),
                $"{ExplorerInterop.ModuleFile} exports '{name}' and {nameof(ExplorerInterop)} names " +
                "no such identifier, so nothing ties the two spellings together.");
        }
    }

    [Theory]
    [InlineData("export function packageVersion() {}", "packageVersion")]
    [InlineData("export async function packageVersion() {}", "packageVersion")]
    [InlineData("export function* packageVersion() {}", "packageVersion")]
    [InlineData("export const packageVersion = () => {};", "packageVersion")]
    [InlineData("export let packageVersion = 1;", "packageVersion")]
    [InlineData("export var packageVersion = 1;", "packageVersion")]
    [InlineData("export class PackageVersion {}", "PackageVersion")]
    [InlineData("function packageVersion() {}\nexport { packageVersion };", "packageVersion")]
    [InlineData("export { internalName as packageVersion };", "packageVersion")]
    [InlineData("export { first, second };", "first second")]
    [InlineData("export { packageVersion } from './other.js';", "packageVersion")]
    [InlineData("function packageVersion() {}", "")]
    [InlineData("// export function packageVersion() {}", "")]
    public void Exports_WhenAModuleNamesThem_ThenEveryFormIsRead(string source, string names)
    {
        // The guard above reads a module with no exports in it, so the matcher is fed its input
        // here instead — every shape a first export could reasonably take, and the two near misses
        // that must read as nothing: a plain declaration, and the word inside a comment.
        Assert.Equal(names, string.Join(' ', Exports(source)));
        Assert.DoesNotMatch(Opaque(), Blanked(source));
    }

    [Theory]
    [InlineData("export default function packageVersion() {}")]
    [InlineData("export default packageVersion;")]
    [InlineData("export * from './other.js';")]
    [InlineData("export * as helpers from './other.js';")]
    public void Exports_WhenAFormHidesTheName_ThenTheModuleIsRefusedRatherThanReadAsEmpty(string source)
    {
        // These are the forms the matcher cannot see a name in. Silence over them is the failure
        // the guard exists to prevent, so the module is refused for using one rather than passing.
        Assert.Matches(Opaque(), Blanked(source));
        Assert.Empty(Exports(source));
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
    public void Import_WhenTheSourceIsRead_ThenSomethingLoadsTheModuleAndNothingStoresTheAnswer()
    {
        // Two halves of one claim. Nothing loading it would leave the theory above vacuous, and a
        // caller STORING the answer would have made the module load-bearing. Branching on it where
        // it stands is allowed for one use only — retrying a failed import on a later render.
        var callers = Callers().ToList();

        Assert.NotEmpty(callers);

        foreach (var (file, source) in callers)
        {
            foreach (var call in Calls(source))
            {
                var statement = Statement(source, call);

                Assert.True(
                    (statement.StartsWith("await ", StringComparison.Ordinal)
                     || statement.StartsWith("if (await ", StringComparison.Ordinal))
                    && !statement.Contains('=', StringComparison.Ordinal),
                    $"{file} keeps the answer to the import: '{statement}'. Nothing rendered may " +
                    "depend on the module, so the answer is dropped or branched on where it " +
                    "stands — never stored.");
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
    public async Task DisposeAsync_WhenTheReleaseItselfFaults_ThenItSwallowsTheFault()
    {
        // A release is a round trip on Blazor Server, so a browser that has already dropped the
        // reference — a reconnect, or a script on the page releasing it first — answers with a
        // JSException. Letting that travel on makes an ordinary unmount an unhandled renderer fault.
        var interop = new ExplorerInterop(
            new LendingJsRuntime(new RefusingModule(new JSException("no such reference"))));

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

    [Fact]
    public async Task TryLoadAsync_WhenTheHostServesNoModule_ThenItIsNotAskedForAgain()
    {
        // The caller retries a failed load on a later render, because an import a reconnecting
        // circuit could not carry is "not yet". A 404 is not: without this, a host serving no
        // module would pay one import round trip per render for the life of the circuit.
        var runtime = new RefusingJsRuntime(new JSException("404"));
        var interop = new ExplorerInterop(runtime);

        Assert.False(await interop.TryLoadAsync());
        Assert.False(await interop.TryLoadAsync());
        Assert.False(await interop.TryLoadAsync());

        Assert.Equal(1, runtime.Imports);
    }

    [Fact]
    public async Task TryLoadAsync_WhenASecondImportOverlapsTheFirst_ThenTheLaterOneIsReleased()
    {
        // The caller asks again while an import is in flight, because its own latch reopens when
        // the bar leaves the render tree. Keeping the arrival that lands second drops the first,
        // and nothing else holds it: one reference in the browser's table for the circuit's life.
        var first = new CountingModule();
        var second = new CountingModule();
        var runtime = new StagingJsRuntime();
        var interop = new ExplorerInterop(runtime);

        var one = interop.TryLoadAsync();
        var two = interop.TryLoadAsync();

        Assert.Equal(2, runtime.Imports);

        runtime.Answer(1, first);

        Assert.True(await one);

        runtime.Answer(2, second);

        Assert.True(await two);

        Assert.Equal(0, first.Disposals);
        Assert.Equal(1, second.Disposals);
    }

    [Fact]
    public async Task TryLoadAsync_WhenTheOverlappingImportFailsTolerably_ThenTheModuleKeptSurvivesIt()
    {
        // The same overlap, answered the other way round: the later import is the one a reconnect
        // swallows, so it arrives as null. Assigning that null would leave a live module the
        // interop believes it does not have — nothing releases it, and its observers stay on.
        var module = new RecordingModule();
        var runtime = new StagingJsRuntime();
        var interop = new ExplorerInterop(runtime);

        var one = interop.TryLoadAsync();
        var two = interop.TryLoadAsync();

        runtime.Answer(1, module);

        Assert.True(await one);

        runtime.Answer(2, null);

        Assert.True(await two);
        Assert.True(interop.IsLoaded);

        await interop.DisconnectHeroFactsAsync("bar-a1b2c3d4");

        Assert.Equal(["bar-a1b2c3d4"], module.ArgumentsOf("disconnectHeroFacts"));
    }

    [Fact]
    public async Task TryLoadAsync_WhenTheCircuitWasReconnecting_ThenAskingAgainStillGetsTheModule()
    {
        // The half the memory above must not swallow. A JSDisconnectedException is answered as null
        // rather than as a refusal, so the next render's ask is the one that lands.
        var runtime = new FlakyJsRuntime(new RecordingModule());
        var interop = new ExplorerInterop(runtime);

        Assert.False(await interop.TryLoadAsync());
        Assert.True(await interop.TryLoadAsync());

        Assert.Equal(2, runtime.Imports);
    }

    // -----------------------------------------------------------------------
    // The exports, and which faults each of them may swallow

    [Fact]
    public async Task ObserveHeroFactsAsync_WhenTheModuleIsThere_ThenBothIdsReachTheExport()
    {
        // The ids are what scope one page's bar to one page's hero row, so a call that carried the
        // wrong one would drive somebody else's bar and every count of the calls would still pass.
        var module = new RecordingModule();
        var interop = new ExplorerInterop(new LendingJsRuntime(module));

        Assert.True(await interop.TryLoadAsync());

        await interop.ObserveHeroFactsAsync("bar-a1b2c3d4", "facts-a1b2c3d4");

        var call = Assert.Single(module.Calls);

        Assert.Equal("observeHeroFacts", call.Identifier);
        Assert.Equal(["bar-a1b2c3d4", "facts-a1b2c3d4"], call.Arguments.Select(argument => argument as string));
    }

    [Fact]
    public async Task ObserveHeroFactsAsync_WhenTheHostServesNoModule_ThenItDoesNothingAtAll()
    {
        // The designer's own note: uten JS dukker den bare aldri opp - ingenting går tapt. This is
        // that sentence as a test, and the reason the bar is rendered hidden rather than shown.
        var interop = new ExplorerInterop(new RefusingJsRuntime(new JSException("404")));

        Assert.False(await interop.TryLoadAsync());

        await interop.ObserveHeroFactsAsync("bar", "facts");
    }

    [Fact]
    public async Task ObserveHeroFactsAsync_WhenTheExportItselfFaults_ThenItTravelsOn()
    {
        // The asymmetry with the disconnect below, and it is deliberate: a fault inside an export
        // is a defect in the module, and one nothing rethrows is a bar that never works with
        // nothing in any host's log to say so.
        var interop = new ExplorerInterop(
            new LendingJsRuntime(new RefusingModule(new JSException("observeHeroFacts is not a function"))));

        Assert.True(await interop.TryLoadAsync());

        await Assert.ThrowsAsync<JSException>(() => interop.ObserveHeroFactsAsync("bar", "facts"));
    }

    [Fact]
    public async Task DisconnectHeroFactsAsync_WhenTheModuleIsThere_ThenTheBarsOwnIdReachesTheExport()
    {
        var module = new RecordingModule();
        var interop = new ExplorerInterop(new LendingJsRuntime(module));

        Assert.True(await interop.TryLoadAsync());

        await interop.DisconnectHeroFactsAsync("bar-a1b2c3d4");

        var call = Assert.Single(module.Calls);

        Assert.Equal("disconnectHeroFacts", call.Identifier);
        Assert.Equal(["bar-a1b2c3d4"], call.Arguments.Select(argument => argument as string));
    }

    [Theory]
    [InlineData(typeof(JSDisconnectedException))]
    [InlineData(typeof(JSException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ObjectDisposedException))]
    [InlineData(typeof(OperationCanceledException))]
    public async Task DisconnectHeroFactsAsync_WhenTheBrowserIsOutOfReach_ThenItSwallowsTheFault(Type thrown)
    {
        // This one runs from disposal, and a reader closing the tab mid-scroll is the normal way it
        // is reached — so a throw here is an unhandled renderer fault in the host's log on every
        // such close. JSException is in the list for that reason and is not in the observe's.
        var interop = new ExplorerInterop(new LendingJsRuntime(new RefusingModule(Raise(thrown))));

        Assert.True(await interop.TryLoadAsync());

        await interop.DisconnectHeroFactsAsync("bar");
    }

    [Fact]
    public async Task DisconnectHeroFactsAsync_WhenTheModuleNeverLoaded_ThenThereIsNothingToDisconnect()
    {
        var interop = new ExplorerInterop(new RefusingJsRuntime(new JSException("404")));

        Assert.False(await interop.TryLoadAsync());

        await interop.DisconnectHeroFactsAsync("bar");
    }

    [Fact]
    public async Task ObserveContentsAsync_WhenTheModuleIsThere_ThenTheColumnsIdReachesTheExport()
    {
        var module = new RecordingModule();
        var interop = new ExplorerInterop(new LendingJsRuntime(module));

        Assert.True(await interop.TryLoadAsync());

        await interop.ObserveContentsAsync("contents-a1b2c3d4");

        var call = Assert.Single(module.Calls);

        Assert.Equal("observeContents", call.Identifier);
        Assert.Equal(["contents-a1b2c3d4"], call.Arguments.Select(argument => argument as string));
    }

    [Fact]
    public async Task ObserveContentsAsync_WhenTheHostServesNoModule_ThenItDoesNothingAtAll()
    {
        var interop = new ExplorerInterop(new RefusingJsRuntime(new JSException("404")));

        Assert.False(await interop.TryLoadAsync());

        await interop.ObserveContentsAsync("contents");
    }

    [Fact]
    public async Task ObserveContentsAsync_WhenTheExportItselfFaults_ThenItTravelsOn()
    {
        // As for the hero facts: a fault inside the export is a defect in the module, and the
        // caller logs it rather than this swallowing it.
        var interop = new ExplorerInterop(
            new LendingJsRuntime(new RefusingModule(new JSException("observeContents is not a function"))));

        Assert.True(await interop.TryLoadAsync());

        await Assert.ThrowsAsync<JSException>(() => interop.ObserveContentsAsync("contents"));
    }

    [Fact]
    public async Task DisconnectContentsAsync_WhenTheModuleIsThere_ThenTheColumnsIdReachesTheExport()
    {
        var module = new RecordingModule();
        var interop = new ExplorerInterop(new LendingJsRuntime(module));

        Assert.True(await interop.TryLoadAsync());

        await interop.DisconnectContentsAsync("contents-a1b2c3d4");

        var call = Assert.Single(module.Calls);

        Assert.Equal("disconnectContents", call.Identifier);
        Assert.Equal(["contents-a1b2c3d4"], call.Arguments.Select(argument => argument as string));
    }

    [Theory]
    [InlineData(typeof(JSDisconnectedException))]
    [InlineData(typeof(JSException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ObjectDisposedException))]
    [InlineData(typeof(OperationCanceledException))]
    public async Task DisconnectContentsAsync_WhenTheBrowserIsOutOfReach_ThenItSwallowsTheFault(Type thrown)
    {
        // Called from disposal, where a throw is an unhandled renderer fault.
        var interop = new ExplorerInterop(new LendingJsRuntime(new RefusingModule(Raise(thrown))));

        Assert.True(await interop.TryLoadAsync());

        await interop.DisconnectContentsAsync("contents");
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

    /// <summary>The statement or branch condition holding the import at <paramref name="call"/>.</summary>
    /// <remarks>
    /// Ends at the first <c>;</c> or <c>{</c>, so an import read as an <c>if</c> condition is that
    /// condition alone rather than the whole branch body dragged along behind it.
    /// </remarks>
    private static string Statement(string source, int call)
    {
        var opens = source.LastIndexOfAny([';', '{', '}', '\n'], call) + 1;
        var closes = source.IndexOfAny([';', '{'], call);

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

    /// <summary>Every name <paramref name="source"/> exports, in the forms that state one.</summary>
    /// <remarks>
    /// Comments go first, through the same blanking the import walk uses — JS spells them the way
    /// C# does, and the module's own header has the word <c>export</c> in its prose.
    /// </remarks>
    private static IEnumerable<string> Exports(string source)
    {
        var read = Blanked(source);

        return Declared()
            .Matches(read)
            .Select(declared => declared.Groups["name"].Value)
            .Concat(Listed()
                .Matches(read)
                .SelectMany(list => list.Groups["names"].Value.Split(','))
                .Select(Bound)
                .Where(name => name.Length > 0));
    }

    /// <summary>The name one entry of an <c>export { … }</c> list binds, after any <c>as</c>.</summary>
    private static string Bound(string entry)
    {
        var words = entry.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);

        return words.Length == 0 ? string.Empty : words[^1];
    }

    [GeneratedRegex(
        @"\bexport\s+(?:async\s+)?(?:function\b\s*\*?|const|let|var|class)\s+(?<name>[A-Za-z_$][\w$]*)")]
    private static partial Regex Declared();

    [GeneratedRegex(@"\bexport\s*\{(?<names>[^}]*)\}")]
    private static partial Regex Listed();

    /// <summary>The export forms that state no name a caller could invoke by.</summary>
    [GeneratedRegex(@"\bexport\s+(?:default\b|\*)")]
    private static partial Regex Opaque();

    [GeneratedRegex(@"\bOn(?:Initialized|ParametersSet|AfterRender)(?:Async)?\b")]
    private static partial Regex Lifecycle();

    private static Exception Raise(Type thrown) =>
        thrown == typeof(OperationCanceledException)
            ? new OperationCanceledException()
            : (Exception)Activator.CreateInstance(thrown, "out of reach")!;
}
