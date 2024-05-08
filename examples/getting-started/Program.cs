// See https://aka.ms/new-console-template for more information

// This is a basic command line example, see *-wasm examples for Web Assembly integration examples

using ff_dotnet_wasm_client_sdk.client;
using ff_dotnet_wasm_client_sdk.client.dto;
using Serilog;
using Serilog.Extensions.Logging;


var apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
var flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") ?? "harnessappdemodarkmode";

if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(flagName)) throw new Exception("Please set FF_API_KEY and FF_FLAG_NAME");

var loggerFactory = new SerilogLoggerFactory(
    new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console()
        .CreateLogger());

var target = new FfTarget("dotNETwasm-getstarted", "dotNETwasm-getstarted",
    new Dictionary<string, string> { { "email", "person@myorg.com" }});

var config = FfConfig.Builder().LoggerFactory(loggerFactory).Debug(true).Build();

using var client = new FfClient(apiKey, config, target);
await client.InitializeAsync();


for (var i = 1; i < 100; i++)
{
    var value = await client.BoolVariationAsync(flagName, false);
    Console.Out.WriteLine("flag {0} = {1}", flagName, value);

    Thread.Sleep(TimeSpan.FromSeconds(1));
}


