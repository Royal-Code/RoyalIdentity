using RoyalIdentity.Contracts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Contexts.Decorators;

/// <summary>
/// Prevents a silent authorization request from falling through to an interactive response.
/// </summary>
/// <remarks>
/// This decorator wraps every downstream interaction producer, including components appended through
/// <see cref="CustomOptions.CustomizeAuthorizeContext"/>. It must therefore remain before those producers in
/// the authorize pipeline: a terminal custom decorator is still observed when its continuation returns.
/// </remarks>
public class PromptNoneInteractionDecorator(ISessionStateGenerator sessionStateGenerator)
    : IDecorator<AuthorizeContext>
{
    public async Task Decorate(AuthorizeContext context, Func<Task> next, CancellationToken ct)
    {
        await next();

        if (!context.RequestedPromptModes.Contains(Oidc.PromptModes.None) ||
            context.Response is not InteractionResponse)
        {
            return;
        }

        context.Response = AuthorizeResponseFactory.Interaction(
            sessionStateGenerator,
            context,
            AuthorizeInteractionKind.Other,
            "The request requires user interaction.");
    }
}
