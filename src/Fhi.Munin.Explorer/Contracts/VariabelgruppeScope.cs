namespace Fhi.Munin.Explorer.Contracts;

/// <summary>A variabelgruppe chosen under one owner: the kilde, delkilde or datasamling it was ticked beneath.</summary>
/// <remarks>
/// A group hangs under every datasamling its variables are in, so a bare id filters it in all of
/// them. This keeps a tick in the kilde tree to the one placement the reader pressed. On the wire it
/// is <c>vgId:ownerId</c>, which is what the API's <c>variabelgruppeScopes</c> binds.
/// </remarks>
public readonly record struct VariabelgruppeScope(Guid VariabelgruppeId, Guid OwnerId)
{
    /// <inheritdoc/>
    public override string ToString() => $"{VariabelgruppeId}:{OwnerId}";

    /// <summary>Read the wire form back. False for anything that is not two guids around one colon.</summary>
    public static bool TryParse(string? value, out VariabelgruppeScope scope)
    {
        scope = default;
        var separator = value?.IndexOf(':', StringComparison.Ordinal) ?? -1;

        if (value is null
            || separator <= 0
            || !Guid.TryParse(value.AsSpan(0, separator), out var variabelgruppeId)
            || !Guid.TryParse(value.AsSpan(separator + 1), out var ownerId))
        {
            return false;
        }

        scope = new VariabelgruppeScope(variabelgruppeId, ownerId);
        return true;
    }
}
