using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// Reads an explicit JSON <c>null</c> as <c>""</c> for a string the contract declares non-nullable.
/// </summary>
/// <remarks>
/// Why <c>""</c> and not a refusal, and why the value types are left refusing, is in AGENTS.md
/// under "What an explicit null does". A modifier rather than a converter on the options because
/// nullable reference annotations are erased: <c>string</c> and <c>string?</c> are one
/// <see cref="Type"/>, so a <c>JsonConverter&lt;string&gt;</c> would flatten the 71 properties
/// declared <c>string?</c> to <c>""</c> as well and stop the components falling back on them.
/// (Fhi.Metadata-o355u)
/// </remarks>
internal static class NullAsEmptyStrings
{
    private static readonly NullAsEmptyString Converter = new();

    /// <summary>Whether <paramref name="property"/> is a string the contract promises is never null.</summary>
    /// <remarks>
    /// Reachable from the tests so <c>ExplicitNullTest</c> can ask it of every contract property
    /// rather than trust that this covers them, the way <c>NullAsEmptyCollectionsTest</c> asks
    /// <see cref="NullAsEmptyCollections.CanConvert"/>.
    /// </remarks>
    internal static bool Covers(PropertyInfo property, NullabilityInfoContext nullability) =>
        property.PropertyType == typeof(string)
        && nullability.Create(property).ReadState == NullabilityState.NotNull;

    internal static void Modifier(JsonTypeInfo typeInfo)
    {
        // Per call: NullabilityInfoContext is not thread-safe, and nothing promises the resolver
        // runs under a lock.
        var nullability = new NullabilityInfoContext();

        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is PropertyInfo declared && Covers(declared, nullability))
            {
                property.CustomConverter ??= Converter;
            }
        }
    }

    private sealed class NullAsEmptyString : JsonConverter<string>
    {
        // The whole point. Left at its default, System.Text.Json never calls Read for a null token
        // and assigns the null itself, which is the behaviour being replaced.
        public override bool HandleNull => true;

        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType == JsonTokenType.Null ? "" : reader.GetString() ?? "";

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
