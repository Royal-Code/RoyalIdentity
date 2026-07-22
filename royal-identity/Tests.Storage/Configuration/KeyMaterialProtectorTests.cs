using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

namespace Tests.Storage.Configuration;

public class KeyMaterialProtectorTests
{
	private const string Material = "private-key-material-that-must-not-appear-in-diagnostics";
	private static readonly byte[] AesKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

	[Fact]
	public async Task Plain_RoundTrips_OnlyAfterExplicitConstruction_AndWarnsWithoutMaterial()
	{
		var logger = new CaptureLogger<PlainKeyMaterialProtector>();
		var protector = new PlainKeyMaterialProtector(logger);

		var envelope = await protector.ProtectAsync(Material);
		var restored = await protector.UnprotectAsync(envelope);

		Assert.Equal(Material, restored);
		var warning = Assert.Single(logger.Messages);
		Assert.Contains("not encrypted", warning, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain(Material, warning, StringComparison.Ordinal);
		Assert.DoesNotContain(Material, envelope.ToString(), StringComparison.Ordinal);
	}

	[Fact]
	public async Task AesGcm_UsesRandomNonce_AndRejectsTamperedCiphertext()
	{
		using var protector = CreateAesProtector();

		var first = await protector.ProtectAsync(Material);
		var second = await protector.ProtectAsync(Material);

		Assert.NotEqual(first.Payload, second.Payload);
		Assert.Equal(Material, await protector.UnprotectAsync(first));
		Assert.Equal(Material, await protector.UnprotectAsync(second));

		var tampered = Convert.FromBase64String(first.Payload);
		tampered[^1] ^= 0x01;
		var tamperedEnvelope = new KeyMaterialEnvelope(first.ProtectorId, Convert.ToBase64String(tampered));
		await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
			() => protector.UnprotectAsync(tamperedEnvelope).AsTask());
	}

	[Theory]
	[InlineData(0)]
	[InlineData(15)]
	[InlineData(17)]
	[InlineData(31)]
	[InlineData(33)]
	public void AesGcm_RejectsInvalidKeyLength(int length)
	{
		var options = Options.Create(new AesKeyMaterialProtectorOptions { Key = new byte[length] });

		var exception = Assert.Throws<InvalidOperationException>(() => new AesKeyMaterialProtector(options));

		Assert.Equal("The AES-GCM signing-key protector requires a 16, 24 or 32 byte key.", exception.Message);
	}

	[Fact]
	public async Task DataProtection_RoundTripsAcrossProtectorInstancesSharingProvider_AndRejectsAnotherProvider()
	{
		var sharedProvider = new EphemeralDataProtectionProvider();
		var writer = new AspNetDataProtectionKeyMaterialProtector(sharedProvider);
		var reader = new AspNetDataProtectionKeyMaterialProtector(sharedProvider);
		var incompatible = new AspNetDataProtectionKeyMaterialProtector(new EphemeralDataProtectionProvider());

		var envelope = await writer.ProtectAsync(Material);

		Assert.Equal(Material, await reader.UnprotectAsync(envelope));
		await Assert.ThrowsAsync<CryptographicException>(
			() => incompatible.UnprotectAsync(envelope).AsTask());
	}

	[Fact]
	public void Envelope_RejectsUnsupportedVersion_WithoutEchoingPayload()
	{
		var exception = Assert.Throws<InvalidOperationException>(
			() => KeyMaterialEnvelope.Parse(AesKeyMaterialProtector.Id, $"v2:{Material}"));

		Assert.DoesNotContain(Material, exception.Message, StringComparison.Ordinal);
	}

	private static AesKeyMaterialProtector CreateAesProtector()
		=> new(Options.Create(new AesKeyMaterialProtectorOptions { Key = AesKey }));

	private sealed class CaptureLogger<T> : ILogger<T>
	{
		public List<string> Messages { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (logLevel >= LogLevel.Warning)
				Messages.Add(formatter(state, exception));
		}
	}
}
