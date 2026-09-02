namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// How a redemption of an identity-link code ended.
/// </summary>
/// <remarks>
/// <para>
/// The API answers each refusal with a stable machine-readable string rather than a sentence, so
/// the wording is the caller's and can be said in the reader's language. This enum is that set,
/// and the distinctions it draws are the whole point of it: "that code has expired, make a new
/// one" and "check what you typed" are different instructions, and folding them into one "noe gikk
/// galt" leaves a reader retrying a code that can never work again.
/// </para>
/// <para>
/// A refusal is not an exception. Every member below is an answer the endpoint is designed to
/// give, and four of the five are things the reader can act on — so they are returned rather than
/// thrown, and only the failures nobody planned for (no network, a 500, a missing token) leave
/// <see cref="IMuninExplorerClient.RedeemIdentityLinkAsync"/> as one.
/// </para>
/// </remarks>
public enum IdentityLinkOutcome
{
    /// <summary>The two logins now share one person. The only success.</summary>
    Linked = 0,

    /// <summary>No such code, or the code was malformed — the reader should check what they typed.</summary>
    InvalidCode,

    /// <summary>The code existed but its ten minutes are up. A new one has to be made where it came from.</summary>
    ExpiredCode,

    /// <summary>The code has already been redeemed, so it is spent rather than wrong.</summary>
    CodeAlreadyUsed,

    /// <summary>Presented by the login that minted it, which links nothing.</summary>
    CannotLinkToSelf,

    /// <summary>Both logins already belong to a linked person, so there is nothing left to join.</summary>
    BothIdentitiesAlreadyLinked
}
