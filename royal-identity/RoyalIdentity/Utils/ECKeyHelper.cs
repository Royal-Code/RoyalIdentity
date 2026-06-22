using SecurityECKeyHelper = RoyalIdentity.Security.Keys.ECKeyHelper;

namespace RoyalIdentity.Utils;

/// <summary>
/// Delegate wrapper kept for backward compatibility until Phase 7.
/// All members delegate to <see cref="SecurityECKeyHelper"/>.
/// </summary>
[Obsolete("Use RoyalIdentity.Security.Keys.ECKeyHelper instead. This shim will be removed in Phase 7.")]
public static class ECKeyHelper
{
	/// <summary>
	/// Exports the ECParameters to an XML string.
	/// </summary>
	/// <param name="ecdsa">The ECDsa instance from which to export the parameters.</param>
	/// <param name="includePrivateParameters">Whether to include private parameters.</param>
	/// <returns>The XML string representing the ECParameters.</returns>
	public static string ExportECParametersToXml(System.Security.Cryptography.ECDsa ecdsa, bool includePrivateParameters)
		=> SecurityECKeyHelper.ExportECParametersToXml(ecdsa, includePrivateParameters);

	/// <summary>
	/// Imports the ECParameters from an XML string.
	/// </summary>
	/// <param name="xmlString">The XML string representing the ECParameters.</param>
	/// <returns>The imported ECParameters.</returns>
	public static System.Security.Cryptography.ECParameters ImportECParametersFromXml(string xmlString)
		=> SecurityECKeyHelper.ImportECParametersFromXml(xmlString);

	/// <summary>
	/// Creates an ECDsaSecurityKey from the ECParameters XML.
	/// </summary>
	/// <param name="xmlString">The XML string representing the ECParameters.</param>
	/// <returns>An ECDsaSecurityKey based on the imported parameters.</returns>
	public static Microsoft.IdentityModel.Tokens.ECDsaSecurityKey CreateECDsaSecurityKeyFromXml(string xmlString)
		=> SecurityECKeyHelper.CreateECDsaSecurityKeyFromXml(xmlString);

	public static string SerializeECParameters(System.Security.Cryptography.ECParameters ecParameters)
		=> SecurityECKeyHelper.SerializeECParameters(ecParameters);

	public static System.Security.Cryptography.ECParameters DeserializeECParameters(string json)
		=> SecurityECKeyHelper.DeserializeECParameters(json);
}
