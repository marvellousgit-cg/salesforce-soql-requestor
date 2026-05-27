# SalesforceClient

A production-ready .NET 10 class library for executing Salesforce SOQL queries, built on top of [NetCoreForce.Client](https://github.com/anthonyreilly/NetCoreForce) with a clean abstraction layer that makes the underlying adapter fully replaceable.

## Prerequisites

- .NET 10 SDK
- A Salesforce Connected App configured for one of the supported OAuth grant types
- (jwt-bearer only) An RSA private key and its public certificate uploaded to the Connected App

## Solution Structure

```
SalesforceClient.sln
├── src/
│   ├── SalesforceClient.Abstractions        # Interfaces and model types (netstandard2.1)
│   ├── SalesforceClient.NetCoreForce        # NetCoreForce adapter (net10.0)
│   └── SalesforceClient.Configuration      # DI / IConfiguration wiring (net10.0)
├── tests/
│   └── SalesforceClient.Tests              # xUnit unit tests
└── samples/
    └── SalesforceClient.ConsoleSample      # Runnable demonstration
```

## Configuration

Copy `samples/SalesforceClient.ConsoleSample/appsettings.json` and fill in your values.

### client_credentials

```json
{
  "Salesforce": {
    "Auth": {
      "GrantType": "client_credentials",
      "LoginUrl": "https://login.salesforce.com",
      "ClientId": "<connected-app-client-id>",
      "ClientSecret": "<connected-app-client-secret>"
    },
    "Connection": { "ApiVersion": "v62.0" }
  }
}
```

### jwt-bearer

```json
{
  "Salesforce": {
    "Auth": {
      "GrantType": "jwt-bearer",
      "LoginUrl": "https://login.salesforce.com",
      "ClientId": "<connected-app-client-id>",
      "Username": "integration@example.com",
      "PrivateKeyPath": "/path/to/private-key.pem"
    },
    "Connection": { "ApiVersion": "v62.0" }
  }
}
```

See `appsettings.jwt-bearer.example.json` for the full schema with annotations.

## Registering Services

```csharp
// In Program.cs / Startup.cs
services.AddSalesforceClient(configuration);
```

This registers `ISalesforceAuthenticator` and `ISalesforceQueryClient` as singletons and validates the configuration at startup.

## Running the Sample

```bash
cd samples/SalesforceClient.ConsoleSample
# Edit appsettings.json with your org credentials
dotnet run
```

Press `Ctrl+C` to cancel in-flight queries gracefully.

## Running the Tests

```bash
dotnet test
```

All tests are pure unit tests that run without a real Salesforce connection.

## Defining Custom Query Models

Extend `SalesforceRecord` to map any SObject:

```csharp
using SalesforceClient.Abstractions.Models;
using System.Text.Json.Serialization;

public sealed record OpportunityRecord : SalesforceRecord
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("Amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("CloseDate")]
    public DateTimeOffset? CloseDate { get; init; }
}

// Usage:
var opps = await queryClient.QueryAsync<OpportunityRecord>(
    "SELECT Id, Name, Amount, CloseDate FROM Opportunity LIMIT 20");
```

## How to Swap NetCoreForce

The abstraction boundary means that replacing the backing HTTP/OAuth library requires **zero changes to consuming code** — only the `SalesforceClient.NetCoreForce` project needs to change.

### What to change

1. Create a new class library (e.g. `SalesforceClient.PrivateFork`) targeting `net10.0`.
2. Implement `ISalesforceAuthenticator` and `ISalesforceQueryClient` (both defined in `SalesforceClient.Abstractions`) using your replacement library. No NetCoreForce packages are needed.
3. In `SalesforceClient.Configuration`, update the `AddSalesforceClient` registrations:

   ```csharp
   // Old:
   services.AddSingleton<ISalesforceAuthenticator, NetCoreForceAuthenticator>();
   services.AddSingleton<ISalesforceQueryClient, NetCoreForceQueryClient>();

   // New:
   services.AddSingleton<ISalesforceAuthenticator, MyForkAuthenticator>();
   services.AddSingleton<ISalesforceQueryClient, MyForkQueryClient>();
   ```

4. Remove the `<ProjectReference>` to `SalesforceClient.NetCoreForce` from `SalesforceClient.Configuration` and add one pointing to your new project.

### What does NOT change

- `SalesforceClient.Abstractions` — unchanged, no third-party deps.
- All consuming application code — it depends only on `ISalesforceQueryClient` and model types from Abstractions.
- `SalesforceClient.Tests` — tests that mock `ISalesforceAuthenticator` continue to work unchanged.

The key invariant: the `using NetCoreForce` directives exist **only** inside `SalesforceClient.NetCoreForce`. Grep the solution to verify before any release:

```bash
grep -r "using NetCoreForce" --include="*.cs" .
# Should only return files under src/SalesforceClient.NetCoreForce/
```
