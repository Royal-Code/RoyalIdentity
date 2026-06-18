using Microsoft.EntityFrameworkCore;
using RoyalCode.Aggregates;
using RoyalCode.DomainEvents;
using RoyalCode.SmartCommands;
using RoyalCode.SmartProblems;
using RoyalCode.SmartSearch;
using RoyalCode.SmartSelector;
using RoyalCode.SmartValidations;
using RoyalCode.WorkContext.EntityFramework.Configurations;

namespace RoyalIdentity.UserAccounts.Features.PreFlight;

/// <summary>
/// Compile-time smoke for the RoyalCode APIs selected by plan-users-accounts-module-v2 Fase 3.
/// The real account model and features land in later phases; these internal types keep package/API drift visible.
/// </summary>
public static class RoyalCodePreFlightSmoke
{
	public static IWorkContextBuilder<TDbContext> ConfigureUserAccountsSmoke<TDbContext>(
		this IWorkContextBuilder<TDbContext> builder)
		where TDbContext : DbContext
	{
		_ = typeof(ICriteria<SmokeAccount>);
		return builder;
	}

	public static ICriteria<SmokeAccount> Apply(ICriteria<SmokeAccount> criteria, SmokeAccountFilter filter)
	{
		return criteria.FilterBy(filter);
	}
}

public sealed partial class CreateSmokeAccount
{
	public string Username { get; init; } = string.Empty;

	public bool HasProblems(out Problems? problems)
	{
		return Rules.Set<CreateSmokeAccount>()
			.NotEmpty(Username)
			.HasProblems(out problems);
	}

	[Command, WithValidateModel]
	public Result<SmokeAccount> Execute()
	{
		return new SmokeAccount(Username);
	}
}

public sealed class SmokeAccount : AggregateRoot<long>
{
	private SmokeAccount()
	{
	}

	public SmokeAccount(string username)
	{
		Username = username;
		AddEvent(new SmokeAccountCreated(username));
	}

	public string Username { get; private set; } = string.Empty;
}

public sealed class SmokeAccountCreated(string username) : DomainEventBase
{
	public string Username { get; } = username;
}

public sealed class SmokeAccountFilter
{
	[Criterion]
	public string? Username { get; init; }
}

[AutoSelect<SmokeAccount>]
public sealed partial class SmokeAccountDetails
{
	[MapFrom(nameof(SmokeAccount.Username))]
	public string Name { get; set; } = string.Empty;
}
