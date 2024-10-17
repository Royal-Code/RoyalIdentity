using Microsoft.AspNetCore.Authentication;

namespace RoyalIdentity.Contracts.Storage;

public interface ITokenSecureDataFormat : ISecureDataFormat<AuthenticationTicket> { }