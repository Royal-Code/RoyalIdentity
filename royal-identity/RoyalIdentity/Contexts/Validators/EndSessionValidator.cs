using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

public class EndSessionValidator : IValidator<EndSessionContext>
{
    public ValueTask Validate(EndSessionContext context, CancellationToken ct)
    {
        // validate subject of IdToken and subject of authenticated user
        if (context.IdToken is not null && context.IsAuthenticated && 
            context.IdToken.Principal.GetSubjectId() != context.Principal.GetSubjectId())
        {
            context.InvalidRequest("Invalid subject in id_token_hint.");
            return default;
        }

        // validate client_id in IdToken and client_id in request
        if (context.IdToken is not null && context.Client is not null && 
            context.IdToken.Client.Id != context.Client.Id)
        {
            context.InvalidRequest("Invalid client_id in id_token_hint.");
            return default;
        }

        // validate post_logout_redirect_uri
        if (context.PostLogoutRedirectUri.IsPresent())
        {
            // if client is not informed, then the request is invalid
            if (context.Client is null)
            {
                context.InvalidRequest("Client is not informed.");
                return default;
            }

            // if post_logout_redirect_uri is not in client's list of post_logout_redirect_uris, then the request is invalid
            if (!context.Client.PostLogoutRedirectUris.Contains(context.PostLogoutRedirectUri))
            {
                context.InvalidRequest("Invalid post_logout_redirect_uri.");
                return default;
            }
        }

        // validate logout_hint as subject_id if present
        if (context.LogoutHint.IsPresent() && context.LogoutHint != context.Principal.GetSubjectId())
        {
            context.InvalidRequest("Invalid logout_hint.");
            return default;
        }

        return default;
    }
}
