using FixedTimeComparer = RoyalIdentity.Security.Cryptography.FixedTimeComparer;

namespace RoyalIdentity.Utils;

/// <summary>
/// Helper class to do equality checks without leaking timing information
/// </summary>
[Obsolete("Use RoyalIdentity.Security.Cryptography.FixedTimeComparer instead. This shim will be removed in Phase 7.")]
public static class TimeConstantComparer
{
    /// <summary>
    /// Checks two strings for equality without leaking timing information.
    /// </summary>
    /// <param name="s1">string 1.</param>
    /// <param name="s2">string 2.</param>
    /// <returns>
    /// 	<c>true</c> if the specified strings are equal; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsEqual(string s1, string s2) => FixedTimeComparer.IsEqualUtf8(s1, s2);
}
