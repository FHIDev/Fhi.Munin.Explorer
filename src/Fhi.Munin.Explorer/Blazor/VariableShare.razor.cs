using Fhi.Munin.Explorer.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// «Del variabel»: copy a link to one variable, or open an e-mail with it. One group for every place a variable is
/// shown, so the list row, the search row and the whole variable say and do the same thing.
/// </summary>
/// <remarks>
/// A building block of the explorer's own views, public only because Razor makes every component so. Hosts get it
/// through <see cref="VariableExplorer"/>; mounting it alone carries no stability promise.
/// </remarks>
public sealed partial class VariableShare : ComponentBase
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;

    /// <summary>The absolute link that opens this variable.</summary>
    [Parameter, EditorRequired] public string Link { get; set; } = "";

    /// <summary>The variable's name as the reader sees it, for the e-mail's subject and body.</summary>
    [Parameter, EditorRequired] public string Name { get; set; } = "";

    /// <summary>The group heading's id; the e-mail hint's id is derived from it.</summary>
    [Parameter, EditorRequired] public string HeadingId { get; set; } = "";

    /// <summary>The id of the field that shows the link when the browser refuses to copy it.</summary>
    [Parameter, EditorRequired] public string FieldId { get; set; } = "";

    /// <summary>The reader's language, as on the component it sits in.</summary>
    [Parameter] public string Language { get; set; } = "no";

    private ILogger? _log;
    private string? _status;
    private bool _notCopied;
    private string? _shownLink;
    private int _generation;

    private ILogger? Log => _log ??= ExplorerLog.For<VariableShare>(ServiceProvider);

    private Texts T => Texts.For(Language);

    private string MailHintId => $"{HeadingId}-mail-hint";

    private string MailTo =>
        $"mailto:?subject={Uri.EscapeDataString(T.ShareVariableMailSubject(Name))}&body={Uri.EscapeDataString(T.ShareVariableMailBody(Name, Link))}";

    // Another variable in the same place: what was said about the last one's link no longer holds.
    protected override void OnParametersSet()
    {
        if (_shownLink != Link)
        {
            _shownLink = Link;
            Forget();
        }
    }

    private void Forget()
    {
        _generation++;
        _status = null;
        _notCopied = false;
    }

    // Safari refuses a clipboard write that arrives after a server round trip, so a refusal shows the
    // link itself to be copied by hand. Emptied first, so a second press is announced again.
    private async Task CopyAsync()
    {
        Forget();
        var asked = _generation;

        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", Link);
            Say(asked, T.VariableLinkCopied, notCopied: false);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException)
        {
            Log?.LogWarning(ex, "the browser refused to copy a variable link");
            Say(asked, T.VariableLinkNotCopied, notCopied: true);
        }
    }

    // Only for the link that was asked about: a press arriving after the variable changed says nothing.
    private void Say(int asked, string status, bool notCopied)
    {
        if (asked != _generation)
        {
            return;
        }

        _status = status;
        _notCopied = notCopied;
    }
}
