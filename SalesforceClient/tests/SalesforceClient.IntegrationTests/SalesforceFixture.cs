using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesforceClient.Abstractions;
using SalesforceClient.Configuration;

namespace SalesforceClient.IntegrationTests;

/// <summary>
/// xUnit collection fixture that boots a real DI container wired to live Salesforce
/// credentials.  Shared across all tests in <see cref="SalesforceCollection"/> so the
/// OAuth token is acquired once and reused.
/// </summary>
public sealed class SalesforceFixture : IDisposable
{
    private readonly IHost _host;

    public SalesforceFixture()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                // appsettings.integration.json is gitignored; copy it alongside the test
                // binary to provide credentials locally.
                cfg.AddJsonFile("appsettings.integration.json", optional: true, reloadOnChange: false);
                // Environment variables override file config — useful in CI pipelines.
                // Prefix: none required; standard double-underscore section separator applies.
                // e.g. Salesforce__Auth__ClientId=xxx
                cfg.AddEnvironmentVariables();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddSalesforceClient(ctx.Configuration);
                services.AddLogging(b =>
                    b.AddConsole().SetMinimumLevel(LogLevel.Debug));
            })
            .Build();

        QueryClient = _host.Services.GetRequiredService<ISalesforceQueryClient>();
        Authenticator = _host.Services.GetRequiredService<ISalesforceAuthenticator>();

        var cfg = _host.Services.GetRequiredService<IConfiguration>();
        IsConfigured = IsCredentialConfigured(cfg);
    }

    /// <summary>Shared query client — token is cached across all integration tests.</summary>
    public ISalesforceQueryClient QueryClient { get; }

    /// <summary>Shared authenticator — allows token-invalidation tests.</summary>
    public ISalesforceAuthenticator Authenticator { get; }

    /// <summary>
    /// <c>true</c> when real credentials are present; <c>false</c> means tests will be
    /// skipped automatically.
    /// </summary>
    public bool IsConfigured { get; }

    private static bool IsCredentialConfigured(IConfiguration cfg)
    {
        var clientId = cfg["Salesforce:Auth:ClientId"];
        return !string.IsNullOrWhiteSpace(clientId)
            && clientId != "<your-connected-app-client-id>";
    }

    public void Dispose() => _host.Dispose();
}

/// <summary>xUnit collection definition — ensures one <see cref="SalesforceFixture"/> instance.</summary>
[CollectionDefinition(SalesforceCollection.Name)]
public sealed class SalesforceCollection : ICollectionFixture<SalesforceFixture>
{
    public const string Name = "Salesforce integration";
}
