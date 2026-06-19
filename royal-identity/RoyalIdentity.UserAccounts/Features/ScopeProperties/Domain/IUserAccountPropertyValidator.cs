using RoyalCode.SmartProblems;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Custom validator for dynamic user account property values.
/// </summary>
public interface IUserAccountPropertyValidator
{
	/// <summary>
	/// Gets the key used by property validation rules to select this validator.
	/// </summary>
	string Key { get; }

	/// <summary>
	/// Validates a property value.
	/// </summary>
	/// <param name="context">The validation context.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A result describing whether the value is valid.</returns>
	ValueTask<Result> ValidateAsync(
		UserAccountPropertyValidationContext context,
		CancellationToken cancellationToken);
}
