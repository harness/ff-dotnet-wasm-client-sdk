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
    private bool _streamFailed;

    private class StreamSourceListener : IEventSourceListener
    {
        private ILogger<StreamSourceListener> _logger;


        internal StreamSourceListener(ILoggerFactory factory)
        {
            _logger = factory.CreateLogger<StreamSourceListener>();
        }

        public void SseStart()
        {
            _logger.LogInformation("SseStart");
        }

        public void SseEnd(string reason, Exception? cause)
        {
            _logger.LogInformation("SseEnd");
        }

        public void SseEvaluationChange(string identifier)
        {
            _logger.LogInformation("SseEvaluationChange");
        }

        public void SseEvaluationsUpdate(List<Evaluation> evaluations)
        {
            _logger.LogInformation("SseEvaluationsUpdate");
        }

        public void SseEvaluationRemove(string identifier)
        {
            _logger.LogInformation("SseEvaluationRemove");
        }

        public void SseEvaluationReload(List<Evaluation> evaluations)
        {
            _logger.LogInformation("SseEvaluationReload");
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
            var listener = new StreamSourceListener(_config.LoggerFactory);
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



        if (_config.StreamEnabled && _streamFailed)
        {
            // TODO if the stream failed restart it here, or in eventsource itself
        }

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