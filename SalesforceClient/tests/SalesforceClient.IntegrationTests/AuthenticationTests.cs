using System.Threading.Tasks;
using SalesforceClient.Abstractions;
using Xunit.Abstractions;

namespace SalesforceClient.IntegrationTests;

[Collection(SalesforceCollection.Name)]
public sealed class AuthenticationTests(SalesforceFixture fixture, ITestOutputHelper output)
{
    private const string SkipReason =
        "No Salesforce credentials configured. Populate appsettings.integration.json or set environment variables.";

    [SkippableFact]
    public async Task AcquiresAccessToken()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var token = await fixture.Authenticator.GetAccessTokenAsync();

        Assert.False(string.IsNullOrWhiteSpace(token), "Expected a non-empty access token.");
        output.WriteLine($"Token (first 20 chars): {token[..Math.Min(20, token.Length)]}…");
    }

    [SkippableFact]
    public async Task AcquiresInstanceUrl()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var instanceUrl = await fixture.Authenticator.GetInstanceUrlAsync();

        Assert.False(string.IsNullOrWhiteSpace(instanceUrl), "Expected a non-empty instance URL.");
        Assert.StartsWith("https://", instanceUrl);
        output.WriteLine($"Instance URL: {instanceUrl}");
    }

    [SkippableFact]
    public async Task TokenIsCached_SecondCallReturnsSameToken()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var first = await fixture.Authenticator.GetAccessTokenAsync();
        var second = await fixture.Authenticator.GetAccessTokenAsync();

        Assert.Equal(first, second);
        output.WriteLine("Token cache confirmed — both calls returned the same token.");
    }

    [SkippableFact]
    public async Task InvalidateThenReacquire_ReturnsValidToken()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var original = await fixture.Authenticator.GetAccessTokenAsync();

        fixture.Authenticator.InvalidateCachedToken();
        output.WriteLine("Token invalidated.");

        var refreshed = await fixture.Authenticator.GetAccessTokenAsync();

        Assert.False(string.IsNullOrWhiteSpace(refreshed), "Expected a valid token after invalidation.");
        output.WriteLine($"Refreshed token differs from original: {original != refreshed}");
    }
}
