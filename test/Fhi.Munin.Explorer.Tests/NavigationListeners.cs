using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Who is still subscribed to <see cref="NavigationManager.LocationChanged"/>, read off the event
/// itself. Reflection because a leak has no other symptom: rendering a disposed component is a
/// no-op, so a handler left behind costs nothing any assertion on a view could see.
/// </summary>
internal static class NavigationListeners
{
    /// <summary>The components still listening, or an empty list when nobody is.</summary>
    internal static IReadOnlyList<object> Of(NavigationManager navigation)
    {
        var field = typeof(NavigationManager).GetField(
            "_locationChanged", BindingFlags.Instance | BindingFlags.NonPublic);

        // Asserted rather than assumed: a framework renaming it would leave every caller reading
        // an empty list and passing, which is the same false green the leak itself is.
        Assert.NotNull(field);

        return field.GetValue(navigation) is EventHandler<LocationChangedEventArgs> subscribed
            ? [.. subscribed.GetInvocationList().Select(handler => handler.Target!)]
            : [];
    }
}
