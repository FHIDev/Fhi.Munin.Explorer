using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <remarks>
/// Loads the hierarchy separately from source metadata. Register IMuninExplorerClient through
/// AddMuninExplorer before mounting. Branches use native disclosures: Tab visits summaries,
/// Enter or Space opens them, and nested lists convey ancestry without ARIA tree keyboard rules.
/// Changing KildeId cancels the previous request and resets all disclosures.
/// </remarks>
public sealed partial class KildeHierarchyView : ComponentBase, IDisposable
{
    [Inject] private IMuninExplorerClient Client { get; set; } = default!;

    [Inject] private IServiceProvider Services { get; set; } = default!;

    private ILogger? _log;

    /// <summary>The host's logger, or none — see <see cref="ExplorerLog"/>.</summary>
    private ILogger? Log => _log ??= ExplorerLog.For<KildeHierarchyView>(Services);

    [Parameter, EditorRequired] public Guid KildeId { get; set; }

    /// <inheritdoc cref="VariableSearch.Language"/>
    [Parameter] public string? Language { get; set; }

    /// <summary>
    /// Whether the tree draws a node icon in front of each name — a folder on a delkilde and one
    /// glyph per datakategori on a datasamling. On by default; a variabelgruppe has no icon either
    /// way. <b>The variable counts are not affected</b>: turning this off removes the glyphs and
    /// the words that stand in for them, and nothing else.
    /// </summary>
    /// <remarks>
    /// The package draws the shapes but decides nothing about their size or colour. Each glyph is
    /// an inline <c>&lt;svg&gt;</c> at <c>1em</c> in <c>currentColor</c>, wearing
    /// <c>munin-explorer-hierarchy__icon</c> and a <c>data-node-icon</c> naming its datakategori,
    /// so a host stylesheet is what makes a category recognisable at a glance rather than only
    /// distinguishable by shape.
    /// <para>
    /// A way of drawing the tree rather than something the reader is looking at, so like
    /// <see cref="VariableSearch.LevelLines"/> it is read once at mount and the package remembers
    /// no choice of its own: reaching <c>localStorage</c> from a circuit is a JS interop call this
    /// package never makes, and what is remembered about a reader is the host's policy to set.
    /// </para>
    /// </remarks>
    [Parameter] public bool ShowNodeIcons { get; set; } = true;

    /// <summary>
    /// Where a datasamling's own page lives, given its id — or null, which is the default and draws
    /// no link at all. The link is a plain <c>&lt;a href&gt;</c>, so whatever this returns has to be
    /// an address the browser can open on its own: middle-click and Ctrl+click are part of what a
    /// link is, and nothing here intercepts the press.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A delegate, so it can only be set by a parent component: a host mounting this type as an
    /// interactive root passes parameters as JSON and cannot serialise one. <see cref="KildeSearch"/>
    /// is what sets it, from the address <see cref="KildeExplorer"/> owns, for the same reason
    /// <c>VariableExplorerPath</c> is a path rather than a callback — only the host knows where its
    /// explorer is mounted.
    /// </para>
    /// <para>
    /// <b>It does not touch the disclosure.</b> The link is its own element beside the name, so the
    /// <c>&lt;summary&gt;</c> keeps its one job: Enter and Space on a branch still expand and
    /// collapse it, and the link is a separate tab stop that navigates. A summary that did both
    /// would leave the tree unopenable from the keyboard.
    /// </para>
    /// </remarks>
    [Parameter] public Func<Guid, string>? DatasamlingHref { get; set; }

    private Texts T => Texts.For(Language);
    private Guid? _requestedId;
    private CancellationTokenSource? _request;
    private IReadOnlyList<KildeHierarchyNode> _nodes = [];
    private bool _loading;
    private bool _failed;
    private bool _rateLimited;
    private bool _retryShown;
    private bool CanRetry => _failed && !_loading && !_rateLimited;
    private bool _disposed;

    private string Status => _loading ? T.HierarchyLoading
        : _rateLimited ? T.RateLimitError
        : _failed ? T.HierarchyError
        : _nodes.Count == 0 ? T.HierarchyEmpty : _retryShown ? T.HierarchyLoaded : "";

    protected override Task OnParametersSetAsync()
    {
        if (_requestedId == KildeId)
        {
            return Task.CompletedTask;
        }

        _requestedId = KildeId;
        _failed = false;
        _rateLimited = false;
        _retryShown = false;
        return LoadAsync();
    }

    private Task RetryAsync() => CanRetry ? LoadAsync() : Task.CompletedTask;

