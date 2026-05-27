namespace SalesforceClient.Abstractions.Options;

/// <summary>Authentication options populated from IConfiguration.</summary>
public sealed class SalesforceAuthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Salesforce:Auth";

    /// <summary>OAuth grant type. Must be "client_credentials" or "jwt-bearer".</summary>
    public required string GrantType { get; init; }

    /// <summary>Salesforce login URL, e.g. https://login.salesforce.com</summary>
    public required string LoginUrl { get; init; }

    /// <summary>Connected app client ID.</summary>
    public required string ClientId { get; init; }

    /// <summary>Client secret — required for client_credentials grant type.</summary>
    public string? ClientSecret { get; init; }

    /// <summary>Salesforce username — subject claim for jwt-bearer.</summary>
    public string? Username { get; init; }

    /// <summary>Path to a PEM-encoded RSA private key file — used for jwt-bearer.</summary>
    public string? PrivateKeyPath { get; init; }

    /// <summary>Inline PEM-encoded RSA private key — alternative to PrivateKeyPath.</summary>
    public string? PrivateKeyPem { get; init; }

    /// <summary>JWT expiry window in seconds. Defaults to 180.</summary>
    public int JwtExpirySeconds { get; init; } = 180;
}
