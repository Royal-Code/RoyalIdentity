using System.Globalization;
using System.Text.RegularExpressions;
using RoyalCode.SmartProblems;
using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Features.ScopeProperties.Domain;

/// <summary>
/// Validates and assigns dynamic property values to accounts.
/// </summary>
public class UserAccountPropertyValueService
{
	private readonly IReadOnlyDictionary<string, IUserAccountPropertyValidator> customValidators;

	/// <summary>
	/// Creates a value service.
	/// </summary>
	/// <param name="customValidators">Custom validators keyed by <see cref="IUserAccountPropertyValidator.Key"/>.</param>
	public UserAccountPropertyValueService(IEnumerable<IUserAccountPropertyValidator>? customValidators = null)
	{
		this.customValidators = (customValidators ?? [])
			.GroupBy(v => v.Key, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
	}

	/// <summary>
	/// Validates and replaces values for one property definition.
	/// </summary>
	/// <param name="account">The target account.</param>
	/// <param name="definitionVersion">The active definition version.</param>
	/// <param name="values">The raw values to assign.</param>
	/// <param name="changedAt">The mutation timestamp.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A result describing whether assignment succeeded.</returns>
	public async ValueTask<Result> SetValuesAsync(
		UserAccount account,
		PropertyDefinitionVersion definitionVersion,
		IEnumerable<string> values,
		DateTimeOffset changedAt,
		CancellationToken cancellationToken = default)
	{
		var definition = definitionVersion.PropertyDefinition;
		if (definition is null)
		{
			return Problems.InvalidState("Property definition is missing.", nameof(definitionVersion), "user_account.property_definition_missing");
		}

		var rawValues = values.ToArray();
		var canonicalValues = new List<string>(rawValues.Length);

		var result = await ValidateValuesAsync(account, definitionVersion, rawValues, canonicalValues, cancellationToken);
		if (result.IsFailure)
		{
			return result;
		}

		return account.ReplacePropertyValues(
			definition,
			definitionVersion.ValueType,
			canonicalValues,
			changedAt);
	}

	private async ValueTask<Result> ValidateValuesAsync(
		UserAccount account,
		PropertyDefinitionVersion definitionVersion,
		IReadOnlyCollection<string> rawValues,
		ICollection<string> canonicalValues,
		CancellationToken cancellationToken)
	{
		if (account.RealmId != definitionVersion.RealmId)
		{
			return Problems.InvalidState("Property definition realm does not match account realm.", nameof(definitionVersion), "user_account.property_realm_mismatch");
		}

		if (!definitionVersion.IsActive)
		{
			return Problems.InvalidState("Property definition is inactive.", nameof(definitionVersion), "user_account.property_inactive");
		}

		if (definitionVersion.PropertyScopeVersion?.Status is not PropertyScopeVersionStatus.Active)
		{
			return Problems.InvalidState("Property definition version is not active.", nameof(definitionVersion), "user_account.property_version_inactive");
		}

		if (definitionVersion.PropertyScopeVersion.PropertyScope?.IsActive is false)
		{
			return Problems.InvalidState("Property scope is inactive.", nameof(definitionVersion), "user_account.property_scope_inactive");
		}

		if (definitionVersion.IsRequired && rawValues.Count is 0)
		{
			return Problems.InvalidParameter("Property value is required.", nameof(rawValues), "user_account.property_required");
		}

		if (!definitionVersion.IsCollection && rawValues.Count > 1)
		{
			return Problems.InvalidParameter("Property accepts a single value.", nameof(rawValues), "user_account.property_single_value");
		}

		var rules = definitionVersion.ValidationRules;
		if (rules.MinItems is not null && rawValues.Count < rules.MinItems)
		{
			return Problems.InvalidParameter("Property has fewer values than allowed.", nameof(rawValues), "user_account.property_min_items");
		}

		if (rules.MaxItems is not null && rawValues.Count > rules.MaxItems)
		{
			return Problems.InvalidParameter("Property has more values than allowed.", nameof(rawValues), "user_account.property_max_items");
		}

		foreach (var rawValue in rawValues)
		{
			var parseResult = TryParse(definitionVersion.ValueType, rawValue, out var parsedValue, out var canonicalValue);
			if (!parseResult)
			{
				return Problems.InvalidParameter("Property value does not match the configured value type.", nameof(rawValues), "user_account.property_value_type");
			}

			var declarativeResult = ValidateDeclarativeRules(definitionVersion, rawValue, parsedValue, canonicalValue);
			if (declarativeResult.IsFailure)
			{
				return declarativeResult;
			}

			foreach (var customValidatorRef in rules.CustomValidators)
			{
				if (!customValidators.TryGetValue(customValidatorRef.ValidatorKey, out var customValidator))
				{
					return Problems.InvalidState("Property custom validator is not registered.", nameof(customValidatorRef.ValidatorKey), "user_account.property_validator_missing");
				}

				var context = new UserAccountPropertyValidationContext
				{
					RealmId = account.RealmId,
					SubjectId = account.SubjectId,
					ClaimType = definitionVersion.ClaimType,
					ValueType = definitionVersion.ValueType,
					RawValue = rawValue,
					CanonicalValue = canonicalValue,
					ParsedValue = parsedValue,
					ParametersJson = customValidatorRef.ParametersJson
				};

				var customResult = await customValidator.ValidateAsync(context, cancellationToken);
				if (customResult.IsFailure)
				{
					return customResult;
				}
			}

			canonicalValues.Add(canonicalValue);
		}

		return Result.Ok();
	}

	private static Result ValidateDeclarativeRules(
		PropertyDefinitionVersion definitionVersion,
		string rawValue,
		object parsedValue,
		string canonicalValue)
	{
		var rules = definitionVersion.ValidationRules;

		if (definitionVersion.ValueType is PropertyValueType.Text)
		{
			if (rules.MinLength is not null && rawValue.Length < rules.MinLength)
			{
				return Problems.InvalidParameter("Property value is shorter than allowed.", nameof(rawValue), "user_account.property_min_length");
			}

			if (rules.MaxLength is not null && rawValue.Length > rules.MaxLength)
			{
				return Problems.InvalidParameter("Property value is longer than allowed.", nameof(rawValue), "user_account.property_max_length");
			}

			if (!string.IsNullOrWhiteSpace(rules.RegexPattern) && !Regex.IsMatch(rawValue, rules.RegexPattern))
			{
				return Problems.InvalidParameter("Property value does not match the configured pattern.", nameof(rawValue), "user_account.property_regex");
			}
		}
		else if (!string.IsNullOrWhiteSpace(rules.RegexPattern))
		{
			return Problems.InvalidState("Regex validation is only supported for text properties.", nameof(rules.RegexPattern), "user_account.property_regex_type_mismatch");
		}

		if (rules.Range is not null)
		{
			var rangeResult = ValidateRange(definitionVersion.ValueType, parsedValue, rules.Range);
			if (rangeResult.IsFailure)
			{
				return rangeResult;
			}
		}

		if (rules.AllowedValues.Count > 0)
		{
			var allowedValues = new HashSet<string>(StringComparer.Ordinal);
			foreach (var allowedValue in rules.AllowedValues)
			{
				if (!TryParse(definitionVersion.ValueType, allowedValue, out _, out var canonicalAllowedValue))
				{
					return Problems.InvalidState("Allowed property value does not match the configured value type.", nameof(rules.AllowedValues), "user_account.property_allowed_value_type");
				}

				allowedValues.Add(canonicalAllowedValue);
			}

			if (!allowedValues.Contains(canonicalValue))
			{
				return Problems.InvalidParameter("Property value is not allowed.", nameof(rawValue), "user_account.property_allowed_values");
			}
		}

		return Result.Ok();
	}

	private static Result ValidateRange(PropertyValueType valueType, object parsedValue, PropertyRangeRule range)
	{
		if (valueType is PropertyValueType.Text or PropertyValueType.Boolean)
		{
			return Problems.InvalidState("Range validation is not supported for this value type.", nameof(range), "user_account.property_range_type_mismatch");
		}

		if (!string.IsNullOrWhiteSpace(range.Min))
		{
			if (!TryParse(valueType, range.Min, out var parsedMin, out _))
			{
				return Problems.InvalidState("Minimum range value does not match the configured value type.", nameof(range.Min), "user_account.property_range_min_type");
			}

			var comparison = CompareParsed(valueType, parsedValue, parsedMin);
			if (comparison < 0 || (!range.IncludeMin && comparison == 0))
			{
				return Problems.InvalidParameter("Property value is below the configured minimum.", nameof(parsedValue), "user_account.property_range_min");
			}
		}

		if (!string.IsNullOrWhiteSpace(range.Max))
		{
			if (!TryParse(valueType, range.Max, out var parsedMax, out _))
			{
				return Problems.InvalidState("Maximum range value does not match the configured value type.", nameof(range.Max), "user_account.property_range_max_type");
			}

			var comparison = CompareParsed(valueType, parsedValue, parsedMax);
			if (comparison > 0 || (!range.IncludeMax && comparison == 0))
			{
				return Problems.InvalidParameter("Property value is above the configured maximum.", nameof(parsedValue), "user_account.property_range_max");
			}
		}

		return Result.Ok();
	}

	private static bool TryParse(
		PropertyValueType valueType,
		string rawValue,
		out object parsedValue,
		out string canonicalValue)
	{
		switch (valueType)
		{
			case PropertyValueType.Text:
				parsedValue = rawValue;
				canonicalValue = rawValue;
				return true;

			case PropertyValueType.Integer:
				if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
				{
					parsedValue = integer;
					canonicalValue = integer.ToString(CultureInfo.InvariantCulture);
					return true;
				}

				break;

			case PropertyValueType.Decimal:
				if (decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
				{
					parsedValue = number;
					canonicalValue = number.ToString(CultureInfo.InvariantCulture);
					return true;
				}

				break;

			case PropertyValueType.Boolean:
				if (bool.TryParse(rawValue, out var boolean))
				{
					parsedValue = boolean;
					canonicalValue = boolean ? "true" : "false";
					return true;
				}

				break;

			case PropertyValueType.Date:
				if (DateOnly.TryParseExact(rawValue, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
				{
					parsedValue = date;
					canonicalValue = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
					return true;
				}

				break;

			case PropertyValueType.DateTime:
				if (DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
				{
					parsedValue = dateTime;
					canonicalValue = dateTime.ToString("O", CultureInfo.InvariantCulture);
					return true;
				}

				break;

			case PropertyValueType.Time:
				if (TimeOnly.TryParseExact(rawValue, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
				{
					parsedValue = time;
					canonicalValue = time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
					return true;
				}

				break;
		}

		parsedValue = string.Empty;
		canonicalValue = string.Empty;
		return false;
	}

	private static int CompareParsed(PropertyValueType valueType, object left, object right)
	{
		return valueType switch
		{
			PropertyValueType.Integer => ((long)left).CompareTo((long)right),
			PropertyValueType.Decimal => ((decimal)left).CompareTo((decimal)right),
			PropertyValueType.Date => ((DateOnly)left).CompareTo((DateOnly)right),
			PropertyValueType.DateTime => ((DateTimeOffset)left).CompareTo((DateTimeOffset)right),
			PropertyValueType.Time => ((TimeOnly)left).CompareTo((TimeOnly)right),
			_ => 0
		};
	}
}