    private async Task LoadAsync()
    {
        _request?.Cancel();
        using var request = new CancellationTokenSource();
        _request = request;
        _nodes = [];
        _loading = true;

        try
        {
            var hierarchy = await Client.GetKildeHierarchyAsync(KildeId, request.Token);
            if (_disposed || request.IsCancellationRequested)
            {
                return;
            }

            _failed = hierarchy is null || hierarchy.KildeId != KildeId;
            _nodes = _failed ? [] : KildeHierarchyNode.From(hierarchy!);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // Inside the guard, not above it. This is the one place in the package that cancels its
            // own calls — on every new KildeId and again on dispose — and a superseded call comes
            // back here as a TaskCanceledException that nothing failed and nobody should read about.
            if (!_disposed && !request.IsCancellationRequested)
            {
                Log?.LogWarning(
                    ex, "the rate limiter refused the hierarchy of kilde {KildeId}", KildeId);

                _rateLimited = true;
                _failed = false;
            }
        }
        catch (Exception ex)
        {
            // The same guard, and for the same reason: HttpClient's own 30-second timeout is a
            // TaskCanceledException worth logging, and this component's own Cancel is not, so the
            // question asked is who cancelled rather than which type arrived.
            if (!_disposed && !request.IsCancellationRequested)
            {
                Log?.LogError(ex, "could not load the hierarchy of kilde {KildeId}", KildeId);

                _failed = true;
            }
        }
        finally
        {
            if (ReferenceEquals(_request, request))
            {
                _request = null;
                _loading = false;
                _retryShown |= _failed;
            }
        }
    }

    private RenderFragment Nodes(IReadOnlyList<KildeHierarchyNode> nodes) => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "munin-explorer-hierarchy__nodes");
        // Explicit semantics survive hosts removing list markers in Safari/VoiceOver.
        builder.AddAttribute(2, "role", "list");
        foreach (var node in nodes)
        {
            builder.OpenElement(3, "li");
            builder.SetKey(node.Key);
            if (node.Children.Count > 0)
            {
                // Native details owns expansion and focus locally, including on legacy Server hosts.
                builder.OpenElement(4, "details");
                builder.AddAttribute(5, "class", "munin-explorer-hierarchy__branch");
                builder.OpenElement(6, "summary");
                builder.AddContent(7, Label(node));
                builder.CloseElement();
                builder.AddContent(8, Nodes(node.Children));
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(9, "span");
                builder.AddAttribute(10, "class", "munin-explorer-hierarchy__leaf");
                builder.AddContent(11, Label(node));
                builder.CloseElement();
            }
            builder.CloseElement();
        }
        builder.CloseElement();
    };

    private RenderFragment Label(KildeHierarchyNode node) => builder =>
    {
        // NodeIcons.Write opens this fragment with sequence numbers of its own, so everything below
        // starts past them: the renderer diffs a fragment against one increasing sequence.
        var icons = ShowNodeIcons ? NodeIcons.For(node) : NodeIcons.None;
        NodeIcons.Write(builder, icons);

        var named = T.Named(node.Name, null);
        builder.OpenElement(20, "span");
        builder.AddAttribute(21, "lang", CatalogueProperties.Foreign(named.Norwegian, ReaderLanguage.Of(Language)));
        builder.AddContent(22, named.Text);
        builder.CloseElement();

        // The glyphs are aria-hidden, so these words are the only place the tree says which
        // datakategori a datasamling carries. After the name rather than before it: a row is found
        // by its name, and a category read first delays the word the reader is listening for.
        if (NodeIcons.SpokenCategories(node.Kind, icons, T) is { } categories)
        {
            builder.OpenElement(23, "span");
            builder.AddAttribute(24, "class", "screenreader-only");
            builder.AddContent(25, $" {categories}");
            builder.CloseElement();
        }

        if (node.Count > 0)
        {
            builder.AddContent(26, " ");
            builder.OpenElement(27, "span");
            builder.AddAttribute(28, "class", "munin-explorer-hierarchy__count");
            builder.AddContent(29, node.Count);
            builder.OpenElement(30, "span");
            builder.AddAttribute(31, "class", "screenreader-only");
            builder.AddContent(32, $" {T.VariableCountSuffix}");
            builder.CloseElement();
            builder.CloseElement();
        }

        // An element of its own rather than a second job for the summary above it, so the
        // disclosure keeps working. aria-label names the datasamling, because a link list full of
        // "Åpne" names nothing; it opens with the visible word, which is what WCAG 2.5.3 asks.
        if (node.DatasamlingId is { } datasamling && DatasamlingHref?.Invoke(datasamling) is { } href)
        {
            builder.AddContent(40, " ");
            builder.OpenElement(41, "a");
            builder.AddAttribute(42, "class", "munin-explorer-hierarchy__open");
            builder.AddAttribute(43, "href", href);
            builder.AddAttribute(44, "aria-label", T.OpenDatasamlingNamed(node.Name));
            builder.AddContent(45, T.OpenDatasamling);
            builder.CloseElement();
        }
    };

    public void Dispose()
    {
        _disposed = true;
        _request?.Cancel();
        _request?.Dispose();
        _request = null;
    }
}
