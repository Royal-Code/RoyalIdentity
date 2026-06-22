using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Keys;

namespace Tests.Identity.Keys;

/// <summary>
/// Exercises the IdP-specific <see cref="KeyParametersFactory"/> adapter, which validates the algorithm against
/// realm <see cref="KeyOptions"/> and delegates material generation to the leaf security library.
/// </summary>
public class KeyParametersCompatibilityTests
{
	[Theory]
	[InlineData(SecurityAlgorithms.HmacSha256, 32)]
	[InlineData(SecurityAlgorithms.HmacSha384, 48)]
	[InlineData(SecurityAlgorithms.HmacSha512, 64)]
	public void Create_Hmac_Uses_Base64_Encoding_And_Algorithm_Appropriate_Key_Size(
		string algorithm,
		int expectedKeySizeInBytes)
	{
		var keyParameters = KeyParametersFactory.Create(new KeyOptions(), algorithm);

		Assert.Equal(KeyEncoding.Base64, keyParameters.Encoding);

		var symmetricKey = keyParameters.CreateSymmetricSecurityKey();
		Assert.Equal(expectedKeySizeInBytes, symmetricKey.Key.Length);

		var data = System.Text.Encoding.UTF8.GetBytes("hmac-data");
		using var signer = new SymmetricSignatureProvider(symmetricKey, algorithm);
		var signature = signer.Sign(data);

		using var verifier = new SymmetricSignatureProvider(symmetricKey, algorithm);
		Assert.True(verifier.Verify(data, signature));
	}
}
