using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>One child of a kilde or delkilde in <see cref="KildeDetail"/>: exactly one of the two is set.</summary>
internal readonly record struct KildeChild(KildeDelkilde? Delkilde, KildeDatasamling? Datasamling);

/// <summary>
/// A parent's delkilder and datasamlinger as the one sequence <see cref="SiblingOrder.Merge{T}"/>
/// ranks them, shared so <see cref="KildeView"/> and <see cref="KildeSearch"/> cannot disagree.
/// </summary>
internal static class KildeChildren
{
    internal static IReadOnlyList<KildeChild> Of(KildeDetail kilde) => Of(kilde.Delkilder, kilde.Datasamlinger);

    internal static IReadOnlyList<KildeChild> Of(KildeDelkilde delkilde) =>
        Of(delkilde.Children, delkilde.Datasamlinger);

    private static IReadOnlyList<KildeChild> Of(
        IReadOnlyList<KildeDelkilde> delkilder, IReadOnlyList<KildeDatasamling> datasamlinger) =>
        SiblingOrder.Merge(
            delkilder.Select(d => new KildeChild(d, null)),
            datasamlinger.Select(d => new KildeChild(null, d)),
            child => child.Delkilde?.DisplayOrder ?? child.Datasamling?.DisplayOrder,
            child => child.Delkilde?.Name ?? child.Datasamling?.Name ?? "",
            child => child.Delkilde?.Id ?? child.Datasamling?.Id ?? Guid.Empty);

    /// <summary>
    /// The sequence cut where the kind changes, so each run of datasamlinger can be one table and
    /// each run of delkilder one list without either kind being moved past the other.
    /// </summary>
    internal static IEnumerable<IReadOnlyList<KildeChild>> Runs(IReadOnlyList<KildeChild> children)
    {
        List<KildeChild> run = [];

        foreach (var child in children)
        {
            if (run.Count > 0 && (run[0].Delkilde is null) != (child.Delkilde is null))
            {
                yield return run;
                run = [];
            }

            run.Add(child);
        }

        if (run.Count > 0)
        {
            yield return run;
        }
    }
}
