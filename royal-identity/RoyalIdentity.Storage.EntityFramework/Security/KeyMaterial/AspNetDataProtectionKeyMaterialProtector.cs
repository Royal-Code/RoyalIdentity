using Microsoft.AspNetCore.DataProtection;

namespace RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

/// <summary>
/// Protects signing-key material with ASP.NET Core Data Protection. Production consumers must configure a
/// persistent/shared key ring and its own at-rest protection when multiple instances need to read the same
/// Configuration database.
/// </summary>
public sealed class AspNetDataProtectionKeyMaterialProtector : IKeyMaterialProtector
{
	public const string Id = "aspnet-data-protection";
	public const string Purpose = "RoyalIdentity.Storage.EntityFramework.SigningKeyMaterial.v1";

	private readonly IDataProtector protector;

	public AspNetDataProtectionKeyMaterialProtector(IDataProtectionProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		protector = provider.CreateProtector(Purpose);
	}

	public string ProtectorId => Id;

	public ValueTask<KeyMaterialEnvelope> ProtectAsync(string material, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(material);
		ct.ThrowIfCancellationRequested();
		return ValueTask.FromResult(new KeyMaterialEnvelope(ProtectorId, protector.Protect(material)));
	}

	public ValueTask<string> UnprotectAsync(KeyMaterialEnvelope envelope, CancellationToken ct = default)
	{
		ValidateEnvelope(envelope);
		ct.ThrowIfCancellationRequested();
		return ValueTask.FromResult(protector.Unprotect(envelope.Payload));
	}

	private static void ValidateEnvelope(KeyMaterialEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		if (envelope.Version != KeyMaterialEnvelope.CurrentVersion
			|| !string.Equals(envelope.ProtectorId, Id, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"The signing-key material envelope is incompatible with the ASP.NET Data Protection protector.");
		}
	}
}
