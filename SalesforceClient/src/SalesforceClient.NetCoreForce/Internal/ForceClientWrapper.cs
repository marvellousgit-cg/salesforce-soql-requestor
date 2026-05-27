using System.Collections.Generic;
using System.Threading;
using NetCoreForce.Client;

namespace SalesforceClient.NetCoreForce.Internal;

/// <summary>Concrete wrapper that delegates query execution to a NetCoreForce ForceClient.</summary>
internal sealed class ForceClientWrapper : IForceClientWrapper
{
    private readonly ForceClient _client;

    internal ForceClientWrapper(ForceClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<T> QueryAsync<T>(string soql, CancellationToken cancellationToken) where T : class
        => _client.QueryAsync<T>(soql, false, null, cancellationToken);
}
