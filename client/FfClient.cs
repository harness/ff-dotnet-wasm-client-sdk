using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Timers;
using ff_dotnet_wasm_client_sdk.client.dto;
using ff_dotnet_wasm_client_sdk.client.impl;
using io.harness.ff_dotnet_client_sdk.openapi.Api;
using io.harness.ff_dotnet_client_sdk.openapi.Client;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ff_dotnet_wasm_client_sdk.client;

// threads and blocking in Wasm are disallowed by the browser sandbox so this client SDK implements a version of
// the SDK that uses Async methods only and no background threads. Cache is populated on demand when calling
// one of the xVariation SDK entry point functions (e.g. boolVariation) but only if key is older than 60 seconds
// (configurable via PollInterval) or it does not exist. Metrics are updated by a timer. It should be noted that this
// design is different to regular SDKs that typically avoid network activity in the xVariation functions for speed.

public class FfClient : IDisposable
{
    internal static readonly string SdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
    internal static readonly string HarnessSdkInfoHeader = ".NETBlazorWasm " + SdkVersion + " Client";
    internal static readonly string UserAgentHeader = ".NET/" + SdkVersion;
    internal const int DefaultTimeoutMs = 60_000;

    private readonly ILogger<FfClient> _logger;
    private readonly string _apiKey;
    private readonly FfConfig _config;
    private readonly FfTarget _target;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache1 = new();
    private readonly ConcurrentDictionary<string, Evaluation> _cache = new();
    private readonly System.Timers.Timer _pollTimer;

    private ClientApi? _api;
    private AuthInfo? _authInfo;
    private MetricsTimer? _metricsTimer;

    public FfClient(string apiKey, FfConfig config, FfTarget target)
    {
        _logger = config.LoggerFactory.CreateLogger<FfClient>();
        _apiKey = apiKey;
        _config = config;
        _target = target;
        _pollTimer = new System.Timers.Timer();
        _pollTimer.Elapsed += Poll;
        var i = TimeSpan.FromSeconds(config.MetricsIntervalInSeconds).TotalMilliseconds;
        _pollTimer.Interval = i;
        _pollTimer.AutoReset = true;
    }

    public async Task InitializeAsync()
    {
        _api = MakeClientApi();
        _authInfo = await AuthenticateAsync(_apiKey, _config, _target);

        if (_config.AnalyticsEnabled)
        {
            _metricsTimer = new MetricsTimer(_config);
        }

        await PollOnce();

        _pollTimer.Enabled = true;

        SdkCodes.InfoSdkAuthOk(_logger, SdkVersion);
    }

    private async Task<AuthInfo> AuthenticateAsync(string apiKey, FfConfig config, FfTarget target)
    {
        var authTarget = new AuthenticationRequestTarget(target.Identifier, target.Name, false, target.Attributes);
        var authRequest = new AuthenticationRequest(apiKey, authTarget);

        var authResp = await _api.AuthenticateAsync(authRequest);

        var jwtToken = (JwtSecurityToken) new JwtSecurityTokenHandler().ReadToken(authResp.AuthToken);;
        var accountId = jwtToken.Payload.TryGetValue("accountID", out var value) ? value.ToString() : "";
        var environment = jwtToken.Payload["environment"].ToString();
        var cluster = jwtToken.Payload["clusterIdentifier"].ToString();
        var environmentIdentifier = jwtToken.Payload.TryGetValue("environmentIdentifier", out value) ? value.ToString() : environment;
        var project = jwtToken.Payload.TryGetValue("project", out value) ? value.ToString() : "";
        var org = jwtToken.Payload.TryGetValue("organization", out value) ? value.ToString() : "";
        var projectId = jwtToken.Payload.TryGetValue("projectIdentifier", out value) ? value.ToString() : "";

        _api.Configuration.DefaultHeaders.Clear();
        _api.Configuration.DefaultHeaders.Add("Authorization", "Bearer " + authResp.AuthToken);
        _api.Configuration.DefaultHeaders.Add("Harness-EnvironmentID", environmentIdentifier);
        _api.Configuration.DefaultHeaders.Add("Harness-AccountID", accountId);
        _api.Configuration.DefaultHeaders.Add("User-Agent", UserAgentHeader);
        _api.Configuration.DefaultHeaders.Add("Harness-SDK-Info", HarnessSdkInfoHeader);

        var authInfo = new AuthInfo
        {
            Project = project,
            ProjectIdentifier = projectId,
            AccountId = accountId,
            Environment = environment,
            ClusterIdentifier = cluster,
            EnvironmentIdentifier = environmentIdentifier,
            Organization = org,
            BearerToken = authResp.AuthToken,
            ApiKey = apiKey
        };

        return authInfo;
    }

