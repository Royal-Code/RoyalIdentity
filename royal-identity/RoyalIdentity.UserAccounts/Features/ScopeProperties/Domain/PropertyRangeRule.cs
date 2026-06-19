namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Inclusive or exclusive range limits interpreted according to a property value type.
/// </summary>
public class PropertyRangeRule
{
#nullable disable
	/// <summary>
	/// Constructor for EF Core.
	/// </summary>
	protected PropertyRangeRule()
	{
	}
#nullable restore

	/// <summary>
	/// Creates a range rule.
	/// </summary>
	/// <param name="min">The optional minimum value in canonical text form.</param>
	/// <param name="max">The optional maximum value in canonical text form.</param>
	/// <param name="includeMin">Whether the minimum is inclusive.</param>
	/// <param name="includeMax">Whether the maximum is inclusive.</param>
	public PropertyRangeRule(
		string? min = null,
		string? max = null,
		bool includeMin = true,
		bool includeMax = true)
	{
		Min = min;
		Max = max;
		IncludeMin = includeMin;
		IncludeMax = includeMax;
	}

	/// <summary>
	/// Gets the optional minimum value in canonical text form.
	/// </summary>
	public string? Min { get; private set; }

	/// <summary>
	/// Gets the optional maximum value in canonical text form.
	/// </summary>
	public string? Max { get; private set; }

	/// <summary>
	/// Gets whether the minimum is inclusive.
	/// </summary>
	public bool IncludeMin { get; private set; } = true;

	/// <summary>
	/// Gets whether the maximum is inclusive.
	/// </summary>
	public bool IncludeMax { get; private set; } = true;
}
