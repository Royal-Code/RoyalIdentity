using RoyalIdentity.Users.Defaults;

namespace Tests.Identity.Users;

public class DefaultPasswordProtectorTests
{
	[Fact]
	public async Task VerifyPasswordAsync_Returns_True_For_Hash_Created_By_Protector()
	{
		var protector = new DefaultPasswordProtector();
		var hash = await protector.HashPasswordAsync("passw0rd");

		Assert.True(await protector.VerifyPasswordAsync("passw0rd", hash));
	}

	[Theory]
	[InlineData("")]
	[InlineData("not-a-hash")]
	[InlineData("$PBKDF2$.only-one-part")]
	[InlineData("$RIPWD$1$PBKDF2-SHA256$notanumber$AAAA$BBBB")]
	public async Task VerifyPasswordAsync_Returns_False_For_Malformed_Or_Unsupported_Hash(string hash)
	{
		var protector = new DefaultPasswordProtector();

		Assert.False(await protector.VerifyPasswordAsync("passw0rd", hash));
	}
}
