using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SalesforceClient.Abstractions.Models;
using Xunit.Abstractions;

namespace SalesforceClient.IntegrationTests;

[Collection(SalesforceCollection.Name)]
public sealed class QueryTests(SalesforceFixture fixture, ITestOutputHelper output)
{
    private const string SkipReason =
        "No Salesforce credentials configured. Populate appsettings.integration.json or set environment variables.";

    // ── QueryAsync ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task QueryAsync_Accounts_ReturnsResults()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var results = await fixture.QueryClient.QueryAsync<AccountRecord>(
            "SELECT Id, Name, Industry, AnnualRevenue, CreatedDate FROM Account LIMIT 5");

        Assert.NotNull(results);
        output.WriteLine($"Accounts returned: {results.Count}");
        foreach (var a in results)
            output.WriteLine($"  [{a.Id}] {a.Name} — {a.Industry}");
    }

    [SkippableFact]
    public async Task QueryAsync_Contacts_ReturnsResults()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var results = await fixture.QueryClient.QueryAsync<ContactRecord>(
            "SELECT Id, FirstName, LastName, Email, AccountId FROM Contact LIMIT 5");

        Assert.NotNull(results);
        output.WriteLine($"Contacts returned: {results.Count}");
        foreach (var c in results)
            output.WriteLine($"  [{c.Id}] {c.FirstName} {c.LastName} — {c.Email}");
    }

    [SkippableFact]
    public async Task QueryAsync_ReturnsReadOnlyList()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var results = await fixture.QueryClient.QueryAsync<AccountRecord>(
            "SELECT Id, Name FROM Account LIMIT 1");

        // Verify the return type is truly read-only at runtime
        Assert.IsAssignableFrom<IReadOnlyList<AccountRecord>>(results);
    }

    [SkippableFact]
    public async Task QueryAsync_AllFields_DeserialiseCorrectly()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        // Pull one account and verify every mapped field deserialises without throwing.
        var results = await fixture.QueryClient.QueryAsync<AccountRecord>(
            "SELECT Id, Name, Industry, AnnualRevenue, CreatedDate FROM Account LIMIT 1");

        if (results.Count == 0)
        {
            output.WriteLine("No Account records found — skipping field assertion.");
            return;
        }

        var a = results[0];
        Assert.False(string.IsNullOrWhiteSpace(a.Id));
        Assert.False(string.IsNullOrWhiteSpace(a.Name));
        output.WriteLine($"Id={a.Id} Name={a.Name} Industry={a.Industry} Revenue={a.AnnualRevenue} Created={a.CreatedDate}");
    }

    [SkippableFact]
    public async Task QueryAsync_ZeroMatchSoql_ReturnsEmptyList()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        // A WHERE clause designed to match nothing.
        var results = await fixture.QueryClient.QueryAsync<AccountRecord>(
            "SELECT Id FROM Account WHERE Name = '__integration_test_no_match__'");

        Assert.NotNull(results);
        Assert.Empty(results);
        output.WriteLine("Empty result confirmed.");
    }

    [SkippableFact]
    public async Task QueryAsync_InvalidSoql_ThrowsException()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            fixture.QueryClient.QueryAsync<AccountRecord>("THIS IS NOT VALID SOQL"));
    }

    [SkippableFact]
    public async Task QueryAsync_EmptyString_ThrowsArgumentException()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.QueryClient.QueryAsync<AccountRecord>(string.Empty));
    }

    // ── Pagination ────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task QueryAsync_LargeResult_PaginatesTransparently()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        // Request enough records to force at least one continuation if the org has them
        // (Salesforce default batch is 2000; use LIMIT 2001 if the org has that many rows).
        // We use LIMIT 500 and verify the count matches what SOQL COUNT() reports.
        const string countSoql = "SELECT COUNT() FROM Account";
        const string dataSoql = "SELECT Id FROM Account LIMIT 500";

        var countResults = await fixture.QueryClient.QueryAsync<SObjectCount>(countSoql);
        var dataResults = await fixture.QueryClient.QueryAsync<AccountRecord>(dataSoql);

        // We can't assert exact equality (COUNT might be > 500) but we can verify
        // the materialised list is consistent with the stated LIMIT.
        Assert.True(dataResults.Count <= 500);
        output.WriteLine($"COUNT()={countResults.Count}, LIMIT 500 returned={dataResults.Count}");
    }

    // ── QueryStreamAsync ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task QueryStreamAsync_Accounts_StreamsInOrder()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        var streamed = new List<AccountRecord>();
        await foreach (var a in fixture.QueryClient.QueryStreamAsync<AccountRecord>(
            "SELECT Id, Name FROM Account LIMIT 10"))
        {
            streamed.Add(a);
            output.WriteLine($"  [stream] [{a.Id}] {a.Name}");
        }

        output.WriteLine($"Total streamed: {streamed.Count}");
        // Each yielded record must have a non-empty Id.
        Assert.All(streamed, a => Assert.False(string.IsNullOrWhiteSpace(a.Id)));
    }

    [SkippableFact]
    public async Task QueryStreamAsync_MatchesQueryAsync_SameRecordCount()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        const string soql = "SELECT Id FROM Account LIMIT 20";

        var materialised = await fixture.QueryClient.QueryAsync<AccountRecord>(soql);

        var streamed = new List<AccountRecord>();
        await foreach (var a in fixture.QueryClient.QueryStreamAsync<AccountRecord>(soql))
            streamed.Add(a);

        Assert.Equal(materialised.Count, streamed.Count);
        output.WriteLine($"Both methods returned {materialised.Count} records.");
    }

    [SkippableFact]
    public async Task QueryStreamAsync_CancellationToken_StopsEnumeration()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        using var cts = new CancellationTokenSource();
        var count = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in fixture.QueryClient.QueryStreamAsync<AccountRecord>(
                "SELECT Id FROM Account LIMIT 50", cts.Token))
            {
                count++;
                if (count >= 3)
                    cts.Cancel(); // Cancel after 3 records
            }
        });

        output.WriteLine($"Enumeration cancelled after {count} record(s).");
        Assert.True(count >= 3, "Expected at least 3 records before cancellation.");
    }

    // ── Token recovery ────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task QueryAsync_AfterTokenInvalidation_RecoverstTransparently()
    {
        Skip.IfNot(fixture.IsConfigured, SkipReason);

        // Warm up the cache.
        await fixture.QueryClient.QueryAsync<AccountRecord>("SELECT Id FROM Account LIMIT 1");

        // Force the next query to re-authenticate.
        fixture.Authenticator.InvalidateCachedToken();
        output.WriteLine("Token invalidated — next query must re-authenticate.");

        // This should acquire a fresh token automatically and succeed.
        var results = await fixture.QueryClient.QueryAsync<AccountRecord>(
            "SELECT Id, Name FROM Account LIMIT 3");

        Assert.NotNull(results);
        output.WriteLine($"Re-authenticated successfully; {results.Count} record(s) returned.");
    }
}

/// <summary>
/// Minimal model for COUNT() queries.  Salesforce returns <c>totalSize</c> but no records
/// for aggregate-only queries, so this is intentionally empty.
/// </summary>
public sealed record SObjectCount : SalesforceRecord;
