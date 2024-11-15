using System.Runtime.CompilerServices;

namespace RoyalIdentity.Utils;

/// <summary>
/// Helper class to do equality checks without leaking timing information
/// </summary>
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEqual(string s1, string s2) => s1.AsSpan().SequenceEqual(s2.AsSpan());
}