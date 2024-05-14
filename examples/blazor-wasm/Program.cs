using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using blazor_wasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<IFeatureFlagsContext>(sp =>
    {
        var ff = new FeatureFlagsContext(sp.GetRequiredService<ILoggerFactory>());
        ff.Init();
        return ff;
    });

await builder.Build().RunAsync();