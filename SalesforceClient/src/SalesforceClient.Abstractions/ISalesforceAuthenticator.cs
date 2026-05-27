using System.Threading;

namespace SalesforceClient.Abstractions;

/// <summary>
/// Provides Salesforce access tokens, handling caching and refresh transparently.
/// </summary>
public interface ISalesforceAuthenticator
{
    /// <summary>Returns a valid access token, refreshing if necessary.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid OAuth access token.</returns>
    System.Threading.Tasks.ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the Salesforce instance URL obtained during authentication.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The instance URL, e.g. https://myorg.my.salesforce.com</returns>
    System.Threading.Tasks.ValueTask<string> GetInstanceUrlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached token so the next call to <see cref="GetAccessTokenAsync"/>
    /// forces a fresh token request. Call this when a 401 is received from Salesforce.
    /// </summary>
    void InvalidateCachedToken();
}
