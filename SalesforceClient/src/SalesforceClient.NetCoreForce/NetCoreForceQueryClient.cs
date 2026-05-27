using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCoreForce.Client;
using NetCoreForce.Client.Models;
using SalesforceClient.Abstractions;
using SalesforceClient.Abstractions.Options;
using SalesforceClient.NetCoreForce.Internal;

namespace SalesforceClient.NetCoreForce;

/// <summary>
/// Implements <see cref="ISalesforceQueryClient"/> using NetCoreForce's <see cref="ForceClient"/>
/// for all SOQL query execution.  Handles pagination automatically and retries once on 401.
/// </summary>
public sealed class NetCoreForceQueryClient : ISalesforceQueryClient, IDisposable
{
    private readonly ISalesforceAuthenticator _authenticator;
    private readonly IOptions<SalesforceConnectionOptions> _connectionOptions;
    private readonly ILogger<NetCoreForceQueryClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly Func<string, string, IForceClientWrapper>? _wrapperFactory;

    /// <summary>
    /// Production constructor — resolves all dependencies from the DI container.
    /// </summary>
    public NetCoreForceQueryClient(
        ISalesforceAuthenticator authenticator,
        IHttpClientFactory httpClientFactory,
        IOptions<SalesforceConnectionOptions> connectionOptions,
        ILogger<NetCoreForceQueryClient> logger)
        : this(authenticator, httpClientFactory, connectionOptions, logger, null)
    {
    }

    /// <summary>
    /// Testable constructor — accepts an optional factory that creates <see cref="IForceClientWrapper"/>
    /// instances so unit tests can inject fakes without referencing NetCoreForce directly.
    /// </summary>
    internal NetCoreForceQueryClient(
        ISalesforceAuthenticator authenticator,
        IHttpClientFactory httpClientFactory,
        IOptions<SalesforceConnectionOptions> connectionOptions,
        ILogger<NetCoreForceQueryClient> logger,
        Func<string, string, IForceClientWrapper>? wrapperFactory)
    {
        if (authenticator is null) throw new ArgumentNullException(nameof(authenticator));
        if (httpClientFactory is null) throw new ArgumentNullException(nameof(httpClientFactory));
        if (connectionOptions is null) throw new ArgumentNullException(nameof(connectionOptions));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        _authenticator = authenticator;
        _connectionOptions = connectionOptions;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Salesforce");
        _wrapperFactory = wrapperFactory;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string soql,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(soql)) throw new ArgumentException("SOQL query must not be empty.", nameof(soql));

        _logger.LogDebug("QueryAsync starting. SOQL: {Soql}", soql);

        var wrapper = await BuildWrapperAsync(cancellationToken).ConfigureAwait(false);
        List<T> records;

        try
        {
            records = await MaterialiseAsync<T>(wrapper, soql, cancellationToken).ConfigureAwait(false);
        }
        catch (ForceApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Received 401 during QueryAsync. Refreshing token and retrying.");
            _authenticator.InvalidateCachedToken();
            wrapper = await BuildWrapperAsync(cancellationToken).ConfigureAwait(false);
            records = await MaterialiseAsync<T>(wrapper, soql, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("QueryAsync completed: {Count} record(s) returned.", records.Count);
        return records.AsReadOnly();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> QueryStreamAsync<T>(
        string soql,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(soql)) throw new ArgumentException("SOQL query must not be empty.", nameof(soql));

        _logger.LogDebug("QueryStreamAsync starting. SOQL: {Soql}", soql);

        var wrapper = await BuildWrapperAsync(cancellationToken).ConfigureAwait(false);
        var count = 0;

        await foreach (var record in wrapper.QueryAsync<T>(soql, cancellationToken).ConfigureAwait(false))
        {
            count++;
            yield return record;
        }

        _logger.LogInformation("QueryStreamAsync completed: {Count} record(s) streamed.", count);
    }

    private async Task<IForceClientWrapper> BuildWrapperAsync(CancellationToken cancellationToken)
    {
        var accessToken = await _authenticator.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var instanceUrl = await _authenticator.GetInstanceUrlAsync(cancellationToken).ConfigureAwait(false);

        if (_wrapperFactory is not null)
            return _wrapperFactory(instanceUrl, accessToken);

        var apiVersion = _connectionOptions.Value.ApiVersion;
        var accessInfo = new AccessTokenResponse
        {
            AccessToken = accessToken,
            InstanceUrl = instanceUrl
        };

        var forceClient = new ForceClient(instanceUrl, apiVersion, accessToken, _httpClient, accessInfo);
        return new ForceClientWrapper(forceClient);
    }

    private static async Task<List<T>> MaterialiseAsync<T>(
        IForceClientWrapper wrapper, string soql, CancellationToken cancellationToken)
        where T : class
    {
        var records = new List<T>();
        await foreach (var record in wrapper.QueryAsync<T>(soql, cancellationToken).ConfigureAwait(false))
            records.Add(record);
        return records;
    }

    /// <inheritdoc/>
    public void Dispose() => _httpClient.Dispose();
}
