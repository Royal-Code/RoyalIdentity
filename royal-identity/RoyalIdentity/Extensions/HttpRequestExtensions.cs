// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace RoyalIdentity.Extensions;

internal static class HttpRequestExtensions
{
    public static string? GetCorsOrigin(this HttpRequest request)
    {
        var origin = request.Headers.Origin.FirstOrDefault();
        var thisOrigin = request.Scheme + "://" + request.Host;

        // see if the Origin is different than this server's origin. if so
        // that indicates a proper CORS request. some browsers send Origin
        // on POST requests.
        if (origin != null && origin != thisOrigin)
        {
            return origin;
        }

        return null;
    }

    internal static bool HasApplicationFormContentType(this HttpRequest request)
    {
        if (request.ContentType is null) return false;

        if (MediaTypeHeaderValue.TryParse(request.ContentType, out var header))
        {
            // Content-Type: application/x-www-form-urlencoded; charset=utf-8
            return header.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
