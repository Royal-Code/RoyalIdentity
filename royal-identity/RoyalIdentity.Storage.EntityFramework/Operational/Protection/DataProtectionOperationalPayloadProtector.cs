using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// Operational payload profile backed by ASP.NET Core Data Protection. The
/// <see cref="OperationalProtectionContext"/> is bound as a purpose chain, which is how Data Protection
/// expresses authenticated context. Production consumers must configure a persistent, shared key ring — and
/// its own at-rest protection — when more than one instance reads the same operational database.
/// </summary>
public sealed class DataProtectionOperationalPayloadProtector : IOperationalPayloadProtector
{
	public const string Purpose = "RoyalIdentity.Storage.EntityFramework.OperationalPayload.v1";

	private readonly IDataProtectionProvider provider;

	public DataProtectionOperationalPayloadProtector(string profileId, IDataProtectionProvider provider)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
		ArgumentNullException.ThrowIfNull(provider);

		ProfileId = profileId;
		this.provider = provider;
	}

	public string ProfileId { get; }

	public ValueTask<string> ProtectAsync(
		string payload, OperationalProtectionContext context, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(payload);
		ArgumentNullException.ThrowIfNull(context);
		ct.ThrowIfCancellationRequested();

		return ValueTask.FromResult(CreateProtector(context).Protect(payload));
	}

	public ValueTask<string> UnprotectAsync(
		string protectedPayload, OperationalProtectionContext context, CancellationToken ct = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(protectedPayload);
		ArgumentNullException.ThrowIfNull(context);
		ct.ThrowIfCancellationRequested();

		try
		{
			return ValueTask.FromResult(CreateProtector(context).Unprotect(protectedPayload));
		}
		catch (CryptographicException exception)
		{
			throw OperationalPayloadProtectionException.Unreadable(ProfileId, exception);
		}
	}

	private IDataProtector CreateProtector(OperationalProtectionContext context)
		=> provider.CreateProtector(Purpose, context.ToPurposeChain());
}
