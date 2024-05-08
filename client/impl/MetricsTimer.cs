using System.Collections.Concurrent;
using System.Net.Http;
using System.Timers;
using ff_dotnet_wasm_client_sdk.client.dto;
using ff_dotnet_wasm_client_sdk.client.impl;
using io.harness.ff_dotnet_client_sdk.client.impl.dto;
using io.harness.ff_dotnet_client_sdk.openapi.Api;
using io.harness.ff_dotnet_client_sdk.openapi.Client;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.Extensions.Logging;

namespace ff_dotnet_wasm_client_sdk.client;

internal class MetricsTimer : IDisposable
{
    private const string FeatureIdentifierAttribute = "featureIdentifier";
    private const string FeatureNameAttribute = "featureName";
    private const string VariationIdentifierAttribute = "variationIdentifier";
    private const string TargetAttribute = "target";
    private const string SdkType = "SDK_TYPE";
    private const string Client = "client";
    private const string SdkLanguage = "SDK_LANGUAGE";
    private const string SdkVersion = "SDK_VERSION";
    private const int MaxFreqMapToRetain = 10_000;
    private readonly System.Timers.Timer _timer;
    private readonly ILogger<MetricsTimer> _logger;

    private readonly FfTarget _target;
    private readonly FfConfig _config;
    private readonly int _maxFreqMapSize;
    private readonly FrequencyMap<Analytics> _frequencyMap = new();
    private readonly MetricsApi _api;
    private readonly AuthInfo _authInfo;
    private int _evalCounter;
    private int _metricsEvaluationsDropped;
    private readonly ConfigBuilder.INetworkChecker _networkChecker = new ConfigBuilder.NullNetworkChecker();



    internal MetricsTimer(FfTarget target, FfConfig config, ILoggerFactory loggerFactory, AuthInfo? authInfo)
    {
        _target = target;
        _config = config;
        _logger = config.LoggerFactory.CreateLogger<MetricsTimer>();

        _timer = new System.Timers.Timer();
        _timer.Elapsed += PostMetrics;
        _timer.Interval = TimeSpan.FromSeconds(config.MetricsIntervalInSeconds).TotalMilliseconds;
        _timer.AutoReset = true;
        _timer.Enabled = true;

        _maxFreqMapSize = Clamp(config.MetricsCapacity, 2048, MaxFreqMapToRetain);
        _api = MakeClientApi(authInfo, loggerFactory);
        _authInfo = authInfo;
        SdkCodes.InfoMetricsThreadStarted(_logger, _config.MetricsIntervalInSeconds);

    }

    private static int Clamp(int value, int lower, int higher) {
        return Math.Max(lower, Math.Min(higher, value));
    }

    internal void PostMetrics(Object source, ElapsedEventArgs e)
    {
        if (_config.Debug)
            _logger.LogInformation("Posting metrics");

        try
        {
            if (_config.Debug)
                _logger.LogInformation("Pushing metrics to server");

            if (_networkChecker.IsNetworkAvailable())
            {
                _ = FlushMetrics();
            }
            else
            {
                _logger.LogInformation("Network is offline, skipping metrics post");
            }
        }
        catch (ApiException ex)
        {
            SdkCodes.WarnPostingMetricsFailed(_logger, "HTTP code " + ex.ErrorCode);
            SdkCodes.LogUtils.LogException(_config, ex);
        }
        catch (Exception ex)
        {
            SdkCodes.WarnPostingMetricsFailed(_logger, ex.Message);
            SdkCodes.LogUtils.LogException(_config, ex);
        }
    }

    internal void RegisterEvaluation(string evaluationId, Variation variation)
    {
        var analytics = new Analytics(_target, evaluationId, variation);

        if (_frequencyMap.ContainsKey(analytics) && _frequencyMap.Count() + 1 > _maxFreqMapSize)
        {
            Interlocked.Increment(ref _metricsEvaluationsDropped);
        }
        else
        {
            _frequencyMap.Increment(analytics);
        }

        Interlocked.Increment(ref _evalCounter);
    }

