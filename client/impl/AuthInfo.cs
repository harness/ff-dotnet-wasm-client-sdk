namespace ff_dotnet_wasm_client_sdk.client;

internal class AuthInfo
{
    public string? Project { get; set; }
    public string Environment { get; set; }
    public string? ProjectIdentifier { get; set; }
    public string EnvironmentIdentifier { get; set; }
    public string? AccountId { get; set; }
    public string? Organization { get; set; }
    public string ClusterIdentifier { get; set; }
    public string BearerToken { get; set; }
    public string ApiKey { get; set; }
}