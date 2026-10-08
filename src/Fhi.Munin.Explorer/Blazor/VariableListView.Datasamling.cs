using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Items keyed by (variable, datasamling): writing a list's items into another, and resolving an item
/// saved before lists named a datasamling ("datasamling ikke valgt") into the datasamlinger the reader picks.
/// </summary>
public sealed partial class VariableListView
{
    // The datasamlinger ticked in the open row's picker; emptied whenever another row opens.
    private readonly HashSet<Guid> _picked = [];
    private bool _resolving;
    private string? _pickFailure;

    /// <summary>An item whose variable is in several datasamlinger and that names none of them.</summary>
    private static bool IsUnresolved(VariableListItem item) =>
        item.DatasamlingId is null && item.CandidateDatasamlinger.Count > 0;

    private string PickerLegendId(VariableListItem item) => $"munin-explorer-list-pick-{_instance}-{ItemSuffix(item)}";

    private string PickerHintId(VariableListItem item) => $"{PickerLegendId(item)}-hint";

    private void ForgetPicker()
    {
        _picked.Clear();
        _pickFailure = null;
    }

    /// <summary>
    /// Writes <paramref name="items"/> into <paramref name="list"/>, a datasamling chosen staying chosen.
    /// </summary>
    /// <remarks>
    /// An item with no datasamling goes by variable: the API then saves the variable's one open
    /// datasamling, or none, and refuses one in several, which it also refuses as an item naming none.
    /// </remarks>
    private async Task<bool> AddEveryItemAsync(Guid list, IEnumerable<VariableListItem> items)
    {
        var all = items.ToList();
        List<VariableDatasamlingKey> chosen = [.. all.Where(i => i.DatasamlingId is not null).Select(VariableDatasamlingKey.Of).Distinct()];
        List<Guid> unchosen = [.. all.Where(i => i.DatasamlingId is null).Select(i => i.VariableId).Distinct()];

        foreach (var chunk in chosen.Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
        {
            if (!await State!.AddItemsAsync(list, chunk))
            {
                return false;
            }
        }

        foreach (var chunk in unchosen.Chunk(IMuninExplorerClient.MaxVariablesPerBatch))
        {
            if (!await State!.AddVariablesAsync(list, chunk))
            {
                return false;
            }
        }

        return true;
    }

    // Real checkboxes in a fieldset, with Stiler's own names: one per datasamling the variable is in.
    private RenderFragment DatasamlingPicker(VariableListItem item) => builder =>
    {
        if (!IsUnresolved(item))
        {
            return;
        }

        builder.OpenElement(0, "fieldset");
        builder.AddAttribute(1, "class", "form-fieldset margin-bottom");
        builder.AddAttribute(2, "aria-describedby", PickerHintId(item));

        builder.OpenElement(3, "legend");
        builder.AddAttribute(4, "class", "form-element__label");
        builder.AddAttribute(5, "id", PickerLegendId(item));
        builder.AddContent(6, T.ChooseDatasamlingLegend);
        builder.CloseElement();

        builder.OpenElement(7, "p");
        builder.AddAttribute(8, "class", "caption");
        builder.AddAttribute(9, "id", PickerHintId(item));
        builder.AddContent(10, T.ChooseDatasamlingHint);
        builder.CloseElement();

        foreach (var candidate in item.CandidateDatasamlinger)
        {
            var id = candidate.Id;

            builder.OpenElement(11, "div");
            builder.SetKey(id);
            builder.OpenElement(12, "label");
            builder.AddAttribute(13, "class", "form-control");

            builder.OpenElement(14, "input");
            builder.AddAttribute(15, "type", "checkbox");
            builder.AddAttribute(16, "checked", _picked.Contains(id));
            builder.AddAttribute(17, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e =>
            {
                if (e.Value is true)
                {
                    _picked.Add(id);
                }
                else
                {
                    _picked.Remove(id);
                }
            }));
            builder.SetUpdatesAttributeName("checked");
            builder.CloseElement();

            builder.OpenElement(18, "span");
            builder.AddAttribute(19, "class", "form-control__label");
            builder.AddAttribute(20, "lang", CatalogueLang(candidate.Name));
            builder.AddContent(21, string.IsNullOrWhiteSpace(candidate.Name) ? T.NotSpecified : candidate.Name);
            builder.CloseElement();

            builder.CloseElement();
            builder.CloseElement();
        }

        // aria-disabled rather than disabled while a choice is saving, so the pressed button keeps focus.
        builder.OpenElement(22, "button");
        builder.AddAttribute(23, "class", "hd-button-square button-square--ghost-blue margin-top");
        builder.AddAttribute(24, "type", "button");
        builder.AddAttribute(25, "aria-disabled", _resolving ? "true" : null);
        builder.AddAttribute(26, "onclick", EventCallback.Factory.Create(this, () => ResolveDatasamlingAsync(item)));
        builder.AddContent(27, T.ChooseDatasamlingSave);
        builder.CloseElement();

        // Always present and empty until needed: an alert inserted and filled at once is announced unreliably.
        builder.OpenElement(28, "p");
        builder.AddAttribute(29, "class", _pickFailure is null ? "caption" : "infobox infobox--bg-yellow");
        builder.AddAttribute(30, "role", "alert");
        builder.AddAttribute(31, "aria-atomic", "true");
        builder.AddContent(32, _pickFailure);
        builder.CloseElement();

        builder.CloseElement();
    };

