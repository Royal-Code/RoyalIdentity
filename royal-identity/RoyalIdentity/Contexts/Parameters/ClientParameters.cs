using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Parameters;

/// <summary>
/// Client data loaded from stores by decorators during the pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <b>Parameters/* convention.</b> A <c>*Parameters</c> object holds state that decorators
/// <i>load</i> into the context during the pipeline — as opposed to direct context properties,
/// which carry data parsed from the raw request by <c>Load()</c>.
/// </para>
/// <para>Every <c>*Parameters</c> type follows the same mutation contract:</para>
/// <list type="bullet">
/// <item>Properties are <c>{ get; private set; }</c> — no public setters.</item>
/// <item>State is changed only through <c>Set*()</c> methods, called by decorators.</item>
/// <item>
/// <c>Assert*()</c> methods (annotated with <see cref="System.Diagnostics.CodeAnalysis.MemberNotNullAttribute"/>)
/// are called by late validators and handlers to guarantee a value is present.
/// </item>
/// </list>
/// </remarks>
public class ClientParameters
{
    /// <summary>
    /// Gets the client loaded for the current request.
    /// </summary>
    public Client? Client { get; private set; }

    /// <summary>
    /// Gets or sets the client secret for the current request.
    /// </summary>
    public EvaluatedCredential? ClientSecret { get; private set; }

    /// <summary>
    /// Gets or sets the authentication method for the current request.
    /// </summary>
    public string? AuthenticationMethod { get; private set; }

    /// <summary>
    /// Gets or sets the value of the confirmation method (will become the cnf claim). Must be a JSON object.
    /// </summary>
    /// <value>
    /// The confirmation.
    /// </value>
    public string? Confirmation => ClientSecret?.Confirmation;

    /// <summary>
    /// Ensures that a client has been loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no client has been loaded.</exception>
    [MemberNotNull(nameof(Client))]
    public void AssertHasClient()
    {
        if (Client is null)
            throw new InvalidOperationException("Client was required, but is missing");
    }

    /// <summary>
    /// Ensures that a client, evaluated credential, and authentication method have been loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the client secret data is incomplete.
    /// </exception>
    [MemberNotNull(nameof(ClientSecret), nameof(Client), nameof(AuthenticationMethod))]
    public void AssertHasClientSecret()
    {
        AssertHasClient();

        if (ClientSecret is null ||
            ClientSecret.Credential is null ||
            AuthenticationMethod is null)
        {
            throw new InvalidOperationException("ClientSecret was required, but is missing");
        }
    }

    /// <summary>
    /// Stores the client for later pipeline steps.
    /// </summary>
    /// <param name="client">The client loaded for the current request.</param>
    public void SetClient(Client client)
    {
        Client = client;
    }

    /// <summary>
    /// Stores the client and its evaluated credential for later pipeline steps.
    /// </summary>
    /// <param name="client">The client loaded for the current request.</param>
    /// <param name="clientSecret">The evaluated client credential.</param>
    /// <param name="authenticationMethod">The authentication method used by the client.</param>
    public void SetClientAndSecret(Client client, EvaluatedCredential clientSecret, string authenticationMethod)
    {
        SetClient(client);
        ClientSecret = clientSecret;
        AuthenticationMethod = authenticationMethod;
    }
}
