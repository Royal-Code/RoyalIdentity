using System.Security.Claims;

namespace RoyalIdentity.Utils;

/// <summary>
/// Compares two instances of Claim
/// </summary>
public class ClaimComparer : EqualityComparer<Claim>
{
    /// <summary>
    /// Claim comparison options
    /// </summary>
    public class Options
    {
        /// <summary>
        /// Specifies if the issuer value is being taken into account
        /// </summary>
        public bool IgnoreIssuerAndValueType { get; set; } = true;

        /// <summary>
        /// Specifies if claim and issuer value comparison should be case-sensitive
        /// </summary>
        public bool IgnoreValueCase { get; set; } = false;
    }

    private readonly Options options = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimComparer"/> class with default options.
    /// </summary>
    public ClaimComparer()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimComparer"/> class with given comparison options.
    /// </summary>
    /// <param name="options">Comparison options.</param>
    public ClaimComparer(Options options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public override bool Equals(Claim? x, Claim? y)
    {
        if (x == null && y == null)
            return true;
        if (x == null || y == null)
            return false;

        var valueComparison = options.IgnoreValueCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var equal = string.Equals(x.Type, y.Type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Value, y.Value, valueComparison);

        if (options.IgnoreIssuerAndValueType)
            return equal;

        return equal &&
            string.Equals(x.Issuer, y.Issuer, valueComparison) &&
            string.Equals(x.ValueType, y.ValueType, StringComparison.Ordinal);

    }

    /// <inheritdoc/>
    public override int GetHashCode(Claim claim)
    {
        if (claim is null)
            return 0;

        int typeHash = claim.Type?.ToLowerInvariant().GetHashCode() ?? 0;

        int valueHash = options.IgnoreValueCase
                ? (claim.Value?.ToLowerInvariant().GetHashCode() ?? 0)
                : (claim.Value?.GetHashCode() ?? 0);

        if (options.IgnoreIssuerAndValueType)
            return typeHash ^ valueHash;

        int issuerHash = options.IgnoreValueCase
            ? (claim.Issuer?.ToLowerInvariant().GetHashCode() ?? 0)
            : (claim.Issuer?.GetHashCode() ?? 0);

        return typeHash ^ valueHash ^ issuerHash ^ (claim.ValueType?.GetHashCode() ?? 0);
    }
}
