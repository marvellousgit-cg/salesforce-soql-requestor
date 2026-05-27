using System.Text.Json.Serialization;

namespace SalesforceClient.Abstractions.Models;

/// <summary>Base class for all Salesforce SObject result models.</summary>
public abstract record SalesforceRecord
{
    /// <summary>Salesforce record ID.</summary>
    [JsonPropertyName("Id")]
    public string Id { get; init; } = default!;

    /// <summary>SObject metadata attributes returned by the API.</summary>
    [JsonPropertyName("attributes")]
    public SObjectAttributes? Attributes { get; init; }
}

/// <summary>SObject metadata attributes returned alongside each record.</summary>
public sealed record SObjectAttributes
{
    /// <summary>SObject API type name, e.g. "Account".</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = default!;

    /// <summary>Relative URL to this record in the REST API.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = default!;
}
