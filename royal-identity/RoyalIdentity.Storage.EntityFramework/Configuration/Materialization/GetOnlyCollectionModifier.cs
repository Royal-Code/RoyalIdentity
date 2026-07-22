using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// <para>
///     A <see cref="DefaultJsonTypeInfoResolver"/> modifier that makes get-only collection properties fully
///     round-trip. By default System.Text.Json skips read-only properties on deserialization, so an options
///     graph would silently keep the constructor's default items (e.g. the full set of signing algorithms)
///     regardless of what was persisted — a fidelity/security hazard.
/// </para>
/// <para>
///     For each get-only property whose type is a mutable collection, this installs a setter with
///     clear-then-add semantics: it empties the instance the constructor created (preserving its custom
///     comparer, such as case-insensitive CORS origins) and copies exactly the persisted items. Serialization
///     is unaffected. A persisted null or a missing target instance is rejected instead of silently retaining
///     constructor defaults. The result is a stable, faithful round-trip that honours removals as well as additions.
/// </para>
/// </summary>
internal static class GetOnlyCollectionModifier
{
	public static void Apply(JsonTypeInfo typeInfo)
	{
		foreach (var property in typeInfo.Properties)
		{
			if (property.Set is not null || property.Get is null)
				continue;

			var collectionInterface = FindCollectionInterface(property.PropertyType);
			if (collectionInterface is null)
				continue;

			var clear = collectionInterface.GetMethod("Clear")!;
			var add = collectionInterface.GetMethod("Add")!;
			var getter = property.Get;

			property.Set = (target, value) =>
			{
				if (value is null)
					throw new JsonException($"The configuration collection '{property.Name}' cannot be null.");

				var existing = getter(target);
				if (existing is null)
					throw new JsonException($"The configuration collection '{property.Name}' has no target instance.");

				clear.Invoke(existing, null);
				foreach (var item in (IEnumerable)value)
					add.Invoke(existing, [item]);
			};
		}
	}

	private static Type? FindCollectionInterface(Type type)
	{
		if (type == typeof(string))
			return null;

		return type
			.GetInterfaces()
			.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
	}
}
