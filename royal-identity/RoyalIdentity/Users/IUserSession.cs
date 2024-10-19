
using System.Security.Claims;

namespace RoyalIdentity.Users;

public interface IUserSession
{
    /// <summary>
    /// Gets the current authenticated user.
    /// </summary>
    [Redesign("Este método é utilizado no IS4, mas é esperado um handler de autenticação que providencie o usuário pelo HttpContext")]
    [Redesign("O handler já deverá validar se a sessão estará ativa ou não.")]
    ValueTask<ClaimsPrincipal?> GetUserAsync();

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">clientId</exception>
    public Task AddClientIdAsync(string clientId);
}