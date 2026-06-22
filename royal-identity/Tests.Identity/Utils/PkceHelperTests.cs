using RoyalIdentity.Utils;

namespace Tests.Identity.Utils;

public class PkceHelperTests
{
	[Fact]
	public void GenerateS256CodeChallenge_Matches_Rfc7636_Vector()
	{
		const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

		var challenge = PkceHelper.GenerateS256CodeChallenge(verifier);

		Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
	}

	[Fact]
	public void GenerateStoredS256CodeChallengeHash_Hashes_The_Rfc7636_Challenge_For_Storage()
	{
		const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
		const string challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

		var stored = PkceHelper.GenerateStoredS256CodeChallengeHash(verifier);

		Assert.Equal(PkceHelper.HashCodeChallengeForStorage(challenge), stored);
	}
}
