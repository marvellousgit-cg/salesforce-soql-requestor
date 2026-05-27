using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SalesforceClient.Abstractions;

/// <summary>
/// Executes SOQL queries against Salesforce and returns strongly typed results.
/// </summary>
public interface ISalesforceQueryClient
{
    /// <summary>
    /// Executes a SOQL query and returns ALL matching records, following pagination automatically.
    /// </summary>
    /// <typeparam name="T">The record type to deserialise into.</typeparam>
    /// <param name="soql">The SOQL query string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fully materialised read-only list of all matching records.</returns>
    Task<System.Collections.Generic.IReadOnlyList<T>> QueryAsync<T>(
        string soql,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Executes a SOQL query and streams records page by page.
    /// Use for very large result sets where you want to process records as they arrive.
    /// </summary>
    /// <typeparam name="T">The record type to deserialise into.</typeparam>
    /// <param name="soql">The SOQL query string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of records streamed across pages.</returns>
    IAsyncEnumerable<T> QueryStreamAsync<T>(
        string soql,
        CancellationToken cancellationToken = default)
        where T : class;
}
