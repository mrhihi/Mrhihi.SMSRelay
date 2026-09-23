using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SmsRelay.Pages;
using SmsRelay.Services;

namespace SmsRelay;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IQueueService, QueueService>();
        builder.Services.AddSingleton<IGotifyClient, GotifyClient>();
        builder.Services.AddSingleton<IQueueProcessor, QueueProcessor>();
        builder.Services.AddSingleton<ISmsHistoryService, SmsHistoryService>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<SettingsPage>();
        var app = builder.Build();
        ServiceRegistry.Provider = app.Services;
        return app;
    }
}
