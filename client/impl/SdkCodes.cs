using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ff_dotnet_wasm_client_sdk.client.impl;

internal static class SdkCodes
{
    internal static void InfoPollingStopped(ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("SDKCODE(poll:4001): Polling stopped");
    }

    internal static void ErrorMissingSdkKey(ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Error))
            logger.LogError("SDKCODE(init:1002): Missing or empty API key");
    }

    internal static void InfoSdkAuthOk(ILogger logger, string sdkVersion)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("SDKCODE(auth:2000): Authenticated ok, version={Version}", sdkVersion);
    }

    internal static void InfoStreamConnected(ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("SDKCODE(stream:5000): SSE stream connected ok");
    }

    internal static void InfoStreamStopped(ILogger logger, string reason)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("SDKCODE(stream:5001): SSE stream disconnected, reason: {Reason}", reason);
    }

    internal static void InfoStreamEventReceived(ILogger logger, string eventJson)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("SDKCODE(stream:5002): SSE event received: {EventJson}", eventJson);
    }

    internal static void WarnDefaultVariationServed(ILogger logger, string evaluationId, string defaultValue, string reason)
    {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("SDKCODE(auth:6001): Default variation was served. Identifier={EvaluationId}, DefaultServed={Default} Reason={Reason}", evaluationId, defaultValue, reason);
    }

    internal static void InfoMetricsThreadStarted(ILogger logger, int intervalInSeconds)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("SDKCODE(metric:7000): Metrics thread started, intervalInSeconds={IntervalInSeconds}", intervalInSeconds);
    }

    internal static void InfoMetricsThreadExited(ILogger logger)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("SDKCODE(metric:7001): Metrics thread exited");
    }

    internal static void WarnPostingMetricsFailed(ILogger logger, string reason)
    {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("SDKCODE(stream:7002): Posting metrics failed, reason: {Reason}", reason);
    }

    public static void WarnMetricsBufferFull(ILogger logger, int droppedEvaluations)
    {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("SDKCODE(stream:7008): Metrics buffer is full and metrics will be discarded. Dropped Count={DroppedEvaluations}", droppedEvaluations);
    }

    internal static class LogUtils
    {
        internal static void LogExceptionAndWarn(ILogger logger, FfConfig config, string message, Exception exception)
        {
            logger.LogWarning("{Message}: {ExceptionMessage}", message, exception.Message);

            if (config.Debug)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        internal static void LogExceptionAndInfo(ILogger logger, FfConfig config, string message, Exception exception)
        {
            logger.LogInformation("{Message}: {ExceptionMessage}", message, exception.Message);

            if (config.Debug)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        public static void LogException(FfConfig config, Exception exception)
        {
            if (config.Debug)
            {
                Console.WriteLine(exception.ToString());
            }
        }

    }


}