    public bool IsAuthenticated()
    {
        return _authInfo != null;
    }

    private void Poll(object sender, ElapsedEventArgs e)
    {
        _ = PollOnce();
    }

    private async Task PollOnce()
    {
        if (_config.Debug)
            _logger.LogInformation("Polling for flags");

        if (_authInfo == null)
        {
            _logger.LogInformation("Polling failed - SDK not authenticated");
            return;
        }

        try
        {
            var evaluations = await
                _api.GetEvaluationsAsync(_authInfo.Environment, _target.Identifier, _authInfo.ClusterIdentifier);

            foreach (var eval in evaluations)
            {
                RepoSetEvaluation(_authInfo.EnvironmentIdentifier, eval.Flag, eval);
                _logger.LogTrace("EnvId={EnvironmentIdentifier} Flag={Flag} Value={Value}", _authInfo.EnvironmentIdentifier, eval.Flag, eval.Value);
            }

            if (_config.Debug)
                _logger.LogInformation("Polling got {FlagCount} flags", evaluations.Count);
        }
        catch (Exception ex)
        {
            SdkCodes.LogUtils.LogException(_config, ex);
        }
    }

    private void RepoSetEvaluation(string? environmentIdentifier, string flag, Evaluation eval)
    {
        var key = MakeCacheKey(environmentIdentifier, flag);
        _cache.AddOrUpdate(key, eval, (_, _) => eval);
        _logger.LogTrace("Added key {CacheKey} to cache. New cache size: {CacheSize}", key, _cache.Count);
    }

    /* note that the xVariationAsync aren't truly async and just return immediately,
       we return Task<T> in case we want to change that behaviour in the future without breaking the API. */

    public Task<bool> BoolVariationAsync(string evaluationId, bool defaultValue)
    {
        return Task.FromResult(XVariation(evaluationId, defaultValue, eval => bool.Parse(eval.Value)));
    }

    public Task<string> StringVariationAsync(string evaluationId, string defaultValue)
    {
        return Task.FromResult(XVariation(evaluationId, defaultValue, eval => eval.Value));
    }

    public Task<double> NumberVariationAsync(string evaluationId, double defaultValue)
    {
        return Task.FromResult(XVariation(evaluationId, defaultValue, eval => double.Parse(eval.Value)));
    }

    public Task<JObject> JsonVariationAsync(string evaluationId, JObject defaultValue)
    {
        return Task.FromResult(XVariation<JObject>(evaluationId, defaultValue, eval => JObject.Parse(eval.Value)));
    }


    private T XVariation<T>(string evaluationId, T defaultValue, Func<Evaluation, T> evalToPrimitive)
    {
        var defaultValueStr = defaultValue?.ToString() ?? "null";

        if (_authInfo == null) {
            SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValueStr, "SDK not authenticated");
            return defaultValue;
        }

        var failureReason = new StringBuilder();
        var key = MakeCacheKey(_authInfo.EnvironmentIdentifier, evaluationId);
        var evaluation = _cache.TryGetValue(key, out var eval) ? eval : null;

