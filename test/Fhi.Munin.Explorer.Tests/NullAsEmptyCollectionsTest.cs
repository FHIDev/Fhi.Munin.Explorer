using System.Collections;
using System.Reflection;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Keeps "every collection on every contract" true rather than aspirational.
/// </summary>
/// <remarks>
/// <see cref="NullAsEmptyCollections"/> matches two declared shapes — <c>IReadOnlyList&lt;T&gt;</c>
/// and <c>IReadOnlyDictionary&lt;string, T&gt;</c> — and every collection under <c>Contracts/</c> is
/// one of them today. That is the whole of what makes the converter's claim, and the eight
/// <c>&lt;remarks&gt;</c> blocks on the contracts that repeat it, actually true.
/// <para>
/// A property later declared <c>IReadOnlyCollection&lt;T&gt;</c>, <c>IEnumerable&lt;T&gt;</c>,
/// <c>List&lt;T&gt;</c>, <c>T[]</c> or a dictionary keyed by anything but string falls straight
/// through <see cref="NullAsEmptyCollections.CanConvert"/> and gets the old null-overwrite
/// behaviour back, while its declaration goes on promising the deserialiser is what keeps it
/// non-null. The next author reads the promise and does not coalesce — which is exactly how this
/// bug shipped twice. <c>IReadOnlyCollection&lt;Guid&gt;</c> is not a hypothetical spelling either:
/// it is what the request bodies in <c>MuninExplorerClient</c> and the batch signatures on
/// <see cref="IMuninExplorerClient"/> already use, through these same options.
/// </para>
/// <para>
/// So it is checked rather than asked for, which is this repository's habit for the failures a
/// compiler cannot see — <c>assert-package-contents.sh</c>, <c>assert-sample-css-in-step.sh</c>,
/// <c>assert-portability-guard-armed.sh</c>. The day someone adds a shape the factory does not
/// cover, this fails and names it; the fix is to widen the factory or to change the declaration.
/// </para>
/// </remarks>
public class NullAsEmptyCollectionsTest
{
    [Fact]
    public void CanConvert_ForEveryCollectionOnEveryContract_ThenTheFactoryCoversIt()
    {
        var factory = new NullAsEmptyCollections();

        var uncovered = CollectionProperties()
            .Where(property => !factory.CanConvert(property.PropertyType))
            .Select(property =>
                $"{property.DeclaringType!.Name}.{property.Name} is {Spell(property.PropertyType)}")
            .ToList();

        Assert.True(
            uncovered.Count == 0,
            "NullAsEmptyCollections reads an explicit JSON null as the empty collection, and these " +
            "contract properties are declared in a shape it does not match — so an explicit null " +
            "overwrites their initialiser and the first read of them throws while rendering:" +
            Environment.NewLine + string.Join(Environment.NewLine, uncovered.Select(line => "  " + line)));
    }

    [Fact]
    public void CollectionProperties_WhenGathered_ThenThereAreSomeToCheck() =>
        // The guard above passes just as happily on nothing at all, and "nothing at all" is what a
        // namespace rename or a reflection filter that stopped matching would leave it looking at.
        // The count is not pinned — contracts are added — only that the sweep still finds them.
        Assert.NotEmpty(CollectionProperties());

    /// <summary>Every public property under <c>Contracts/</c> that <c>System.Text.Json</c> reads as an array or a keyed object.</summary>
    /// <remarks>
    /// <c>string</c> is an <see cref="IEnumerable"/> and is not a collection on the wire, and
    /// <c>byte[]</c> is written and read as a base64 string rather than an array — a JSON null for
    /// one of those is a null string, which is a different question and one the nullable annotation
    /// already answers. <see cref="ExportedList.Bytes"/> is the only one of those here, and it is
    /// never deserialised at all: it is the response body read as bytes.
    /// <para>
    /// Exceptions are left out for the same reason and a blunter one: the namespace holds
    /// <see cref="MuninExplorerRateLimitedException"/>, and every exception inherits
    /// <c>Exception.Data</c>, an <see cref="IDictionary"/> that is nobody's contract. Nothing else
    /// is filtered — a type added under <c>Contracts/</c> is in this sweep by being there.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<PropertyInfo> CollectionProperties() =>
        typeof(IMuninExplorerClient).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == typeof(IMuninExplorerClient).Namespace
                           && !typeof(Exception).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.GetMethod is not null && IsCollection(property.PropertyType))
            .ToList();

    private static bool IsCollection(Type type) =>
        type != typeof(string)
        && type != typeof(byte[])
        && typeof(IEnumerable).IsAssignableFrom(type);

    /// <summary>The declared type as C# spells it, so a failure names the shape to fix.</summary>
    private static string Spell(Type type)
    {
        if (type.IsArray)
        {
            return Spell(type.GetElementType()!) + "[]";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Spell))}>";
    }
}
