using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>A link that opens one of the reader's own lists (Fhi.Metadata-9huq2.61).</summary>
public sealed partial class VariableListView
{
    /// <summary>
    /// One of the reader's own lists to open, by the id <c>GET api/explorer/my/lists</c> gives it, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read only with <c>MuninExplorer:Features:ListLink</c> on. Signed in, the list is shown when it is one of the reader's,
    /// and the view says it was not found when it is not; while a <see cref="ShareCode"/> is open, the shared list stays.
    /// In each case <see cref="OwnListIdChanged"/> then reports null, so a host writing the id into its address can drop it.
    /// While the reader's lists cannot be read the id waits, and is followed once a read, or the retry, succeeds. An id set
    /// again after null is followed again.
    /// </para>
    /// <para>
    /// Signed out nothing is reported, so the id stays where the host keeps it until the reader signs in. With the
    /// browser list on, the view says above it that signing in opens the list.
    /// </para>
    /// </remarks>
    [Parameter] public Guid? OwnListId { get; set; }

    /// <summary>Raised with null once <see cref="OwnListId"/> has been opened or found missing.</summary>
    [Parameter] public EventCallback<Guid?> OwnListIdChanged { get; set; }

    private Guid? _followedOwnListId;

    // Said in the alert region until the reader's next action clears it, as every failure there is.
    private bool _ownListMissing;

    private Guid? OwnListLink => ServiceProvider.GetService<ListLinkSwitch>()?.On == true ? OwnListId : null;

    // Signed out, the link waits for sign-in; this sentence says so above the browser list.
    private bool OwnListAwaitsSignIn => OwnListLink is not null && State?.IsAuthenticated != true;

    private async Task FollowOwnListIdAsync()
    {
        if (OwnListLink is not { } id)
        {
            _followedOwnListId = null;
            return;
        }

        if (id == _followedOwnListId || State is not { IsAuthenticated: true })
        {
            return;
        }

        // A shared list wins, and the id still goes, so the address beside the code carries no private list.
        if (ShowsSharedList)
        {
            _followedOwnListId = id;
            await OwnListIdChanged.InvokeAsync(null);
            return;
        }

        // Not answered while the lists could not be read: that failure is said already, and a later render asks again.
        if (_failed || State.ListsReadFailed || !State.HasLoaded)
        {
            return;
        }

        _followedOwnListId = id;

        if (Lists.Any(l => l.Id == id))
        {
            if (_shownList != id)
            {
                await ShowListAsync(id);
            }
        }
        else
        {
            ForgetFailures();
            _ownListMissing = true;
        }

        await OwnListIdChanged.InvokeAsync(null);
    }
}
