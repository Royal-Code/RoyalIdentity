using RoyalIdentity.Contexts;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts;

public interface ICodeFactory
{
    Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext context, CancellationToken ct);
}
