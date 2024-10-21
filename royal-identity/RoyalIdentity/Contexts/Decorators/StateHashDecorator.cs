using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contexts.Decorators;

public class StateHashDecorator : IDecorator<AuthorizeContext>
{
    private readonly IKeyManager keyManager;

    public StateHashDecorator(IKeyManager keyManager)
    {
        this.keyManager = keyManager;
    }

    public async Task Decorate(AuthorizeContext context, Func<Task> next, CancellationToken ct)
    {
        context.AssertHasClient();

        if (context.State.IsPresent())
        {
            var credential = await keyManager.GetSigningCredentialsAsync(context.Client.AllowedIdentityTokenSigningAlgorithms, ct)
                    ?? throw new InvalidOperationException("No signing credential is configured.");

            var algorithm = credential.Algorithm;
            var stateHash = CryptoHelper.CreateHashClaimValue(context.State, algorithm);

            context.StateHash = stateHash;
        }

        await next();
    }
}
