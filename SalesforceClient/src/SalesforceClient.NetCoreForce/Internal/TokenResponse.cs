using System.Text.Json.Serialization;

namespace SalesforceClient.NetCoreForce.Internal;

/// <summary>Internal DTO for deserialising OAuth token endpoint responses.</summary>
internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = default!;

    [JsonPropertyName("instance_url")]
    public string InstanceUrl { get; set; } = default!;

    [JsonPropertyName("issued_at")]
    public string? IssuedAt { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}
