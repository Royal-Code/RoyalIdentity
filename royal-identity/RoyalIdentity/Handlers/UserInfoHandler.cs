﻿using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;
using RoyalIdentity.Utils;
using System.Security.Claims;

namespace RoyalIdentity.Handlers;

public class UserInfoHandler : IHandler<UserInfoContext>
{
    private readonly IResourceStore resourceStore;
    private readonly IProfileService profileService;
    private readonly ILogger logger;

    public UserInfoHandler(IResourceStore resourceStore, IProfileService profileService, ILogger<UserInfoHandler> logger)
    {
        this.resourceStore = resourceStore;
        this.profileService = profileService;
        this.logger = logger;
    }

    public async Task Handle(UserInfoContext context, CancellationToken ct)
    {
        logger.LogDebug("Creating UserInfo response");

        context.AssertHasToken();

        var scopes = context.EvaluatedToken.Principal.Claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
        var resources = await resourceStore.FindResourcesByScopeAsync(scopes);
        var requestedClaims = resources.IdentityResources.SelectMany(r => r.UserClaims);

        var request = new ProfileDataRequest(
            context,
            resources,
            context.EvaluatedToken.Principal,
            context.EvaluatedToken.Client,
            nameof(UserInfoHandler),
            requestedClaims);

        await profileService.GetProfileDataAsync(request, ct);

        var profileClaims = request.IssuedClaims;

        // construct outgoing claims
        var outgoingClaims = new HashSet<Claim>(new ClaimComparer());

        if (profileClaims.Count is 0)
        {
            logger.LogDebug("Profile service returned no claims");
        }
        else
        {
            outgoingClaims.AddRange(profileClaims);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Profile service returned the following claim types: {Types}",
                    profileClaims.Select(c => c.Type).ToSpaceSeparatedString());
            }
        }

        var subClaim = outgoingClaims.SingleOrDefault(x => x.Type == JwtClaimTypes.Subject);
        if (subClaim is null)
        {
            outgoingClaims.Add(new Claim(JwtClaimTypes.Subject, context.EvaluatedToken.Principal.GetSubjectId()));
        }
        else if (subClaim.Value != context.EvaluatedToken.Principal.GetSubjectId())
        {
            logger.LogError(context, $"Profile service returned incorrect subject value: {subClaim}");

            throw new InvalidOperationException("Profile service returned incorrect subject value");
        }

        var userData = outgoingClaims.ToClaimsDictionary();
        context.Response = new UserInfoResponse(userData);
    }
}