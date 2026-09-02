using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The way in for a code minted on the reader's other login, which joins the two accounts.
/// </summary>
/// <remarks>
/// <para>
/// The component only ever <em>receives</em> a link. Signing in here starts nothing and navigating
/// here starts nothing: it runs inside a CMS page that is not ours, and opening a dialog over
/// somebody else's page because a reader happened to log in would be an intrusion whichever way
/// the feature was meant to go (Fhi.Metadata-bl448).
/// </para>
/// <para>
/// Typed rather than deep-linked. A code carried in a query parameter would be the better journey,
/// but nothing has verified that a parameter survives the host's routing, so this builds the half
/// with no unknowns in it and leaves the deep link to be added once somebody has tested that.
/// </para>
/// </remarks>
public partial class VariableExplorer
{
    /// <summary>Where the reader is in the two-step redemption.</summary>
    private enum LinkStage
    {
        /// <summary>Typing the code. Also where a refusal returns them, with the code still there.</summary>
        Entering = 0,

        /// <summary>Shown what linking does, and asked to confirm it.</summary>
        Confirming,

        /// <summary>The redemption is in flight.</summary>
        Working,

        /// <summary>Linked. There is nothing left to do here.</summary>
        Linked
    }

    private LinkStage _linkStage;
    private string _linkCode = "";
    private IdentityLinkOutcome? _linkFailure;

    // Told apart from a refusal on purpose: a refusal is an answer about the code, and this is the
    // call never arriving. "Sjekk koden" would be a lie about a network that was down.
    private bool _linkThrew;
    private bool _linkThrottled;

    private string AccountLinkCodeId => $"munin-explorer-account-link-code-{_instance}";

    // Drawn only for a signed-in reader, the same rule the save button follows: redeeming is an
    // authenticated write, so signed out there is nothing the control could do.
    private bool ShowAccountLink => ListState?.IsAuthenticated == true;

    private RenderFragment AccountLink() => builder =>
    {
        if (!ShowAccountLink)
        {
            return;
        }

        // The same <details> the column picker is, wearing the same two borrowed names, so the two
        // entries in this row look like one another and neither needs script to open and close.
        builder.OpenElement(0, "details");
        builder.AddAttribute(1, "class", "dropdown munin-explorer__dropdown");
        builder.AddAttribute(2, "style", "position:relative");

        builder.OpenElement(3, "summary");
        builder.AddAttribute(4, "class",
            "hd-button-square button-square--ghost munin-explorer-header__actions-button");
        builder.AddContent(5, T.LinkAccount);
        builder.CloseElement();

        builder.OpenElement(6, "div");
        builder.AddAttribute(7, "class", "munin-explorer-account-link");

        switch (_linkStage)
        {
            case LinkStage.Entering:
                BuildCodeEntry(builder);
                break;

            case LinkStage.Confirming:
            case LinkStage.Working:
                BuildConfirmation(builder);
                break;

            case LinkStage.Linked:
                break;
        }

        // Always here and empty until there is something to say: a role="alert" inserted and filled
        // in one update is announced unreliably. Numbered above both builders above rather than
        // continuing from 8 — a descending run lets the diff re-add it, which is the same failure.
        builder.OpenElement(70, "p");
        builder.AddAttribute(71, "role", "alert");
        builder.AddAttribute(72, "aria-live", "assertive");
        builder.AddAttribute(73, "aria-atomic", "true");
        builder.AddContent(74, LinkMessage);
        builder.CloseElement();

        builder.CloseElement();
        builder.CloseElement();
    };

    private void BuildCodeEntry(RenderTreeBuilder builder)
    {
        builder.OpenElement(20, "label");
        builder.AddAttribute(21, "class", "form-element__label");
        builder.AddAttribute(22, "for", AccountLinkCodeId);
        builder.AddContent(23, T.LinkCodeLabel);
        builder.CloseElement();

        // Stiler's own free-text input, the one the search box wears. No name of our own: a class
        // Stiler has never heard of draws a raw browser default inside an otherwise styled page.
        builder.OpenElement(24, "input");
        builder.AddAttribute(25, "class", "searchbox__freetext");
        builder.AddAttribute(26, "id", AccountLinkCodeId);
        builder.AddAttribute(27, "type", "text");

        // Off, all four. A linking code is single-use and expires in ten minutes, so a browser
        // offering the last one back is offering a code that can no longer work.
        builder.AddAttribute(28, "autocomplete", "off");
        builder.AddAttribute(29, "autocapitalize", "off");
        builder.AddAttribute(30, "autocorrect", "off");
        builder.AddAttribute(31, "spellcheck", "false");
        builder.AddAttribute(32, "value", _linkCode);
        builder.AddAttribute(33, "onchange",
            EventCallback.Factory.CreateBinder(this, v => _linkCode = v ?? "", _linkCode));
        builder.CloseElement();

        builder.OpenElement(34, "div");
        builder.AddAttribute(35, "class", "munin-explorer-account-link__actions");

        builder.OpenElement(36, "button");
        builder.AddAttribute(37, "class", "hd-button-square button-square--secondary");
        builder.AddAttribute(38, "type", "button");
        builder.AddAttribute(39, "onclick", EventCallback.Factory.Create(this, StartConfirming));
        builder.AddContent(40, T.LinkContinue);
        builder.CloseElement();

        builder.CloseElement();
    }

