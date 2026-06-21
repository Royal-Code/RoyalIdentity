using RoyalCode.SmartCommands;

namespace RoyalIdentity.UserAccounts.Features.Accounts.Commons;

/// <summary>
/// SmartCommands DI aggregator for the module. The source generator emits
/// <c>AddUserAccountsHandlersServices&lt;TContext&gt;(IServiceCollection)</c> on this partial class,
/// registering every generated command handler in the module assembly.
/// </summary>
[AddHandlersServices("UserAccounts")]
public static partial class UserAccountsCommandServices
{
}
