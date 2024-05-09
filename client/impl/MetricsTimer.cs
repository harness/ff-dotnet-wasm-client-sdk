using System.Timers;
using Microsoft.Extensions.Logging;

namespace ff_dotnet_wasm_client_sdk.client;

internal class MetricsTimer : IDisposable
{
    private readonly FfConfig _config;
    private readonly System.Timers.Timer _timer;
    private readonly ILogger<MetricsTimer> _logger;

    internal MetricsTimer(FfConfig config)
    {
        _config = config;
        _logger = config.LoggerFactory.CreateLogger<MetricsTimer>();

        _timer = new System.Timers.Timer();
        _timer.Elapsed += PostMetrics;
        _timer.Interval = TimeSpan.FromSeconds(config.MetricsIntervalInSeconds).TotalMilliseconds;
        _timer.AutoReset = true;
        _timer.Enabled = true;
        _logger.LogInformation("Metrics started");

    }

    internal void PostMetrics(Object source, ElapsedEventArgs e)
    {
        if (_config.Debug)
            _logger.LogInformation("Posting metrics");
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        //_api.Dispose();
    }
}