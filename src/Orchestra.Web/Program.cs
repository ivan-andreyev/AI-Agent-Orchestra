using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Orchestra.Web;
using Orchestra.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Настраиваем HttpClient для API
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5002") });
builder.Services.AddScoped<OrchestratorService>();

await builder.Build().RunAsync();
