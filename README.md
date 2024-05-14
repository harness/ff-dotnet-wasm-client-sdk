# ff-dotnet-wasm-client-sdk
FF .NET client SDK for Blazor WebAssembly

# Overview
This SDK allows you to run a Feature Flags client inside a WebAssembly (Wasm) Virtual Machine on a browser. For a detailed overview of Wasm see [MDN web docs](https://developer.mozilla.org/en-US/docs/WebAssembly)
It supports polling, metrics and streaming via `Microsoft.AspNetCore.Components.WebAssembly.Http.HttpRequestMessage` and uses a non-threaded design based async/await C# methods and timers for compatibility with the limited .NET APIs available in the VM.


# Which SDK to use?

Harness Feature flags provide several .NET SDKs. Broadly speaking a client SDK serves one end user (or target) whereas a server SDK handles many targets.
A client SDK typically requires less CPU and network bandwith as they don't process rules locally, so are more suited to web browsers, phone and desktop apps.
Server SDKs are designed for server environments, since rule config is pulled the SDK API key should be treated as a backend secret and not exposed.

## ff-dotnet-wasm-client-sdk

This repository. This is a client SDK - it is designed to run client-side inside the web browser's WebAssembly Virtual Machine.
It imports some wasm specific modules for streaming support.

## ff-dotnet-client-client-sdk

This is a generic .NET C# client SDK designed for desktop and mobule apps via MAUI. See the [Github repository](https://github.com/harness/ff-dotnet-client-sdk)

## ff-dotnet-server-client-sdk

Our server side .NET C# server SDK, designed for server environments. See the [Github repository](https://github.com/harness/ff-dotnet-server-sdk)

# Getting started

Some sample applications are provided to help you test the SDK for the first time.
You will need to create a boolean flag called `harnessappdemodarkmode` and have a client API key for the SDK to authenticate. Edit FeatureFlagsContext.cs and add your client API to `_apiKey`.
Make sure you have the WASM workflows installed for your particular development environment. This is not described here but you can find more information from Microsoft at [Blazor WebAssembly build tools](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-8.0).


The `dotnet watch` command will open up a browser:

```bash
cd examples/blazor-wasm
dotnet watch
```