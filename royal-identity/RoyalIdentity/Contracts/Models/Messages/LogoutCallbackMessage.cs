﻿using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Contracts.Models.Messages;

public class LogoutCallbackMessage
{
    public string? SessionId { get; set; }

    public string? PostLogoutRedirectUri { get; set; }

    public string? State { get; set; }

    public HashSet<string>? FrontChannelLogout { get; set; }

    public HashSet<LogoutBackChannelMessage>? BackChannelLogout { get; set; }

    public bool AutomaticRedirectAfterSignOut { get; set; }

    public string SignOutIframeUrl { get; set; } = ProtocolRoutePaths.EndSessionCallback;

    public bool HasChannel()
    {
        return (FrontChannelLogout is not null && FrontChannelLogout.Any()) ||
            (BackChannelLogout is not null && BackChannelLogout.Any());
    }
}