// Ignore Spelling: Jwk

using Microsoft.AspNetCore.Http;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts;

public class JwkContext(
    HttpContext httpContext,
    ContextItems? items = null) 
    : EndpointContextBase(httpContext, [], items)
{ }
