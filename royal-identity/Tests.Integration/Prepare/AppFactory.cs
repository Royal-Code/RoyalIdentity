using Microsoft.AspNetCore.Mvc.Testing;

#pragma warning disable S1118 // Utility classes should not have public constructors

namespace Tests.Integration.Prepare;

public class AppFactory : WebApplicationFactory<Program> { }