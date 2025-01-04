using Microsoft.Extensions.Logging;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

public class ClientCredentialsValidator : IValidator<ClientCredentialsContext>
{
    private readonly ILogger logger;

    public ClientCredentialsValidator(ILogger<ClientCredentialsValidator> logger)
    {
        this.logger = logger;
    }

    public ValueTask Validate(ClientCredentialsContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
