using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesforceClient.Abstractions;
using SalesforceClient.Abstractions.Models;
using SalesforceClient.Configuration;

// Wire Ctrl+C / SIGTERM to a CancellationToken so in-flight queries are cancelled gracefully.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nCancellation requested — shutting down.");
};

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddSalesforceClient(ctx.Configuration);
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var queryClient = host.Services.GetRequiredService<ISalesforceQueryClient>();

try
{
    // ── Query 1: Accounts (materialised list) ────────────────────────────
    logger.LogInformation("=== QueryAsync<AccountRecord> ===");
    var accounts = await queryClient.QueryAsync<AccountRecord>(
        "SELECT Id, Name, Industry, AnnualRevenue, CreatedDate FROM Account LIMIT 5",
        cts.Token);

    Console.WriteLine($"Accounts returned: {accounts.Count}");
    foreach (var a in accounts)
        Console.WriteLine($"  [{a.Id}] {a.Name} | {a.Industry} | {a.AnnualRevenue:C} | {a.CreatedDate:d}");

    // ── Query 2: Contacts (materialised list) ─────────────────────────────
    logger.LogInformation("=== QueryAsync<ContactRecord> ===");
    var contacts = await queryClient.QueryAsync<ContactRecord>(
        "SELECT Id, FirstName, LastName, Email, AccountId FROM Contact LIMIT 5",
        cts.Token);

    Console.WriteLine($"Contacts returned: {contacts.Count}");
    foreach (var c in contacts)
        Console.WriteLine($"  [{c.Id}] {c.FirstName} {c.LastName} | {c.Email}");

    // ── Query 3: Accounts (streaming) ────────────────────────────────────
    logger.LogInformation("=== QueryStreamAsync<AccountRecord> ===");
    Console.WriteLine("Streaming accounts (first 10):");
    var streamCount = 0;
    await foreach (var account in queryClient.QueryStreamAsync<AccountRecord>(
        "SELECT Id, Name FROM Account LIMIT 10", cts.Token))
    {
        Console.WriteLine($"  [stream] [{account.Id}] {account.Name}");
        streamCount++;
    }
    Console.WriteLine($"Total streamed: {streamCount}");
}
catch (OperationCanceledException)
{
    logger.LogWarning("Operation was cancelled by the user.");
}
catch (Exception ex)
{
    logger.LogError(ex, "An unexpected error occurred.");
    return 1;
}

return 0;
