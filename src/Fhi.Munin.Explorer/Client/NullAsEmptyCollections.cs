using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// Reads an explicit JSON <c>null</c> for a collection as the empty collection.
/// </summary>
/// <remarks>
/// Every collection on every contract is declared non-nullable with an initialiser — <c>= []</c>,
/// or an empty dictionary — and that initialiser only survives a key <em>absent</em> from the
/// payload. For an explicit <c>"additionalProperties": null</c>, <c>System.Text.Json</c> writes the
/// null straight over it, leaving a property whose type says it cannot be null holding null. The
/// first read of it throws while rendering, which is past the try/catch around the fetch, and on a
/// Blazor Server host takes the circuit and the page it is mounted in down.
/// <para>
/// That has shipped twice now, both times fixed at whichever read the payload happened to reach,
/// and the declaration those reads trusted said nothing about it either time. The Explorer API
/// demonstrably emits explicit nulls — <c>additionalProperties</c> is the key it has been seen on —
/// and nothing marks the sibling keys as incapable of it. Handled here rather than per read site
/// because here it is one rule for every collection on every contract, which is what makes the
/// declarations honest instead of leaving the next unguarded read waiting for the right payload.
/// That last sentence holds because <see cref="CanConvert"/> matches the two shapes the contracts
/// are spelled in and <c>NullAsEmptyCollectionsTest</c> fails the day one of them is not — see the
/// remarks there.
/// </para>
/// <para>
/// Null is read as empty rather than refused, because empty is what the payload means by it: no
/// curated properties, no groups, no codes. Refusing it would fail a whole page over one key that
/// had nothing in it.
/// </para>
/// <para>
/// Registered on <see cref="MuninExplorerClient.Json"/>, so it covers everything this client
/// deserialises. A host substituting its own <see cref="Contracts.IMuninExplorerClient"/> reads the
/// API with its own options and gets none of this — which is why the component-side reads of
/// <c>AdditionalProperties</c> still coalesce for themselves.
/// </para>
/// </remarks>
internal sealed class NullAsEmptyCollections : JsonConverterFactory
{
    /// <summary>
    /// The two shapes the contracts declare their collections as.
    /// </summary>
    /// <remarks>
    /// Dictionaries only when keyed by string, which every one of them is. A converter has to write
    /// property names itself, and doing that for an arbitrary key type means reimplementing the
    /// serialiser's key handling — for no contract that exists.
    /// <para>
    /// Which makes "every collection on every contract" a claim about how the contracts happen to
    /// be spelled, so it is checked rather than asked for: <c>NullAsEmptyCollectionsTest</c> walks
    /// every collection-typed property under <c>Contracts/</c> and fails on the first one this
    /// method does not match. A property declared <c>IReadOnlyCollection&lt;T&gt;</c>,
    /// <c>IEnumerable&lt;T&gt;</c>, <c>T[]</c> or a non-string-keyed dictionary would otherwise
    /// fall through to the old behaviour while its declaration went on promising otherwise — and
    /// the promise being trusted at the declaration is how this shipped twice.
    /// </para>
    /// </remarks>
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        var definition = typeToConvert.GetGenericTypeDefinition();

        return definition == typeof(IReadOnlyList<>)
               || (definition == typeof(IReadOnlyDictionary<,>)
                   && typeToConvert.GetGenericArguments()[0] == typeof(string));
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var arguments = typeToConvert.GetGenericArguments();

        var converter = typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            ? typeof(ListConverter<>).MakeGenericType(arguments)
            : typeof(DictionaryConverter<>).MakeGenericType(arguments[1]);

        return (JsonConverter)Activator.CreateInstance(converter)!;
    }

    private sealed class ListConverter<T> : JsonConverter<IReadOnlyList<T>>
    {
        // The whole point of the factory. Left at its default, System.Text.Json never calls Read
        // for a null token and assigns the null itself, which is the behaviour being replaced.
        public override bool HandleNull => true;

        public override IReadOnlyList<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null
                ? []
                : JsonSerializer.Deserialize<List<T>>(ref reader, options) ?? [];

        // Written out rather than handed back to the serialiser as IReadOnlyList<T>, which would
        // resolve this same converter again and recurse until the stack ran out.
        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();

                return;
            }

            writer.WriteStartArray();

            foreach (var item in value)
            {
                JsonSerializer.Serialize(writer, item, options);
            }

            writer.WriteEndArray();
        }
    }

    private sealed class DictionaryConverter<TValue> : JsonConverter<IReadOnlyDictionary<string, TValue>>
    {
        public override bool HandleNull => true;

        public override IReadOnlyDictionary<string, TValue> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null
                ? ReadOnlyDictionary<string, TValue>.Empty
                : (IReadOnlyDictionary<string, TValue>?)
                  JsonSerializer.Deserialize<Dictionary<string, TValue>>(ref reader, options)
                  ?? ReadOnlyDictionary<string, TValue>.Empty;

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlyDictionary<string, TValue> value,
            JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();

                return;
            }

            writer.WriteStartObject();

            foreach (var (key, item) in value)
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, item, options);
            }

            writer.WriteEndObject();
        }
    }
}
