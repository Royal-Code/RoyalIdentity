namespace RoyalIdentity.Contracts.Models;

/// <summary>
/// The mechanism a direct request used to present client credentials, decided once, before any credential is
/// read or validated.
/// </summary>
/// <remarks>
/// A client certificate is deliberately not a source here. It is a property of the connection rather than a
/// credential the request presented, and a deployment may terminate mTLS for reasons unrelated to client
/// authentication. Counting it would turn every certificate-bearing connection into a second mechanism and
/// refuse otherwise valid Basic requests. It is used only when the request presented nothing in band —
/// <see cref="None"/>.
/// </remarks>
public enum ClientAuthenticationSource
{
    /// <summary>No credential in the request itself; the connection certificate, or no secret at all, decides.</summary>
    None = 0,

    /// <summary>
    /// An <c>Authorization</c> header, whatever scheme it names. The scheme is deliberately not part of the
    /// decision: a header the endpoint cannot use is still a client trying to authenticate, and classifying it
    /// as "nothing presented" is what let an unusable header fall through to the connection certificate or to
    /// the no-secret path. Only <c>Basic</c> is supported, and any other scheme fails authentication.
    /// </summary>
    AuthorizationHeader,

    /// <summary><c>client_secret</c> in the request body.</summary>
    PostBody,

    /// <summary><c>client_assertion</c> plus <c>client_assertion_type</c> in the request body.</summary>
    ClientAssertion,
}

/// <summary>
/// What the preflight concluded about client authentication, carried on the context so the evaluation chain
/// never has to re-derive it — and never disagrees with it.
/// </summary>
public sealed class ClientAuthenticationAttempt
{
    public static readonly ClientAuthenticationAttempt NoneAttempt =
        new(ClientAuthenticationSource.None);

    public ClientAuthenticationAttempt(ClientAuthenticationSource source)
    {
        Source = source;
    }

    public ClientAuthenticationSource Source { get; }

    /// <summary>
    /// Whether authentication was attempted through the <c>Authorization</c> header. RFC 6749 §5.2 answers
    /// those failures with HTTP 401 and a <c>WWW-Authenticate</c> challenge, and every other mechanism with 400.
    /// </summary>
    public bool ViaAuthorizationHeader => Source is ClientAuthenticationSource.AuthorizationHeader;
}
