using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Urbe.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        return builder.Build();
    }
}
