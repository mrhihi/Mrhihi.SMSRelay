using SmsRelay.Pages;

namespace SmsRelay;

public sealed class AppShell : Shell
{
    private readonly TabBar _tabs;
    public AppShell()
    {
        _tabs = new TabBar();
        // Pages require services, so Shell must resolve them through MAUI DI rather
        // than using DataTemplate(Type), which only calls a parameterless constructor.
        _tabs.Items.Add(new ShellContent { Title = "狀態", ContentTemplate = new DataTemplate(() => ServiceRegistry.Get<DashboardPage>()) });
        _tabs.Items.Add(new ShellContent { Title = "歷史簡訊", ContentTemplate = new DataTemplate(() => ServiceRegistry.Get<HistoryPage>()) });
        _tabs.Items.Add(new ShellContent { Title = "設定", ContentTemplate = new DataTemplate(() => ServiceRegistry.Get<SettingsPage>()) });
        Items.Add(_tabs);
    }

    public void SwitchTab(int delta)
    {
        var entries = _tabs.Items.ToList();
        var current = _tabs.CurrentItem ?? entries.FirstOrDefault();
        var index = entries.IndexOf(current!);
        var next = Math.Clamp(index + delta, 0, entries.Count - 1);
        if (next != index) _tabs.CurrentItem = entries[next];
    }

    public bool CanSwitchTab(int delta)
    {
        var entries = _tabs.Items.ToList();
        var current = _tabs.CurrentItem ?? entries.FirstOrDefault();
        var index = entries.IndexOf(current!);
        return index + delta is >= 0 and < 3;
    }
}
