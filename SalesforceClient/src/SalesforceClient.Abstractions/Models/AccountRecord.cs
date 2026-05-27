using System;
using System.Text.Json.Serialization;

namespace SalesforceClient.Abstractions.Models;

/// <summary>Represents a Salesforce Account SObject query result.</summary>
public sealed record AccountRecord : SalesforceRecord
{
    /// <summary>Account name.</summary>
    [JsonPropertyName("Name")]
    public string Name { get; init; } = default!;

    /// <summary>Industry vertical.</summary>
    [JsonPropertyName("Industry")]
    public string? Industry { get; init; }

    /// <summary>Annual revenue in the organisation's currency.</summary>
    [JsonPropertyName("AnnualRevenue")]
    public decimal? AnnualRevenue { get; init; }

    /// <summary>Date and time the record was created.</summary>
    [JsonPropertyName("CreatedDate")]
    public DateTimeOffset? CreatedDate { get; init; }
}
