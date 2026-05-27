using System.Collections.Generic;
using System.Threading;

namespace SalesforceClient.NetCoreForce.Internal;

/// <summary>
/// Thin abstraction over ForceClient query methods, enabling unit testing without
/// a real Salesforce connection and without referencing NetCoreForce from test projects.
/// </summary>
internal interface IForceClientWrapper
{
    /// <summary>Executes a SOQL query and streams all results, handling pagination internally.</summary>
    IAsyncEnumerable<T> QueryAsync<T>(string soql, CancellationToken cancellationToken) where T : class;
}
