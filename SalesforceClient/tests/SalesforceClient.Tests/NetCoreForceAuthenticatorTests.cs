using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SalesforceClient.Abstractions;
using SalesforceClient.Abstractions.Options;
using SalesforceClient.NetCoreForce;
using SalesforceClient.Tests.Helpers;

namespace SalesforceClient.Tests;

public sealed class NetCoreForceAuthenticatorTests
{
    private const string ValidTokenJson = """
        {
            "access_token": "test-access-token",
            "instance_url": "https://test.salesforce.com",
            "issued_at": "1234567890000",
            "token_type": "Bearer"
        }
        """;

    private static NetCoreForceAuthenticator BuildAuthenticator(
        FakeHttpMessageHandler handler,
        string grantType = "client_credentials")
    {
        var opts = Options.Create(new SalesforceAuthOptions
        {
            GrantType = grantType,
            LoginUrl = "https://login.salesforce.com",
            ClientId = "clientId",
            ClientSecret = "clientSecret",
            Username = "user@example.com",
            PrivateKeyPem = GenerateFakeRsaPem()
        });

        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("Salesforce")).Returns(httpClient);

        return new NetCoreForceAuthenticator(opts, factoryMock.Object, NullLogger<NetCoreForceAuthenticator>.Instance);
    }

    [Fact]
    public async Task ClientCredentials_ReturnsToken()
    {
        using var auth = BuildAuthenticator(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk(ValidTokenJson)));
        var token = await auth.GetAccessTokenAsync();
        Assert.Equal("test-access-token", token);
    }

    [Fact]
    public async Task ClientCredentials_CachesToken_SecondCallNoHttpRequest()
    {
        // Queue only one response; a second HTTP call would return an error.
        var handler = new FakeHttpMessageHandler(
            FakeHttpMessageHandler.JsonOk(ValidTokenJson),
            FakeHttpMessageHandler.Error(HttpStatusCode.InternalServerError));

        using var auth = BuildAuthenticator(handler);

        var first = await auth.GetAccessTokenAsync();
        var second = await auth.GetAccessTokenAsync();    // Must come from cache.

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task InvalidateCachedToken_ForcesRefreshOnNextCall()
    {
        const string SecondTokenJson = """
            {
                "access_token": "refreshed-token",
                "instance_url": "https://test.salesforce.com",
                "issued_at": "1234567890000",
                "token_type": "Bearer"
            }
            """;

        var handler = new FakeHttpMessageHandler(
            FakeHttpMessageHandler.JsonOk(ValidTokenJson),
            FakeHttpMessageHandler.JsonOk(SecondTokenJson));

        using var auth = BuildAuthenticator(handler);

        var first = await auth.GetAccessTokenAsync();
        auth.InvalidateCachedToken();
        var second = await auth.GetAccessTokenAsync();

        Assert.Equal("test-access-token", first);
        Assert.Equal("refreshed-token", second);
    }

    [Fact]
    public async Task ConcurrentCallers_OnlyOneHttpRequestMade()
    {
        // Only one valid response queued — if two HTTP calls go out, second returns 500.
        var handler = new FakeHttpMessageHandler(
            FakeHttpMessageHandler.JsonOk(ValidTokenJson),
            FakeHttpMessageHandler.Error(HttpStatusCode.InternalServerError));

        using var auth = BuildAuthenticator(handler);

        var t1 = auth.GetAccessTokenAsync().AsTask();
        var t2 = auth.GetAccessTokenAsync().AsTask();
        var t3 = auth.GetAccessTokenAsync().AsTask();

        var results = await Task.WhenAll(t1, t2, t3);

        Assert.All(results, t => Assert.Equal("test-access-token", t));
    }

    [Fact]
    public async Task JwtBearer_ReturnsToken()
    {
        var handler = new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk(ValidTokenJson));
        using var auth = BuildAuthenticator(handler, grantType: "jwt-bearer");

        var token = await auth.GetAccessTokenAsync();

        Assert.Equal("test-access-token", token);
    }

    [Fact]
    public async Task UnsupportedGrantType_ThrowsNotSupportedException()
    {
        var opts = Options.Create(new SalesforceAuthOptions
        {
            GrantType = "password",
            LoginUrl = "https://login.salesforce.com",
            ClientId = "id",
            ClientSecret = "secret"
        });

        var httpClient = new HttpClient(new FakeHttpMessageHandler());
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("Salesforce")).Returns(httpClient);

        using var auth = new NetCoreForceAuthenticator(opts, factoryMock.Object, NullLogger<NetCoreForceAuthenticator>.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(() => auth.GetAccessTokenAsync().AsTask());
    }

    [Fact]
    public async Task AuthFailure_ThrowsInvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(
            FakeHttpMessageHandler.Error(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}"""));

        using var auth = BuildAuthenticator(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.GetAccessTokenAsync().AsTask());
    }

    private static string GenerateFakeRsaPem()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }
}
