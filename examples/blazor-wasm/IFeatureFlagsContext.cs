using Newtonsoft.Json.Linq;

namespace blazor_wasm;

public interface IFeatureFlagsContext : IDisposable
{
    public Task<bool> GetBoolFlagAsync(string flagid);
    public Task<double> GetNumberFlagAsync(string flagid);
    public Task<string> GetStrFlagAsync(string flagid);
    public Task<JObject> GetJsonFlagAsync(string flagid);

    public bool IsAuthenticated();

    public bool IsSdkKeyValid();
}