using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// Materializes JSON values declared as <see cref="object"/> into their natural CLR representation instead
/// of leaving them as <see cref="JsonElement"/>. This is required by discovery custom entries, whose runtime
/// behavior distinguishes strings (including relative <c>~/</c> paths) from other JSON values.
/// </summary>
internal sealed class ConfigurationObjectJsonConverter : JsonConverter<object>
{
	public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using var document = JsonDocument.ParseValue(ref reader);
		return Materialize(document.RootElement);
	}

	public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(value);

		var runtimeType = value.GetType();
		if (runtimeType == typeof(object))
			throw new JsonException("A configuration JSON value must have a supported runtime type.");

		JsonSerializer.Serialize(writer, value, runtimeType, options);
	}

	private static object? Materialize(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.Null => null,
		JsonValueKind.String => element.GetString(),
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.Number => MaterializeNumber(element),
		JsonValueKind.Array => element.EnumerateArray().Select(Materialize).ToList(),
		JsonValueKind.Object => element.EnumerateObject().ToDictionary(
			property => property.Name,
			property => Materialize(property.Value),
			StringComparer.Ordinal),
		_ => throw new JsonException($"Unsupported configuration JSON value kind '{element.ValueKind}'."),
	};

	private static object MaterializeNumber(JsonElement element)
	{
		if (element.TryGetInt32(out var int32))
			return int32;

		if (element.TryGetInt64(out var int64))
			return int64;

		if (element.TryGetUInt64(out var uint64))
			return uint64;

		if (element.TryGetDecimal(out var decimalValue))
			return decimalValue;

		return element.GetDouble();
	}
}
