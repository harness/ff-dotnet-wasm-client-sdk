using System.Net.Http.Headers;
using System.Text;
using ff_dotnet_wasm_client_sdk.client.impl.dto;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ff_dotnet_wasm_client_sdk.client.impl
{

    interface IEventSourceListener
    {
        /// SSE stream started ok
        Task SseStart();
        /// SSE stream ended, if stream ended because of an error then cause will be non-null
        Task SseEnd(string reason, Exception? cause);
        /// Indicates callback to server required to get flag state
        Task SseEvaluationChange(string identifier);
        /// Event includes evaluations payload, cache can be updated immediately with no callback
        Task SseEvaluationsUpdate(List<Evaluation> evaluations);
        /// Indicates flag removal
        Task SseEvaluationRemove(string identifier);
        /// Indicates creation, change or removal of a target group we want to reload evaluations
        Task SseEvaluationReload(List<Evaluation> evaluations);

    }

    internal class EventSource : IDisposable
    {
        private readonly ILogger<EventSource> _logger;
        private readonly string _url;
        private readonly FfConfig _config;
        private readonly HttpClient _httpClient;
        private readonly IEventSourceListener _callback;
        private readonly AuthInfo _authInfo;
        private const int ReadTimeoutMs = 60_000;
        private Task? _eventReceiverTask;

        internal EventSource(AuthInfo authInfo, string url, FfConfig config, IEventSourceListener callback, ILoggerFactory loggerFactory)
        {
            _authInfo = authInfo;
            _httpClient = MakeHttpClient(authInfo, config, loggerFactory);
            _url = url;
            _config = config;
            _callback = callback;
            _logger = loggerFactory.CreateLogger<EventSource>();
        }

        private static HttpClient MakeHttpClient(AuthInfo authInfo, FfConfig config, ILoggerFactory loggerFactory)
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMilliseconds(FfClient.DefaultTimeoutMs);
            client.DefaultRequestHeaders.Add("API-Key", authInfo.ApiKey);
            AddSdkHeaders(client.DefaultRequestHeaders, authInfo);
            return client;
        }

        private static void AddSdkHeaders(HttpRequestHeaders httpRequestHeaders, AuthInfo authInfo)
        {
            var headers = new Dictionary<string, string>();
            FfClient.AddSdkHeaders(headers, authInfo);

            foreach (var keyPair in headers)
            {
                httpRequestHeaders.Add(keyPair.Key, keyPair.Value);
            }
        }

        private static async Task<string?> ReadLineAsync(Stream stream, CancellationTokenSource cts)
        {
            var builder = new StringBuilder();
            var nextByte = new byte[1];
            do
            {
                var bytesGot = await stream.ReadAsync(nextByte, cts.Token);
                if (bytesGot == 0)
                {
                    return null;
                }

                builder.Append((char)nextByte[0]);
            } while (nextByte[0] != 10);

            return builder.ToString();
        }

        internal void Start()
        {
            _eventReceiverTask = StreamEventReceiverTask();
        }

        private async Task StreamEventReceiverTask()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(60_000));

                cts.Token.Register(() => _logger.LogWarning("stream cancellation requested"));

                using var req = new HttpRequestMessage(HttpMethod.Get, _url);
                AddSdkHeaders(req.Headers, _authInfo);
                req.SetBrowserResponseStreamingEnabled(true);
                //req.SetBrowserRequestMode(BrowserRequestMode.Cors);

                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);

                await _callback.SseStart();

                while (!cts.Token.IsCancellationRequested)
                {
                    while (await ReadLineAsync(stream, cts) is { } message)
                    {
                        if (!message.Contains("domain"))
                        {
                            _logger.LogTrace("Received event source heartbeat");
                            continue;
                        }

                        SdkCodes.InfoStreamEventReceived(_logger, message);

                        var jsonMessage = JObject.Parse("{" + message + "}");
                        await ProcessMessage(jsonMessage["data"]);
                    }
                }
                await _callback.SseEnd("End of stream", null);
            }
            catch (Exception e)
            {
                SdkCodes.LogUtils.LogExceptionAndWarn(_logger, _config, "EventSource threw an error: " + e.Message, e);
                await _callback.SseEnd(e.Message, e);
            }

        }

        private async Task ProcessMessage(JToken? data)
        {
            if (data == null)
            {
                return;
            }

            var domain = data["domain"]?.ToString() ?? "";
            var eventType = data["event"]?.ToString() ?? "";
            var identifier = data["identifier"]?.ToString() ?? "";

            if ("target-segment".Equals(domain))
            {
                // On creation, change or removal of a target group we want to reload evaluations
                if ("delete".Equals(eventType) || "patch".Equals(eventType) || "Equals".Equals(eventType))
                {
                    await _callback.SseEvaluationReload(new List<Evaluation>());
                }
            }
            else if ("flag".Equals(domain))
            {
                // On creation or change of a flag we want to send a change event
                if ("create".Equals(eventType) || "patch".Equals(eventType))
                {
                    await _callback.SseEvaluationChange(identifier);
                }
                // On deletion of a flag we want to send a remove event
                else if ("delete".Equals(eventType))
                {
                    await _callback.SseEvaluationRemove(identifier);
                }
            }
        }

        public void Dispose()
        {
            _eventReceiverTask?.Wait();
            _eventReceiverTask?.Dispose();
            _httpClient.Dispose();
        }

    }
}