using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SalesforceClient.Abstractions;
using SalesforceClient.Abstractions.Options;
using SalesforceClient.NetCoreForce.Internal;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;

namespace SalesforceClient.NetCoreForce;

/// <summary>
/// Implements <see cref="ISalesforceAuthenticator"/> using NetCoreForce's HttpClientFactory
/// for all HTTP communication.  Supports <c>client_credentials</c> and <c>jwt-bearer</c>
/// OAuth flows with thread-safe token caching and on-demand refresh.
/// </summary>
public sealed class NetCoreForceAuthenticator : ISalesforceAuthenticator, IDisposable
{
    private readonly IOptions<SalesforceAuthOptions> _authOptions;
    private readonly ILogger<NetCoreForceAuthenticator> _logger;
    private readonly HttpClient _httpClient;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private CachedToken? _cachedToken;

    /// <summary>
    /// Initialises a new <see cref="NetCoreForceAuthenticator"/> using the named
    /// <c>Salesforce</c> HTTP client provided by <paramref name="httpClientFactory"/>.
    /// </summary>
    public NetCoreForceAuthenticator(
        IOptions<SalesforceAuthOptions> authOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<NetCoreForceAuthenticator> logger)
    {
        if (authOptions is null) throw new ArgumentNullException(nameof(authOptions));
        if (httpClientFactory is null) throw new ArgumentNullException(nameof(httpClientFactory));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        _authOptions = authOptions;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Salesforce");
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cachedToken;
        if (cached is not null && DateTimeOffset.UtcNow < cached.ExpiresAt)
            return cached.AccessToken;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cachedToken;
            if (cached is not null && DateTimeOffset.UtcNow < cached.ExpiresAt)
                return cached.AccessToken;

            _cachedToken = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Salesforce access token acquired; expires ~{ExpiresAt:u}", _cachedToken.ExpiresAt);
            return _cachedToken.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<string> GetInstanceUrlAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cachedToken;
        if (cached is not null && DateTimeOffset.UtcNow < cached.ExpiresAt)
            return cached.InstanceUrl;

        await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return _cachedToken!.InstanceUrl;
    }

    /// <inheritdoc/>
    public void InvalidateCachedToken()
    {
        _cachedToken = null;
        _logger.LogDebug("Cached Salesforce token invalidated.");
    }

    private Task<CachedToken> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var opts = _authOptions.Value;
        return opts.GrantType switch
        {
            "client_credentials" => ClientCredentialsAsync(opts, cancellationToken),
            "jwt-bearer" => JwtBearerAsync(opts, cancellationToken),
            _ => throw new NotSupportedException($"Grant type '{opts.GrantType}' is not supported. Use 'client_credentials' or 'jwt-bearer'.")
        };
    }

    private async Task<CachedToken> ClientCredentialsAsync(SalesforceAuthOptions opts, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(opts.ClientSecret))
            throw new InvalidOperationException("ClientSecret is required for the client_credentials grant type.");

        var tokenEndpoint = BuildTokenEndpoint(opts.LoginUrl);
        _logger.LogDebug("Requesting client_credentials token from {Endpoint}", tokenEndpoint);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = opts.ClientId,
            ["client_secret"] = opts.ClientSecret
        });

        using var response = await _httpClient.PostAsync(tokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        return await ParseAndCacheTokenResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CachedToken> JwtBearerAsync(SalesforceAuthOptions opts, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(opts.Username))
            throw new InvalidOperationException("Username is required for the jwt-bearer grant type.");

        var pem = await LoadPrivateKeyPemAsync(opts, cancellationToken).ConfigureAwait(false);
        var assertion = BuildJwtAssertion(opts, pem);
        var tokenEndpoint = BuildTokenEndpoint(opts.LoginUrl);

        _logger.LogDebug("Requesting jwt-bearer token from {Endpoint} for subject {Username}", tokenEndpoint, opts.Username);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });

        using var response = await _httpClient.PostAsync(tokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        return await ParseAndCacheTokenResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> LoadPrivateKeyPemAsync(SalesforceAuthOptions opts, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(opts.PrivateKeyPath))
            return await File.ReadAllTextAsync(opts.PrivateKeyPath, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(opts.PrivateKeyPem))
            return opts.PrivateKeyPem;

        throw new InvalidOperationException(
            "Either PrivateKeyPath or PrivateKeyPem must be provided for the jwt-bearer grant type.");
    }

    private static string BuildJwtAssertion(SalesforceAuthOptions opts, string pem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var rsaParams = rsa.ExportParameters(true);
        var rsaKey = new RsaSecurityKey(rsaParams);
        var credentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

        var now = DateTimeOffset.UtcNow;
        var handler = new JwtSecurityTokenHandler();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opts.ClientId,
            Audience = opts.LoginUrl,
            Subject = new ClaimsIdentity(new[] { new Claim("sub", opts.Username!) }),
            Expires = now.AddSeconds(opts.JwtExpirySeconds).UtcDateTime,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            SigningCredentials = credentials
        };

        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private async Task<CachedToken> ParseAndCacheTokenResponseAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Salesforce authentication failed with {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new InvalidOperationException(
                $"Salesforce authentication failed ({(int)response.StatusCode}): {body}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body)
            ?? throw new InvalidOperationException("Salesforce returned an empty or unparseable token response.");

        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException(
                $"Salesforce returned a token response with no access_token. Error: {tokenResponse.Error} — {tokenResponse.ErrorDescription}");
        }

        // Salesforce access tokens expire per org session settings (default 2 hours).
        // Cache conservatively for 50 minutes to stay well within that window.
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(50);

        _logger.LogInformation(
            "Salesforce token obtained for instance {InstanceUrl}; cached until {ExpiresAt:u}",
            tokenResponse.InstanceUrl, expiresAt);

        return new CachedToken(tokenResponse.AccessToken, tokenResponse.InstanceUrl, expiresAt);
    }

    private static string BuildTokenEndpoint(string loginUrl)
        => $"{loginUrl.TrimEnd('/')}/services/oauth2/token";

    /// <inheritdoc/>
    public void Dispose()
    {
        _lock.Dispose();
    }

    /// <summary>Immutable snapshot of a cached access token.</summary>
    private sealed record CachedToken(string AccessToken, string InstanceUrl, DateTimeOffset ExpiresAt);
}
