using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;
using System.Timers;
using ff_dotnet_wasm_client_sdk.client.dto;
using ff_dotnet_wasm_client_sdk.client.impl;
using ff_dotnet_wasm_client_sdk.client.impl.dto;
using io.harness.ff_dotnet_client_sdk.openapi.Api;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using LogUtils = ff_dotnet_wasm_client_sdk.client.impl.SdkCodes.LogUtils;

namespace ff_dotnet_wasm_client_sdk.client;

public class FfClient : IDisposable
{
    internal static readonly string SdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
    internal static readonly string HarnessSdkInfoHeader = ".NETBlazorWasm " + SdkVersion + " Client";
    internal static readonly string UserAgentHeader = ".NETBlazorWasm/" + SdkVersion;
    internal const int DefaultTimeoutMs = 60_000;

    private readonly ILogger<FfClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _apiKey;
    private readonly FfConfig _config;
    private readonly FfTarget _target;
    private readonly ConcurrentDictionary<string, Evaluation> _cache = new();
    private readonly System.Timers.Timer _pollTimer;

    private ClientApi? _api;
    private AuthInfo? _authInfo;
    private MetricsTimer? _metricsTimer;
    private EventSource? _eventSource;

    private class StreamSourceListener : IEventSourceListener
    {
        private readonly ILogger<StreamSourceListener> _logger;
        private readonly FfConfig _config;
        private readonly FfClient _ffClient;
        private readonly AuthInfo _authInfo;
        private readonly ClientApi _api;
        private readonly FfTarget _target;

        internal StreamSourceListener(FfClient ffClient, ClientApi api, AuthInfo authInfo, FfTarget target, FfConfig config)
        {
            _logger = config.LoggerFactory.CreateLogger<StreamSourceListener>();
            _ffClient = ffClient;
            _api = api;
            _authInfo = authInfo;
            _target = target;
            _config = config;
        }

        public async Task SseStart()
        {
            await _ffClient.PollOnce();
            SdkCodes.InfoStreamConnected(_logger);
        }

        public async Task SseEnd(string reason, Exception? cause)
        {
            SdkCodes.InfoStreamStopped(_logger, reason);
            if (cause != null)
            {
                LogUtils.LogExceptionAndWarn(_logger, _config, "Stream end exception", cause);
            }

            await _ffClient.PollOnce();
        }

        public async Task SseEvaluationChange(string identifier)
        {
            _logger.LogTrace("SSE Evaluation {identifier} changed, fetching flag from server",identifier);

            Evaluation evaluation = await _api.GetEvaluationByIdentifierAsync(_authInfo.Environment,
                identifier, _target.Identifier, _authInfo.ClusterIdentifier);

            _ffClient.RepoSetEvaluation(_authInfo.EnvironmentIdentifier, evaluation.Flag, evaluation);
        }

        public Task SseEvaluationsUpdate(List<Evaluation> evaluations)
        {
            _logger.LogTrace("SSE update {Count} evaluations from event", evaluations.Count);

            foreach (var evaluation in evaluations)
            {
                _ffClient.RepoSetEvaluation(_authInfo.EnvironmentIdentifier, evaluation.Flag, evaluation);
            }

            return Task.CompletedTask;
        }

        public Task SseEvaluationRemove(string identifier)
        {
            _logger.LogTrace("SSE Evaluation remove {Identifier}", identifier);
            _ffClient.RepoRemoveEvaluation(_authInfo.EnvironmentIdentifier, identifier);
            return Task.CompletedTask;
        }

        public Task SseEvaluationReload(List<Evaluation> evaluations)
        {
            _logger.LogInformation("SseEvaluationReload");
            return Task.CompletedTask;
        }
    }

    public FfClient(string apiKey, FfConfig config, FfTarget target)
    {
        _logger = config.LoggerFactory.CreateLogger<FfClient>();
        _apiKey = apiKey;
        _config = config;
        _target = target;
        _pollTimer = new System.Timers.Timer();
        _pollTimer.Elapsed += Poll;
        _pollTimer.Interval = TimeSpan.FromSeconds(config.MetricsIntervalInSeconds).TotalMilliseconds;
        _pollTimer.AutoReset = true;
    }

    public async Task InitializeAsync()
    {
        _api = MakeClientApi();
        _authInfo = await AuthenticateAsync(_apiKey, _config, _target);

        if (_config.AnalyticsEnabled)
        {
            _metricsTimer = new MetricsTimer(_target, _config, _config.LoggerFactory, _authInfo);
        }

        await PollOnce();

        if (_config.StreamEnabled)
        {
            var listener = new StreamSourceListener(this, _api, _authInfo, _target, _config);
            var streamUrl = _config.ConfigUrl + "/stream?cluster=" + _authInfo.ClusterIdentifier;
            _eventSource = new EventSource(_authInfo, streamUrl, _config, listener, _config.LoggerFactory);
            _eventSource.Start();
        }

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

        _api.Configuration.DefaultHeaders.Clear();
        AddSdkHeaders(_api.Configuration.DefaultHeaders, authInfo);


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
            LogUtils.LogException(_config, ex);
        }
    }

    private void RepoSetEvaluation(string? environmentIdentifier, string flag, Evaluation eval)
    {
        var key = MakeCacheKey(environmentIdentifier, flag);
        _cache.AddOrUpdate(key, eval, (_, _) => eval);
        _logger.LogTrace("Added key {CacheKey} to cache. New cache size: {CacheSize}", key, _cache.Count);
    }

    private void RepoRemoveEvaluation(string authInfoEnvironmentIdentifier, string evaluationFlag)
    {
        var key = MakeCacheKey(authInfoEnvironmentIdentifier, evaluationFlag);
        _cache.TryRemove(key, out _);
        _logger.LogTrace("Removed key {CacheKey} from cache. New cache size: {CacheSize}", key, _cache.Count);
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
            failureReason.Append(key).Append(" not in cache");
            SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValueStr, failureReason.ToString());
            return defaultValue;
        }

        RegisterEvaluation(evaluationId, evaluation);

        return evalToPrimitive.Invoke(evaluation);
    }

    private void RegisterEvaluation(string evaluationId, Evaluation evaluation)
    {
        if (_config?.AnalyticsEnabled ?? false)
        {
            Variation variation = new Variation(evaluation.Identifier, evaluation.Value, evaluationId);
            _metricsTimer?.RegisterEvaluation(evaluationId, variation);
        }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        _metricsTimer?.Dispose();
        _eventSource?.Dispose();
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

    internal static void AddSdkHeaders(IDictionary<string, string> headers, AuthInfo authInfo)
    {
        headers.Add("Authorization", "Bearer " + authInfo.BearerToken);
        headers.Add("Harness-SDK-Info", HarnessSdkInfoHeader);
        headers.Add("User-Agent", UserAgentHeader);

        if (!authInfo.AccountId.IsNullOrEmpty())
        {
            headers.Add("Harness-AccountID", authInfo.AccountId);
        }

        if (!authInfo.EnvironmentIdentifier.IsNullOrEmpty())
        {
            headers.Add("Harness-EnvironmentID", authInfo.EnvironmentIdentifier);
        }
    }
}