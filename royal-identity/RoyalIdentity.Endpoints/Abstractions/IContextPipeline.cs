namespace RoyalIdentity.Endpoints.Abstractions;

public interface IContextPipeline<TContext>
    where TContext : class, IContextBase
{

    Task SendAsync(TContext context);
}