    private void BuildConfirmation(RenderTreeBuilder builder)
    {
        var working = _linkStage == LinkStage.Working;

        // What the reader is agreeing to, said as its consequence. It cannot name the account on
        // the other side: no endpoint previews a code, and this component is told only whether
        // somebody is signed in — never who (Fhi.Metadata-bl448).
        builder.AddContent(50, RenderParagraph(working ? T.LinkWorking : T.LinkConfirmQuestion));

        // The same two buttons while the call is in flight, inert rather than gone. Removing the
        // one the reader just pressed drops focus to <body>, which is the failure aria-disabled
        // exists to avoid here as it does on the pager. RedeemAsync is what makes the press inert.
        builder.OpenElement(51, "div");
        builder.AddAttribute(52, "class", "munin-explorer-account-link__actions");

        builder.OpenElement(53, "button");
        builder.AddAttribute(54, "class", "hd-button-square button-square--secondary");
        builder.AddAttribute(55, "type", "button");
        builder.AddAttribute(56, "aria-disabled", AriaDisabled(!working));
        builder.AddAttribute(57, "onclick", EventCallback.Factory.Create(this, RedeemAsync));
        builder.AddContent(58, T.LinkConfirm);
        builder.CloseElement();

        builder.OpenElement(59, "button");
        builder.AddAttribute(60, "class", "hd-button-square button-square--ghost");
        builder.AddAttribute(61, "type", "button");
        builder.AddAttribute(62, "aria-disabled", AriaDisabled(!working));
        builder.AddAttribute(63, "onclick", EventCallback.Factory.Create(this, CancelConfirming));
        builder.AddContent(64, T.LinkCancel);
        builder.CloseElement();

        builder.CloseElement();
    }

    private RenderFragment RenderParagraph(string text) => builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "caption");
        builder.AddContent(2, text);
        builder.CloseElement();
    };

    // The condition and not the sentence, so a host that switches language mid-session does not
    // leave this panel speaking the old one.
    private string? LinkMessage => _linkStage switch
    {
        LinkStage.Linked => T.LinkSucceeded,
        _ when _linkThrottled => T.RateLimitError,
        _ when _linkThrew => T.LinkError,
        _ => _linkFailure switch
        {
            IdentityLinkOutcome.InvalidCode => T.LinkInvalidCode,
            IdentityLinkOutcome.ExpiredCode => T.LinkExpiredCode,
            IdentityLinkOutcome.CodeAlreadyUsed => T.LinkCodeAlreadyUsed,
            IdentityLinkOutcome.CannotLinkToSelf => T.LinkCannotLinkToSelf,
            IdentityLinkOutcome.BothIdentitiesAlreadyLinked => T.LinkBothAlreadyLinked,
            _ => null
        }
    };

    private void StartConfirming()
    {
        ClearLinkResult();
        _linkStage = LinkStage.Confirming;
    }

    private void CancelConfirming()
    {
        // Inert while the redemption is in flight: the code is already spent by then, and taking
        // the reader back to the field would offer them a second press of a one-shot credential.
        if (_linkStage == LinkStage.Working)
        {
            return;
        }

        ClearLinkResult();
        _linkStage = LinkStage.Entering;
    }

    private void ClearLinkResult()
    {
        _linkFailure = null;
        _linkThrew = false;
        _linkThrottled = false;
    }

    private async Task RedeemAsync()
    {
        // The press that arrives while the first one is still in flight. A linking code is
        // single-use, so a second redemption of it would answer code_already_used and tell the
        // reader their own successful link had failed.
        if (_linkStage == LinkStage.Working)
        {
            return;
        }

        ClearLinkResult();
        _linkStage = LinkStage.Working;

        try
        {
            var outcome = await Client.RedeemIdentityLinkAsync(_linkCode);

            if (outcome == IdentityLinkOutcome.Linked)
            {
                _linkStage = LinkStage.Linked;

                // The code is spent and the panel is done with it. Keeping it would leave a
                // single-use credential in the DOM for the rest of the circuit.
                _linkCode = "";
            }
            else
            {
                _linkFailure = outcome;
                _linkStage = LinkStage.Entering;
            }
        }
        catch (MuninExplorerRateLimitedException)
        {
            _linkThrottled = true;
            _linkStage = LinkStage.Entering;
        }
        catch (Exception)
        {
            // Caught the way every other await in this component catches: an unhandled exception
            // out of an EventCallback takes the whole circuit down with it.
            _linkThrew = true;
            _linkStage = LinkStage.Entering;
        }

        StateHasChanged();
    }
}
