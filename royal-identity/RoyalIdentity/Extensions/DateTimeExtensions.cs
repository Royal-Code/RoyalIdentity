// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using System.Diagnostics;

namespace RoyalIdentity.Extensions;

internal static class DateTimeExtensions
{
    [DebuggerStepThrough]
    public static bool HasExceeded(this DateTime creationTime, int seconds, DateTime now)
    {
        return now > creationTime.AddSeconds(seconds);
    }

    [DebuggerStepThrough]
    public static bool HasExceeded(this DateTime? date, TimeSpan time, DateTime now)
    {
        return date.HasValue && now > date.Value.Add(time);
    }

    [DebuggerStepThrough]
    public static int GetLifetimeInSeconds(this DateTime creationTime, DateTime now)
    {
        return (int)(now - creationTime).TotalSeconds;
    }

    [DebuggerStepThrough]
    public static bool HasExpired(this DateTime? expirationTime, DateTime now)
    {
        if (expirationTime.HasValue &&
            expirationTime.Value.HasExpired(now))
        {
            return true;
        }

        return false;
    }

    [DebuggerStepThrough]
    public static bool HasExpired(this DateTime expirationTime, DateTime now)
    {
        if (now > expirationTime)
        {
            return true;
        }

        return false;
    }
}
