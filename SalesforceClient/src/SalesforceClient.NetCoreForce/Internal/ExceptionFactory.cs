using System.Collections.Generic;
using System.Net;
using NetCoreForce.Client;
using NetCoreForce.Client.Models;

namespace SalesforceClient.NetCoreForce.Internal;

/// <summary>
/// Creates NetCoreForce-specific exceptions without requiring test projects to reference
/// NetCoreForce.Client directly.
/// </summary>
internal static class ExceptionFactory
{
    /// <summary>Creates a <see cref="ForceApiException"/> simulating an HTTP 401 response.</summary>
    internal static ForceApiException CreateUnauthorizedException()
        => new("Simulated 401 Unauthorized", new List<ErrorResponse>(), HttpStatusCode.Unauthorized);
}
