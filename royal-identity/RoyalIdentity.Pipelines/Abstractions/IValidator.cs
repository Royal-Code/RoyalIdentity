namespace RoyalIdentity.Pipelines.Abstractions;

public interface IValidator<in TContext>
{
    ValueTask Validate(TContext context, CancellationToken ct);
}