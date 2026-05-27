namespace SalesforceClient.Abstractions.Options;

/// <summary>Salesforce API connection options.</summary>
public sealed class SalesforceConnectionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Salesforce:Connection";

    /// <summary>Salesforce REST API version. Defaults to v62.0.</summary>
    public string ApiVersion { get; init; } = "v62.0";
}
