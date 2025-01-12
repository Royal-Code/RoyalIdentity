using RoyalIdentity.Contexts.Parameters;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithClient : IEndpointContextBase
{
    public bool IsClientRequired { get; }

    public string? ClientId { get; }

    public ClientParameters ClientParameters {  get; }
}
