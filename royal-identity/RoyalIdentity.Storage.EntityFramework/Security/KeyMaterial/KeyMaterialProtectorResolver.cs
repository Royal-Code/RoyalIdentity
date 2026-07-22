namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

internal sealed class KeyMaterialProtectorResolver(IEnumerable<IKeyMaterialProtector> protectors)
{
	private readonly IReadOnlyList<IKeyMaterialProtector> protectors = protectors.ToList();

	public IKeyMaterialProtector GetForWrite()
	{
		if (protectors.Count is 0)
		{
			throw new InvalidOperationException(
				"No signing-key material protector is configured. Select Plain, ASP.NET Data Protection, AES-GCM, or a custom protector explicitly.");
		}

		if (protectors.Count is not 1)
			throw new InvalidOperationException("Exactly one signing-key material protector must be active for writes.");

		return protectors[0];
	}

	public IKeyMaterialProtector GetForRead(string protectorId)
	{
		var matches = protectors
			.Where(protector => string.Equals(protector.ProtectorId, protectorId, StringComparison.Ordinal))
			.ToList();

		return matches.Count switch
		{
			1 => matches[0],
			0 => throw new InvalidOperationException(
				"The persisted signing-key material requires a protector that is not configured."),
			_ => throw new InvalidOperationException(
				"More than one signing-key material protector is registered with the persisted identifier."),
		};
	}
}