        if (evaluation == null || string.IsNullOrEmpty(evaluation.Value))
        {
            failureReason.Append(evaluationId).Append(" not in cache");
            SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValueStr, failureReason.ToString());
            return defaultValue;
        }

        RegisterEvaluation(evaluationId, evaluation);

        return evalToPrimitive.Invoke(evaluation);
    }

    public async Task<bool> BoolVariationAsync2(string evaluationId, bool defaultValue)
    {
        return await XVariationAsync2(evaluationId, defaultValue, eval => bool.Parse(eval.Value));
    }

    public async Task<string> StringVariationAsync2(string evaluationId, string defaultValue)
    {
        return await XVariationAsync2(evaluationId, defaultValue, eval => eval.Value);
    }

    public  async Task<double> NumberVariationAsync2(string evaluationId, double defaultValue)
    {
        return await XVariationAsync2(evaluationId, defaultValue, eval => double.Parse(eval.Value));
    }

    public  async Task<JObject> JsonVariationAsync2(string evaluationId, JObject defaultValue)
    {
        return await XVariationAsync2<JObject>(evaluationId, defaultValue, eval => JObject.Parse(eval.Value));
    }

    private async Task<T> XVariationAsync2<T>(string evaluationId, T defaultValue, Func<Evaluation, T> evalToPrimitive)
    {
        if (_authInfo == null) {
            SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValue?.ToString() ?? "null", "SDK not authenticated");
            return defaultValue;
        }

        var key = MakeCacheKey(_authInfo.EnvironmentIdentifier, evaluationId);

        try
        {
            var keyExists = _cache1.TryGetValue(key, out var cacheEntry);
            var elapsed = keyExists ? (DateTime.Now - (cacheEntry?.LastUpdated ?? DateTime.Now)).Seconds : 0;

            if (cacheEntry == null || elapsed >= 59)
            {
                if (_config.Debug)
                    _logger.LogInformation("Key {CacheKey} not in cache or expired, query server for evaluation {EvaluationId}", key, evaluationId);

                // flag not cached or cached entry out of date, refresh it from server
                var evaluation = await _api.GetEvaluationByIdentifierAsync(_authInfo.Environment,
                    evaluationId, _target.Identifier, _authInfo.ClusterIdentifier);

                cacheEntry = new CacheEntry(true, 200, "ok", evaluation, DateTime.Now);
                _cache1[key] = cacheEntry;
            }

            if (!cacheEntry.IsSuccess() || cacheEntry.Eval == null)
            {
                _logger.LogInformation($"Evaluation {evaluationId} found in cache with failed status {cacheEntry.StatusCode}:{cacheEntry.StatusMsg}");

                SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValue?.ToString() ?? "null",
                    $"Evaluation returned status {cacheEntry.StatusCode}:{cacheEntry.StatusMsg} {elapsed} seconds ago");
                return defaultValue;
            }

            _logger.LogInformation("Evaluation {EvaluationId} found in cache", evaluationId);

            RegisterEvaluation(evaluationId, cacheEntry.Eval);
            return evalToPrimitive.Invoke(cacheEntry.Eval);

        }
        catch (Exception ex)
        {
            var statusCode = 0;
            if (ex is ApiException apiEx)
                statusCode = apiEx.ErrorCode;

            // If we get an exception on the given key then cache the key with the status code/error msg, so we don't repeatedly hit the BE
            _cache1[key] = new CacheEntry(false, statusCode, ex.Message, null, DateTime.Now);

            SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValue?.ToString() ?? "null", ex.Message);
            SdkCodes.LogUtils.LogException(_config, ex);
            return defaultValue;
        }
    }

    private void RegisterEvaluation(string evaluationId, Evaluation evaluation)
    {
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _metricsTimer?.Dispose();
        _api?.Dispose();
    }


    private static string MakeCacheKey(string? environmentIdentifier, string flag)
    {
        return new StringBuilder().Append(environmentIdentifier).Append('_').Append(flag).ToString();
    }


    private ClientApi MakeClientApi()
    {
        var httpClientHandler = new HttpClientHandler();
        var httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromMilliseconds(DefaultTimeoutMs),
        };

        return new ClientApi(httpClient, _config.ConfigUrl, httpClientHandler);
    }


}