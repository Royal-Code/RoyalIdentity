using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class ProcessRequestObject : IDecorator<AuthorizeContext>
{
    public Task Decorate(AuthorizeContext context, Func<Task> next, CancellationToken ct)
    {
        // Not Supported Yet

        return next();
    }
}
