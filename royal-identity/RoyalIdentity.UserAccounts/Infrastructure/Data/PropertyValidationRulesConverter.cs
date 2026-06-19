using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

namespace RoyalIdentity.UserAccounts.Infrastructure.Data;

/// <summary>
/// Persists <see cref="PropertyValidationRules"/> as a canonical JSON string so the typed, declarative
/// rules survive a database round-trip without leaking storage concerns into the domain type.
/// </summary>
public sealed class PropertyValidationRulesConverter : ValueConverter<PropertyValidationRules, string>
{
	/// <summary>
	/// Creates the converter.
	/// </summary>
	public PropertyValidationRulesConverter()
		: base(rules => Serialize(rules), json => Deserialize(json))
	{
	}

	/// <summary>
	/// Gets a comparer that snapshots and compares rules by their canonical JSON form.
	/// </summary>
	public static ValueComparer<PropertyValidationRules> Comparer { get; } = new(
		(left, right) => Serialize(left!) == Serialize(right!),
		rules => Serialize(rules).GetHashCode(StringComparison.Ordinal),
		rules => Deserialize(Serialize(rules)));

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	internal static string Serialize(PropertyValidationRules rules)
	{
		var snapshot = new RulesSnapshot
		{
			MinLength = rules.MinLength,
			MaxLength = rules.MaxLength,
			Range = rules.Range is null
				? null
				: new RangeSnapshot
				{
					Min = rules.Range.Min,
					Max = rules.Range.Max,
					IncludeMin = rules.Range.IncludeMin,
					IncludeMax = rules.Range.IncludeMax
				},
			AllowedValues = rules.AllowedValues.Count is 0 ? null : [.. rules.AllowedValues],
			RegexPattern = rules.RegexPattern,
			MinItems = rules.MinItems,
			MaxItems = rules.MaxItems,
			CustomValidators = rules.CustomValidators.Count is 0
				? null
				: [.. rules.CustomValidators.Select(c => new CustomValidatorSnapshot
				{
					ValidatorKey = c.ValidatorKey,
					ParametersJson = c.ParametersJson
				})]
		};

		return JsonSerializer.Serialize(snapshot, SerializerOptions);
	}

	internal static PropertyValidationRules Deserialize(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return PropertyValidationRules.None;
		}

		var snapshot = JsonSerializer.Deserialize<RulesSnapshot>(json, SerializerOptions);
		if (snapshot is null)
		{
			return PropertyValidationRules.None;
		}

		var range = snapshot.Range is null
			? null
			: new PropertyRangeRule(
				snapshot.Range.Min,
				snapshot.Range.Max,
				snapshot.Range.IncludeMin,
				snapshot.Range.IncludeMax);

		var customValidators = snapshot.CustomValidators?
			.Select(c => new PropertyCustomValidationRef(c.ValidatorKey, c.ParametersJson));

		return new PropertyValidationRules(
			snapshot.MinLength,
			snapshot.MaxLength,
			range,
			snapshot.AllowedValues,
			snapshot.RegexPattern,
			snapshot.MinItems,
			snapshot.MaxItems,
			customValidators);
	}

	private sealed class RulesSnapshot
	{
		public int? MinLength { get; set; }

		public int? MaxLength { get; set; }

		public RangeSnapshot? Range { get; set; }

		public string[]? AllowedValues { get; set; }

		public string? RegexPattern { get; set; }

		public int? MinItems { get; set; }

		public int? MaxItems { get; set; }

		public CustomValidatorSnapshot[]? CustomValidators { get; set; }
	}

	private sealed class RangeSnapshot
	{
		public string? Min { get; set; }

		public string? Max { get; set; }

		public bool IncludeMin { get; set; }

		public bool IncludeMax { get; set; }
	}

	private sealed class CustomValidatorSnapshot
	{
		public string ValidatorKey { get; set; } = string.Empty;

		public string? ParametersJson { get; set; }
	}
}
