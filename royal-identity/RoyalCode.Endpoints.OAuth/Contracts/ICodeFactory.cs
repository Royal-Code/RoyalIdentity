using RoyalIdentity.Contexts;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts;

public interface ICodeFactory
{
    Task<AuthorizationCode> CreateCodeAsync(AuthorizeContext context, CancellationToken ct);
}