    private async Task FlushMetrics()
    {
        var droppedEvaluations = Interlocked.Exchange(ref _metricsEvaluationsDropped, 0);

        if (droppedEvaluations > 0)
        {
            SdkCodes.WarnMetricsBufferFull(_logger, droppedEvaluations);
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Running metrics thread iteration. frequencyMapSize={Count}", _frequencyMap.Count());
        }

        var metricsSnapshot = _frequencyMap.DrainToDictionary();

        if (metricsSnapshot.Count <= 0) return;
        var metrics = PrepareMessageBody(metricsSnapshot);
        if (metrics.MetricsData.Sum(md => md.Count) <= 0 && metrics.TargetData.Count <= 0) return;
        await PostMetrics(metrics);
    }

    private async Task PostMetrics(Metrics metrics)
    {
        await _api.PostMetricsAsync(_authInfo.Environment,  _authInfo.ClusterIdentifier, metrics);
    }

    private long GetCurrentUnixTimestampMillis()
    {
        return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }

    private void SetMetricsAttributes(MetricsData metricsData, string key, string value)
    {
        var metricsAttributes = new KeyValue(key, value);
        metricsData.Attributes.Add(metricsAttributes);
    }

    private Metrics PrepareMessageBody(IDictionary<Analytics, Int64> data)
    {
        var metrics = new Metrics();
        metrics.TargetData = new List<TargetData>();
        metrics.MetricsData = new List<MetricsData>();

        foreach (var analytic in data)
        {
            var metricsData = new MetricsData(GetCurrentUnixTimestampMillis(), (int) analytic.Value, MetricsData.MetricsTypeEnum.FFMETRICS, new());

            SetMetricsAttributes(metricsData, FeatureIdentifierAttribute, analytic.Key.Variation.Name);
            SetMetricsAttributes(metricsData, FeatureNameAttribute, analytic.Key.Variation.Name);
            SetMetricsAttributes(metricsData, VariationIdentifierAttribute, analytic.Key.Variation.Identifier);
            SetMetricsAttributes(metricsData, TargetAttribute, analytic.Key.Target.Identifier);
            SetMetricsAttributes(metricsData, SdkType, Client);
            SetMetricsAttributes(metricsData, SdkLanguage, ".NET");
            SetMetricsAttributes(metricsData, SdkVersion, FfClient.SdkVersion);
            metrics.MetricsData.Add(metricsData);
        }

        return metrics;
    }

    private class FrequencyMap<TK>
    {
        private readonly ConcurrentDictionary<TK, Int64> _freqMap = new();

        internal void Increment(TK key)
        {
            _freqMap.AddOrUpdate(key, 1, (_, v) => v + 1);
        }

        internal int Count()
        {
            return _freqMap.Count;
        }

        internal Int64 Sum()
        {
            return _freqMap.Values.Sum(v => v);
        }

        internal Dictionary<TK, Int64> DrainToDictionary()
        {
            Dictionary<TK, Int64> snapshot = new();

            // Take a snapshot of the ConcurrentDictionary atomically setting each key's value to 0 as we copy it
            foreach (var kvPair in _freqMap)
            {
                _freqMap.AddOrUpdate(kvPair.Key, 0, (k, v) =>
                {
                    snapshot.Add(k, v);
                    return 0;
                });
            }

            // Clean up entries with 0
            foreach (var kvPair in snapshot)
            {
                // ConcurrentDictionary doesn't have a RemoveIf value==0
                if (_freqMap.TryGetValue(kvPair.Key, out var existingVal))
                {
                    if (existingVal == 0)
                    {
                        _freqMap.TryRemove(kvPair.Key, out _);
                    }
                }
            }

            return snapshot;
        }

        internal bool ContainsKey(TK key)
        {
            return _freqMap.ContainsKey(key);
        }

    }

    private MetricsApi MakeClientApi(AuthInfo authInfo, ILoggerFactory loggerFactory)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(_config.EventUrl);
        client.Timeout = TimeSpan.FromMilliseconds(FfClient.DefaultTimeoutMs);
        var api = new MetricsApi(client, _config.EventUrl);
        api.Configuration.DefaultHeaders.Clear();
        FfClient.AddSdkHeaders(api.Configuration.DefaultHeaders, authInfo);
        return api;
    }

    public void Dispose()
    {
        // await FlushMetrics();

        SdkCodes.InfoMetricsThreadExited(_logger);

        _timer.Stop();
        _timer.Dispose();
        _api.Dispose();
    }

}