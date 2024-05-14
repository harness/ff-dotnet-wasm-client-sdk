
using ff_dotnet_wasm_client_sdk.client;
using ff_dotnet_wasm_client_sdk.client.dto;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace blazor_wasm;

public class FeatureFlagsContext : IFeatureFlagsContext
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly string _apiKey = ""; // <-- add your client SDK key here
    private string? _token = "token not set";
    private FfClient? _client;

    public FeatureFlagsContext(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<FeatureFlagsContext>();
        _logger.LogInformation("Created FF context");
    }

    public void Dispose()
    {
        _logger.LogInformation("Dispose FF context");
        _client?.Dispose();
    }

    public async Task<bool> GetBoolFlagAsync(string flagid)
    {
        return await _client?.BoolVariationAsync(flagid, false);
    }

    public async Task<double> GetNumberFlagAsync(string flagid)
    {
        return await _client?.NumberVariationAsync(flagid, 0.0);
    }

    public async Task<string> GetStrFlagAsync(string flagid)
    {
        return await _client?.StringVariationAsync(flagid, "(defaultValue)");
    }

    public async Task<JObject> GetJsonFlagAsync(string flagid)
    {
        string json = "{'default': 'value'}";
        return await _client?.JsonVariationAsync(flagid, JObject.Parse(json));
    }


    public bool IsAuthenticated()
    {
        return _client?.IsAuthenticated() ?? false;
    }

    public bool IsSdkKeyValid()
    {
        return _apiKey.Trim().Length > 0;
    }

    public async void Init()
    {
        try
        {
            var target = new FfTarget("dotNETwasm", "dotNETwasm",
                new Dictionary<string, string> { { "email", "person@myorg.com" }});

            var config = FfConfig.Builder()
                //.ConfigUrl("http://localhost:8000/api/1.0")
                //.EventUrl(" http://localhost:8001/api/1.0")
                .LoggerFactory(_loggerFactory)
                .Debug(true)
                .Build();

            _client = new FfClient(_apiKey, config, target);
            await _client.InitializeAsync();
        }
        catch (Exception ex)
        {
            _token = ex.Message;
            _logger.LogError(ex, "exception");
        }

    }
}