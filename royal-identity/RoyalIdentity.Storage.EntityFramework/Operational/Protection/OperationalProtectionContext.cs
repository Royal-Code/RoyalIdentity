using System.Text;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Protection;

/// <summary>
/// The context a protected operational payload is bound to (plan DF30): realm, record type, lookup key and
/// payload version. Profiles that support authenticated data bind it, so a payload cannot be replayed into
/// another realm, another record type, another row or another payload version.
/// </summary>
/// <param name="RealmId">The realm that owns the record.</param>
/// <param name="RecordType">The record type; see <see cref="Materialization.OperationalRecordTypes"/>.</param>
/// <param name="LookupKey">The persisted lookup key of the row — a digest, never a raw handle (plan DF38).</param>
/// <param name="PayloadVersion">The version of the payload being protected.</param>
public sealed record OperationalProtectionContext(
    string RealmId,
    string RecordType,
    string LookupKey,
    int PayloadVersion)
{
    private const string Domain = "RoyalIdentity.Operational.Payload.v1";

    /// <summary>
    /// The canonical byte encoding used as associated data. Each part is length-prefixed, so no combination of
    /// values can produce the encoding of a different combination.
    /// </summary>
    public byte[] ToAssociatedData()
    {
        var builder = new StringBuilder(Domain);
        Append(builder, RealmId);
        Append(builder, RecordType);
        Append(builder, LookupKey);
        Append(builder, PayloadVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>The same parts as purpose chain entries, for profiles that bind context by purpose.</summary>
    public string[] ToPurposeChain() =>
        [RealmId, RecordType, PayloadVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), LookupKey];

    private static void Append(StringBuilder builder, string value)
        => builder.Append('|').Append(value.Length).Append(':').Append(value);
}
