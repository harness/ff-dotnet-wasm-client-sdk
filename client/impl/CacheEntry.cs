using io.harness.ff_dotnet_client_sdk.openapi.Model;

namespace ff_dotnet_wasm_client_sdk.client.impl;

internal class CacheEntry
{
    internal CacheEntry(bool success, int errStatusCode, string statusMsg, Evaluation? eval, DateTime updated)
    {
        Success = success;
        Eval = eval;
        LastUpdated = updated;
        StatusCode = errStatusCode;
        StatusMsg = statusMsg;
    }

    internal bool Success { get; set; }
    internal Evaluation? Eval { get; set; }
    internal DateTime LastUpdated { get; set; }
    internal int StatusCode { get; set; }
    internal string StatusMsg { get; set; }

    internal bool IsSuccess()
    {
        return Success;
    }
}