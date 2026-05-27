using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SalesforceClient.Abstractions;
using SalesforceClient.Abstractions.Models;
using SalesforceClient.Abstractions.Options;
using SalesforceClient.NetCoreForce;
using SalesforceClient.NetCoreForce.Internal;

namespace SalesforceClient.Tests;

public sealed class NetCoreForceQueryClientTests
{
    private static readonly IOptions<SalesforceConnectionOptions> DefaultConnectionOptions =
        Options.Create(new SalesforceConnectionOptions { ApiVersion = "v62.0" });

    private static NetCoreForceQueryClient BuildClient(
        ISalesforceAuthenticator authenticator,
        Func<string, string, IForceClientWrapper> wrapperFactory)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("Salesforce")).Returns(new HttpClient());

        return new NetCoreForceQueryClient(
            authenticator,
            factoryMock.Object,
            DefaultConnectionOptions,
            NullLogger<NetCoreForceQueryClient>.Instance,
            wrapperFactory);
    }

    private static Mock<ISalesforceAuthenticator> DefaultAuthMock(
        string token = "token", string instanceUrl = "https://test.salesforce.com")
    {
        var mock = new Mock<ISalesforceAuthenticator>();
        mock.Setup(a => a.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        mock.Setup(a => a.GetInstanceUrlAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(instanceUrl);
        return mock;
    }

    private static IForceClientWrapper WrapperReturning<T>(IEnumerable<T> records) where T : class
    {
        var mock = new Mock<IForceClientWrapper>();
        mock.Setup(w => w.QueryAsync<T>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(records.ToAsyncEnumerable());
        return mock.Object;
    }

    [Fact]
    public async Task QueryAsync_SinglePage_ReturnsAllRecords()
    {
        var accounts = new[]
        {
            new AccountRecord { Id = "001", Name = "Acme" },
            new AccountRecord { Id = "002", Name = "Globex" }
        };

        using var client = BuildClient(
            DefaultAuthMock().Object,
            (_, _) => WrapperReturning(accounts));

        var result = await client.QueryAsync<AccountRecord>("SELECT Id, Name FROM Account");

        Assert.Equal(2, result.Count);
        Assert.Equal("Acme", result[0].Name);
        Assert.Equal("Globex", result[1].Name);
    }

    [Fact]
    public async Task QueryAsync_MultiPage_ReturnsAllRecords()
    {
        // Simulate 200 records across two logical pages (ForceClient.QueryAsync handles this
        // internally; we just verify all records come through).
        var allAccounts = Enumerable.Range(1, 200)
            .Select(i => new AccountRecord { Id = $"{i:D3}", Name = $"Account {i}" })
            .ToArray();

        using var client = BuildClient(
            DefaultAuthMock().Object,
            (_, _) => WrapperReturning(allAccounts));

        var result = await client.QueryAsync<AccountRecord>("SELECT Id, Name FROM Account");

        Assert.Equal(200, result.Count);
    }

    [Fact]
    public async Task QueryAsync_On401_RefreshesTokenAndRetries()
    {
        var authMock = DefaultAuthMock();
        var callCount = 0;

        using var client = BuildClient(authMock.Object, (_, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                var throwingWrapper = new Mock<IForceClientWrapper>();
                throwingWrapper
                    .Setup(w => w.QueryAsync<AccountRecord>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(ThrowUnauthorized<AccountRecord>());
                return throwingWrapper.Object;
            }
            return WrapperReturning(new[] { new AccountRecord { Id = "001", Name = "Acme" } });
        });

        var result = await client.QueryAsync<AccountRecord>("SELECT Id, Name FROM Account");

        Assert.Single(result);
        authMock.Verify(a => a.InvalidateCachedToken(), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_SecondConsecutive401_PropagatesException()
    {
        using var client = BuildClient(DefaultAuthMock().Object, (_, _) =>
        {
            var throwingWrapper = new Mock<IForceClientWrapper>();
            throwingWrapper
                .Setup(w => w.QueryAsync<AccountRecord>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(ThrowUnauthorized<AccountRecord>());
            return throwingWrapper.Object;
        });

        // The exception type is NetCoreForce-internal; catching the base Exception is fine here.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.QueryAsync<AccountRecord>("SELECT Id FROM Account"));
    }

    [Fact]
    public async Task QueryStreamAsync_YieldsRecordsInOrder()
    {
        var contacts = Enumerable.Range(1, 5)
            .Select(i => new ContactRecord { Id = $"00{i}", LastName = $"Smith{i}" })
            .ToArray();

        using var client = BuildClient(
            DefaultAuthMock().Object,
            (_, _) => WrapperReturning(contacts));

        var streamed = new List<ContactRecord>();
        await foreach (var contact in client.QueryStreamAsync<ContactRecord>("SELECT Id, LastName FROM Contact"))
            streamed.Add(contact);

        Assert.Equal(5, streamed.Count);
        for (var i = 0; i < 5; i++)
            Assert.Equal($"Smith{i + 1}", streamed[i].LastName);
    }

    [Fact]
    public async Task QueryAsync_EmptyResult_ReturnsEmptyList()
    {
        using var client = BuildClient(
            DefaultAuthMock().Object,
            (_, _) => WrapperReturning(Array.Empty<AccountRecord>()));

        var result = await client.QueryAsync<AccountRecord>("SELECT Id FROM Account");
        Assert.Empty(result);
    }

    private static async IAsyncEnumerable<T> ThrowUnauthorized<T>()
    {
        await Task.Yield();
        throw ExceptionFactory.CreateUnauthorizedException();
#pragma warning disable CS0162 // Unreachable code — required to make this method an IAsyncEnumerable
        yield break;
#pragma warning restore CS0162
    }
}

internal static class AsyncEnumerableExtensions
{
    internal static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
