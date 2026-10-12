using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

// One enum rather than two booleans, so "both open at once" cannot be written down.
internal enum SourceKind
{
    Kilde,
    Datasamling
}

/// <summary>The owners a variable's panel can open out into, for the search and the list alike.</summary>
internal static class VariableOwners
{
    // KildeId is a bare Guid, so "no kilde" arrives as Guid.Empty; treating it as absent keeps a
    // button off the screen that could only ever report "not found".
    public static Guid? IdOf(VariableDetail detail, SourceKind kind)
    {
        var id = kind == SourceKind.Kilde ? detail.KildeId : detail.DatasamlingId;

        return id is { } value && value != Guid.Empty ? value : null;
    }

    // Widest first, the order the kilde trail above the buttons names them in.
    public static IReadOnlyList<SourceKind> Targets(VariableDetail detail) =>
        [.. new[] { SourceKind.Kilde, SourceKind.Datasamling }.Where(kind => IdOf(detail, kind) is not null)];
}
