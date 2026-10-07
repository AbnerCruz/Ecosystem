using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Urbe.UI;

namespace Urbe.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<WorkspaceSession>();
        return builder.Build();
    }
}
