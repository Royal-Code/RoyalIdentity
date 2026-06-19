namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Reference to a custom property validator registered in the module.
/// </summary>
public class PropertyCustomValidationRef
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PropertyCustomValidationRef()
	{
	}
#nullable restore

	/// <summary>
	/// Creates a custom validator reference.
	/// </summary>
	/// <param name="validatorKey">The registered validator key.</param>
	/// <param name="parametersJson">Optional validator parameters encoded as JSON.</param>
	public PropertyCustomValidationRef(string validatorKey, string? parametersJson = null)
	{
		ValidatorKey = validatorKey;
		ParametersJson = parametersJson;
	}

	/// <summary>
	/// Gets the registered validator key.
	/// </summary>
	public string ValidatorKey { get; private set; } = string.Empty;

	/// <summary>
	/// Gets optional validator parameters encoded as JSON.
	/// </summary>
	public string? ParametersJson { get; private set; }
}
