using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalesforceClient.Abstractions;
using SalesforceClient.Abstractions.Options;
using SalesforceClient.NetCoreForce;

namespace SalesforceClient.Configuration;

/// <summary>
/// Extension methods for registering the Salesforce SOQL client with the .NET DI container.
/// </summary>
public static class SalesforceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISalesforceQueryClient"/> and <see cref="ISalesforceAuthenticator"/>
    /// backed by the NetCoreForce adapter.  Configuration is read from
    /// <c>Salesforce:Auth</c> and <c>Salesforce:Connection</c> sections.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">Application configuration containing Salesforce settings.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSalesforceClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));

        services.Configure<SalesforceAuthOptions>(
            configuration.GetSection(SalesforceAuthOptions.SectionName));

        services.Configure<SalesforceConnectionOptions>(
            configuration.GetSection(SalesforceConnectionOptions.SectionName));

        // Validate at startup — prevents silent misconfiguration.
        services.AddOptions<SalesforceAuthOptions>()
            .Bind(configuration.GetSection(SalesforceAuthOptions.SectionName))
            .Validate(
                o => o.GrantType is "client_credentials" or "jwt-bearer",
                "GrantType must be 'client_credentials' or 'jwt-bearer'.")
            .ValidateOnStart();

        // Named HTTP client reused for both auth and API calls.
        services.AddHttpClient("Salesforce");

        services.AddSingleton<ISalesforceAuthenticator, NetCoreForceAuthenticator>();
        services.AddSingleton<ISalesforceQueryClient, NetCoreForceQueryClient>();

        return services;
    }
}
