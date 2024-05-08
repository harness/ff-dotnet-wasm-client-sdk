using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Abstractions;

namespace ff_dotnet_wasm_client_sdk.client;

public class FfConfig
{
    public string ConfigUrl { get; }
    public string EventUrl { get; }
    public int PollIntervalInSeconds { get; }
    public int MetricsIntervalInSeconds { get; }

    public bool StreamEnabled { get; }
    public bool AnalyticsEnabled { get; }

    public int MetricsCapacity { get; }
    public bool Debug { get; }

    public ILoggerFactory LoggerFactory { get; }

    internal FfConfig(string configUrl, string eventUrl, int pollIntervalInSeconds, int metricsIntervalInSeconds, bool streamEnabled, bool analyticsEnabled, int metricsCapacity, bool debug, ILoggerFactory loggerFactory)
    {
        ConfigUrl = configUrl;
        EventUrl = eventUrl;
        PollIntervalInSeconds =  Math.Max(60, pollIntervalInSeconds);
        MetricsIntervalInSeconds = Math.Max(60, metricsIntervalInSeconds);
        StreamEnabled = streamEnabled;
        AnalyticsEnabled = analyticsEnabled;
        MetricsCapacity = metricsCapacity;
        Debug = debug;
        LoggerFactory = loggerFactory;
    }

    public static ConfigBuilder Builder()
    {
        return new ConfigBuilder();
    }
}

public class ConfigBuilder
{
    private const int DefaultMetricsCapacity = 1024;
    private const int MinIntervalSeconds = 60;
    private string _configUrl = "https://config.ff.harness.io/api/1.0";
    private string _eventUrl = "https://events.ff.harness.io/api/1.0";
    private int _pollIntervalInSeconds = MinIntervalSeconds;
    private int _metricsIntervalInSeconds = MinIntervalSeconds;
    private bool _streamEnabled = true;
    private bool _analyticsEnabled = true;
    private int _metricsCapacity = DefaultMetricsCapacity;
    private bool _debug = false;
    private ILoggerFactory _loggerFactory = new NullLoggerFactory();

    public FfConfig Build()
    {
        return new FfConfig(_configUrl, _eventUrl, _pollIntervalInSeconds, _metricsIntervalInSeconds, _streamEnabled, _analyticsEnabled, _metricsCapacity, _debug, _loggerFactory);
    }

    public ConfigBuilder SetPollingInterval(int pollIntervalInSeconds)
    {
        _pollIntervalInSeconds = Math.Max(MinIntervalSeconds, pollIntervalInSeconds);
        return this;
    }

    public ConfigBuilder SetMetricsInterval(int metricsIntervalInSeconds)
    {
        _metricsIntervalInSeconds = Math.Max(MinIntervalSeconds, metricsIntervalInSeconds);
        return this;
    }

    public ConfigBuilder SetStreamEnabled(bool enabled = true)
    {
        _streamEnabled = enabled;
        return this;
    }

    public ConfigBuilder SetAnalyticsEnabled(bool analyticsEnabled = true)
    {
        _analyticsEnabled = analyticsEnabled;
        return this;
    }

    public ConfigBuilder MetricsCapacity(int metricsCapacity)
    {
        _metricsCapacity = Math.Max(DefaultMetricsCapacity, metricsCapacity);
        return this;
    }

    public ConfigBuilder ConfigUrl(string configUrl)
    {
        _configUrl = configUrl;
        return this;
    }

    public ConfigBuilder EventUrl(string eventUrl)
    {
        _eventUrl = eventUrl;
        return this;
    }

    public ConfigBuilder Debug(bool debug)
    {
        _debug = debug;
        return this;
    }


    public ConfigBuilder LoggerFactory(ILoggerFactory factory)
    {
        _loggerFactory = factory;
        return this;
    }

    public interface INetworkChecker
    {
        bool IsNetworkAvailable();
    }

    internal class NullNetworkChecker : INetworkChecker {
        public bool IsNetworkAvailable()
        {
            return true;
        }
    }

}