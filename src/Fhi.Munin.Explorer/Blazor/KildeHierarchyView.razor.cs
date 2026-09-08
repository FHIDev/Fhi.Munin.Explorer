using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

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

    [Parameter, EditorRequired] public Guid KildeId { get; set; }

    /// <inheritdoc cref="VariableSearch.Language"/>
    [Parameter] public string? Language { get; set; }

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
        catch (MuninExplorerRateLimitedException)
        {
            if (!_disposed && !request.IsCancellationRequested)
            {
                _rateLimited = true;
                _failed = false;
            }
        }
        catch (Exception)
        {
            if (!_disposed && !request.IsCancellationRequested)
            {
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
        var named = T.Named(node.Name, null);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "lang", CatalogueProperties.Foreign(named.Norwegian, ReaderLanguage.Of(Language)));
        builder.AddContent(2, named.Text);
        builder.CloseElement();
        if (node.Count > 0)
        {
            builder.AddContent(3, " ");
            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "class", "munin-explorer-hierarchy__count");
            builder.AddContent(6, node.Count);
            builder.OpenElement(7, "span");
            builder.AddAttribute(8, "class", "screenreader-only");
            builder.AddContent(9, $" {T.VariableCountSuffix}");
            builder.CloseElement();
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
