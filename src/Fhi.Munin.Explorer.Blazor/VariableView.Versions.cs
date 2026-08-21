using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The variable's version history: what it has been called and been valid for, over time.
/// </summary>
public sealed partial class VariableView
{
    /// <summary>Which version rows are expanded, by version id.</summary>
    /// <remarks>
    /// A set rather than a single id: these are disclosures, not tabs. Comparing two versions is
    /// the reason to open the history at all, and closing one to read the next would make that the
    /// one thing it cannot do.
    /// </remarks>
    private readonly HashSet<Guid> _openVersions = [];

    private string VersionPanelId(Guid versionId) => $"munin-version-{versionId:N}";

    /// <summary>
    /// The versions, as they arrived.
    /// </summary>
    /// <remarks>
    /// From the detail rather than from <c>GetVariableTimelineAsync</c>. Measured on
    /// 2026-08-21: the two return the same ids in the same order, so the extra request buys a
    /// loading state and nothing else. The bead assumed the endpoint; the payload had already
    /// brought it.
    /// </remarks>
    private IReadOnlyList<VariableVersion> Versions => Variable?.Versions ?? [];

    /// <summary>
    /// The badge on a version: the one the reader is looking at, or its own status.
    /// </summary>
    /// <remarks>
    /// "Gjeldende" is not a status. Every version of every variable sampled on the test API comes
    /// back <c>Active</c>, including four superseded ones on the same variable — so a badge taken
    /// from <see cref="VariableVersion.Status"/> alone would call all five of them active and none
    /// of them current. It is an identity: the version whose id is the one this detail is.
    /// </remarks>
    private string VersionBadge(VariableVersion version) =>
        Variable?.VersionId == version.VersionId
            ? T.VersionCurrent
            : T.VersionStatusLabel(version.Status);

    /// <summary>A version's name, or that it has not got one.</summary>
    /// <remarks>
    /// Empty names are real and not rare: three of five versions on one sampled variable have no
    /// preferred term at all. A blank row would read as a rendering fault, so it says what is
    /// actually true — the catalogue holds a version here and no name for it.
    /// </remarks>
    private string VersionName(VariableVersion version) =>
        string.IsNullOrWhiteSpace(version.PreferredTerm) ? T.VersionUnnamed : version.PreferredTerm;

    private Task ToggleVersionAsync(Guid versionId)
    {
        if (!_openVersions.Remove(versionId))
        {
            _openVersions.Add(versionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>The version history, one disclosure per version.</summary>
    private RenderFragment VersionHistory => builder =>
    {
        if (Versions.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "variable-explorer-versions");

        var seq = 100;

        foreach (var version in Versions)
        {
            var open = _openVersions.Contains(version.VersionId);
            var panelId = VersionPanelId(version.VersionId);

            builder.OpenElement(seq, "li");

            builder.OpenElement(seq + 1, "button");
            builder.AddAttribute(seq + 2, "type", "button");
            builder.AddAttribute(seq + 3, "class", "variable-explorer-versions__toggle");
            builder.AddAttribute(seq + 4, "aria-expanded", open ? "true" : "false");
            builder.AddAttribute(seq + 5, "aria-controls", panelId);
            builder.AddAttribute(seq + 6, "onclick",
                EventCallback.Factory.Create(this, () => ToggleVersionAsync(version.VersionId)));

            // The catalogue's own name, so it stays Norwegian whoever is reading.
            builder.OpenElement(seq + 7, "span");
            builder.AddAttribute(seq + 8, "class", "variable-explorer-versions__name");
            builder.AddAttribute(seq + 9, "lang", CatalogueProperties.Foreign("no", Reader));
            builder.AddContent(seq + 10, VersionName(version));
            builder.CloseElement();

            builder.OpenElement(seq + 11, "span");
            builder.AddAttribute(seq + 12, "class", "variable-explorer-versions__badge");
            builder.AddContent(seq + 13, VersionBadge(version));
            builder.CloseElement();

            // Two spans rather than one string. A version with no start date would otherwise read
            // "— – Pågående", a dash immediately followed by a dash, which is a puzzle rather than
            // a date. Kept apart they also line up down the list and can be read as two columns,
            // which is how Runa lays them out.
            builder.OpenElement(seq + 14, "span");
            builder.AddAttribute(seq + 15, "class", "variable-explorer-versions__from");
            builder.AddContent(seq + 16, version.ValidFrom is { } from ? Day(from) : "—");
            builder.CloseElement();

            builder.OpenElement(seq + 17, "span");
            builder.AddAttribute(seq + 18, "class", "variable-explorer-versions__to");
            builder.AddContent(seq + 19, version.ValidTo is { } to ? Day(to) : T.Ongoing);
            builder.CloseElement();

            builder.CloseElement();

            builder.OpenElement(seq + 20, "div");
            builder.AddAttribute(seq + 21, "id", panelId);
            builder.AddAttribute(seq + 22, "class", "variable-explorer-versions__detail");

            // Hidden rather than absent, so the control it is named by always points at something.
            if (!open)
            {
                builder.AddAttribute(seq + 23, "hidden", true);
            }
            else
            {
                builder.AddContent(seq + 24, VersionDetail(version));
            }

            builder.CloseElement();

            builder.CloseElement();
            seq += 100;
        }

        builder.CloseElement();
    };

    /// <summary>What one version says about itself, once opened.</summary>
    private RenderFragment VersionDetail(VariableVersion version) => builder =>
    {
        var facts = new List<(string Label, string? Value, bool Norwegian)>
        {
            (T.FieldDescription, version.Description, true),
            (T.FieldValidFrom, version.ValidFrom is { } f ? Day(f) : null, false),
            (T.FieldValidTo, version.ValidTo is { } t ? Day(t) : T.Ongoing, false),
        };

        builder.AddContent(0, Facts(facts));
    };
}