    /// <summary>
    /// Replaces an item whose datasamling is not chosen with one item per datasamling ticked, its
    /// "Ønskede data" and notes copied to each, and only then removes it.
    /// </summary>
    /// <remarks>
    /// In that order so a failure part-way leaves the reader's words on the item they started from:
    /// the new items may stand without them, but the old one is not removed until every copy landed.
    /// </remarks>
    private async Task ResolveDatasamlingAsync(VariableListItem item)
    {
        if (State is null || _shownList is not { } list || _resolving || !IsUnresolved(item))
        {
            return;
        }

        List<Guid> chosen = [.. item.CandidateDatasamlinger.Select(c => c.Id).Where(_picked.Contains)];

        if (chosen.Count == 0)
        {
            _pickFailure = T.ChooseDatasamlingNone;
            return;
        }

        _pickFailure = null;
        _resolving = true;

        try
        {
            if (!await State.AddItemsAsync(list, [.. chosen.Select(d => new VariableDatasamlingKey(item.VariableId, d))])
                || !await CopyAnnotationsAsync(list, item, chosen)
                || !await State.RemoveItemsAsync(list, [new VariableDatasamlingKey(item.VariableId, null)]))
            {
                _pickFailure = T.ChooseDatasamlingError;
                return;
            }

            ForgetNotesFor(list, VariableDatasamlingKey.Of(item));
            ForgetPicker();
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused choosing a datasamling for variable {VariableId} in list {ListId}", item.VariableId, list);
            _pickFailure = T.RateLimitError;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            Log?.LogWarning(ex, "the API refused choosing a datasamling for variable {VariableId} in list {ListId} as unauthorised", item.VariableId, list);
            _pickFailure = T.SignInRequiredError;
        }
        catch (Exception ex)
        {
            Log?.LogError(ex, "could not choose a datasamling for variable {VariableId} in list {ListId}", item.VariableId, list);
            _pickFailure = T.ChooseDatasamlingError;
        }
        finally
        {
            _resolving = false;
        }
    }

    // The new items' ids are read back, since an add answers with none, and the old item's words with them: the
    // page on screen may predate an edit saved since. Narrowed to the variable's kilde to keep the read short.
    private async Task<bool> CopyAnnotationsAsync(Guid list, VariableListItem item, IReadOnlyCollection<Guid> chosen)
    {
        var read = new List<VariableListItem>();
        var page = 1;
        IReadOnlyCollection<Guid>? kilder = item.KildeId is { } kilde ? [kilde] : null;

        while (true)
        {
            var slice = await Client.GetMyListVariablesAsync(list, page, 1000, kilder);

            if (slice is null)
            {
                return false;
            }

            read.AddRange(slice.Items.Where(i => i.VariableId == item.VariableId));

            if (slice.Items.Count == 0 || page * 1000 >= slice.TotalCount)
            {
                break;
            }

            page++;
        }

        var source = read.FirstOrDefault(i => i.DatasamlingId is null) ?? item;
        var desired = string.IsNullOrWhiteSpace(source.DesiredDataFreeText) ? null : source.DesiredDataFreeText;
        var notes = string.IsNullOrWhiteSpace(source.Notes) ? null : source.Notes;

        if (desired is null && notes is null)
        {
            return true;
        }

        List<Guid> targets = [.. read
            .Where(i => i.DatasamlingId is { } d && chosen.Contains(d))
            .Select(i => i.ItemId)
            .OfType<Guid>()];

        if (targets.Count != chosen.Count)
        {
            return false;
        }

        foreach (var target in targets)
        {
            if ((desired is not null
                    && (await Client.SetMyListItemDesiredDataAsync(list, target, desired)).Outcome != DesiredDataOutcome.Saved)
                || (notes is not null
                    && (await Client.SetMyListItemNotesAsync(list, target, notes)).Outcome != DesiredDataOutcome.Saved))
            {
                return false;
            }
        }

        return true;
    }
}
