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
    private bool _disposed;

    private string Status => _loading ? T.HierarchyLoading
        : _failed ? T.HierarchyError
        : _nodes.Count == 0 ? T.HierarchyEmpty : "";

    protected override Task OnParametersSetAsync()
    {
        if (_requestedId == KildeId)
        {
            return Task.CompletedTask;
        }

        _requestedId = KildeId;
        _failed = false;
        return LoadAsync();
    }

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
            }
        }
    }

    private RenderFragment Nodes(IReadOnlyList<KildeHierarchyNode> nodes) => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "munin-explorer-hierarchy__nodes");
        foreach (var node in nodes)
        {
            builder.OpenElement(2, "li");
            builder.SetKey(node.Key);
            if (node.Children.Count > 0)
            {
                // Native details owns expansion and focus locally, including on legacy Server hosts.
                builder.OpenElement(3, "details");
                builder.AddAttribute(4, "class", "munin-explorer-hierarchy__branch");
                builder.OpenElement(5, "summary");
                builder.AddContent(6, Label(node));
                builder.CloseElement();
                builder.AddContent(7, Nodes(node.Children));
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(8, "span");
                builder.AddAttribute(9, "class", "munin-explorer-hierarchy__leaf");
                builder.AddContent(10, Label(node));
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
