using System.Text.Json.Serialization;

namespace SalesforceClient.Abstractions.Models;

/// <summary>Represents a Salesforce Contact SObject query result.</summary>
public sealed record ContactRecord : SalesforceRecord
{
    /// <summary>Contact first name.</summary>
    [JsonPropertyName("FirstName")]
    public string? FirstName { get; init; }

    /// <summary>Contact last name.</summary>
    [JsonPropertyName("LastName")]
    public string LastName { get; init; } = default!;

    /// <summary>Contact email address.</summary>
    [JsonPropertyName("Email")]
    public string? Email { get; init; }

    /// <summary>ID of the related Account record.</summary>
    [JsonPropertyName("AccountId")]
    public string? AccountId { get; init; }
}
