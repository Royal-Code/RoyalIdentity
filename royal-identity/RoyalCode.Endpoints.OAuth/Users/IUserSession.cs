
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
}