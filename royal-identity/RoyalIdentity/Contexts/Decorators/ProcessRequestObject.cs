using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class ProcessRequestObject : IDecorator<AuthorizeContext>
{
    public ValueTask Decorate(AuthorizeContext context, Func<ValueTask> next, CancellationToken ct)
    {
        // Not Supported Yet

        return next();
    }
}
