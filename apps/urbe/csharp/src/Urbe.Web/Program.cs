using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Urbe.UI;
using Urbe.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton<WorkspaceSession>();
builder.Services.AddSingleton<IVaultHost, PreviewVaultHost>();
await builder.Build().RunAsync();
