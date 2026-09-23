using SmsRelay.Pages;

namespace SmsRelay;

public sealed class AppShell : Shell
{
    public AppShell()
    {
        var tabs = new TabBar();
        // Pages require services, so Shell must resolve them through MAUI DI rather
        // than using DataTemplate(Type), which only calls a parameterless constructor.
        tabs.Items.Add(new ShellContent { Title = "狀態", ContentTemplate = new DataTemplate(() => ServiceRegistry.Get<DashboardPage>()) });
        tabs.Items.Add(new ShellContent { Title = "歷史簡訊", ContentTemplate = new DataTemplate(() => ServiceRegistry.Get<HistoryPage>()) });
        tabs.Items.Add(new ShellContent { Title = "設定", ContentTemplate = new DataTemplate(() => ServiceRegistry.Get<SettingsPage>()) });
        Items.Add(tabs);
    }
}
