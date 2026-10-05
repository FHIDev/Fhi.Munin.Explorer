using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

// The action row's two folds: «Last ned», and «Flere valg» holding every action but creating.
public sealed partial class VariableListView
{
    private bool _menuOpen;
    private bool _downloadOpen;

    // Moved by every disclosure press and by CloseFolds, so a late share can tell the reader acted since.
    private int _readerMoves;
    private MenuFocus _menuFocus;
    private ElementReference _menuToggle;
    private ElementReference _downloadToggle;
    private ElementReference _renameField;
    private ElementReference _copyField;
    private ElementReference _sharedCodeField;
    private ElementReference _shareCodeField;

    private string MenuToggleId => $"munin-explorer-list-menu-toggle-{_instance}";

    private string MenuPanelId => $"munin-explorer-list-menu-{_instance}";

    private string DownloadToggleId => $"munin-explorer-list-download-toggle-{_instance}";

    private string DownloadPanelId => $"munin-explorer-list-download-{_instance}";

    /// <summary>Where focus goes once the fold has closed under the control that held it.</summary>
    private enum MenuFocus
    {
        None = 0,
        Toggle,
        DownloadToggle,
        Rename,
        Copy,
        OpenShared,
        Share
    }

    // One fold at a time: both panels hang over the rows below, and two open paint one over the other.
    private void ToggleMenuFromControl(MouseEventArgs released)
    {
        var wasOpen = _menuOpen;
        Toggle(released, ref _menuOpen);

        if (wasOpen && !_menuOpen)
        {
            DisarmConfirmations();
        }
        else if (_menuOpen)
        {
            _downloadOpen = false;
        }
    }

    private void ToggleDownloadFromControl(MouseEventArgs released)
    {
        Toggle(released, ref _downloadOpen);

        if (_downloadOpen)
        {
            _menuOpen = false;
            DisarmConfirmations();
        }
    }

    // Closed without moving focus, which stays on the control the reader pressed outside them.
    private void CloseFolds()
    {
        _readerMoves++;
        _menuOpen = false;
        _downloadOpen = false;
        DisarmConfirmations();
    }

    private void CloseDownloadOnEscape(KeyboardEventArgs pressed)
    {
        if (pressed.Key == "Escape" && _downloadOpen)
        {
            _downloadOpen = false;
            _menuFocus = MenuFocus.DownloadToggle;
        }
    }

    private void CloseMenuOnEscape(KeyboardEventArgs pressed)
    {
        if (pressed.Key == "Escape" && _menuOpen)
        {
            CloseMenu();
        }
    }

    // A confirmation left armed behind a closed fold is one press from destroying the list next time.
    private void CloseMenu(MenuFocus focus = MenuFocus.Toggle)
    {
        CloseFolds();
        _menuFocus = focus;
    }

    private void DisarmConfirmations()
    {
        _confirmingDelete = false;
        _confirmingEmpty = false;
    }

    // A refused or ignored press changes nothing and leaves the fold open, with the reason beside it.
    private void LeaveMenuFor(bool openBefore, bool openAfter, MenuFocus field)
    {
        if (openBefore != openAfter)
        {
            CloseMenu(openAfter ? field : MenuFocus.Toggle);
        }
    }

    private void ChooseRenamingFromMenu(MouseEventArgs released)
    {
        var before = _renaming;
        ToggleRenamingFromControl(released);
        LeaveMenuFor(before, _renaming, MenuFocus.Rename);
    }

    private void ChooseCopyingFromMenu(MouseEventArgs released)
    {
        var before = _copying;
        ToggleCopyingFromControl(released);
        LeaveMenuFor(before, _copying, MenuFocus.Copy);
    }

    private void ChooseOpeningSharedFromMenu(MouseEventArgs released)
    {
        var before = _openingShared;
        ToggleOpeningSharedFromControl(released);
        LeaveMenuFor(before, _openingShared, MenuFocus.OpenShared);
    }

    private async Task ChooseSharingFromMenu(MouseEventArgs released)
    {
        var before = _sharingList;
        var moves = _readerMoves;
        await ToggleSharingFromControl(released);

        // The press moved the count once; any more is the reader acting while the share was out.
        if (_readerMoves - moves <= 1)
        {
            LeaveMenuFor(before, _sharingList, MenuFocus.Share);
        }
    }

    // Closed before the write, so focus lands on the toggle as the pressed button leaves.
    private Task ConfirmDeleteFromMenuAsync()
    {
        CloseMenu();
        return DeleteListAsync();
    }

    private Task ConfirmEmptyFromMenuAsync()
    {
        CloseMenu();
        return EmptyListAsync();
    }

    private ElementReference? TakeMenuFocusTarget()
    {
        var focus = _menuFocus;
        _menuFocus = MenuFocus.None;

        return focus switch
        {
            MenuFocus.None => null,
            MenuFocus.DownloadToggle => _downloadToggle,
            MenuFocus.Rename => _renameField,
            MenuFocus.Copy => _copyField,
            MenuFocus.OpenShared => _sharedCodeField,

            // A share that failed leaves the panel open with no code in it to focus.
            MenuFocus.Share when _shareMade?.ListId == _shownList => _shareCodeField,
            _ => _menuToggle
        };
    }
}
