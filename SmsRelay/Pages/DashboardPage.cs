using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;
using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class DashboardPage : ContentPage
{
    private readonly IQueueService _queue; private readonly ISettingsService _settings;
    private readonly Label _permission = new(); private readonly Label _delivery = new(); private readonly Label _summary = new(); private readonly Button _permissionAction = Ui.IconButton("⚠", "授權必要權限"); private readonly Button _powerAction = Ui.IconButton("⚡", "允許鎖屏即時轉發"); private readonly VerticalStackLayout _recent = new() { Spacing = 8 }; private readonly VerticalStackLayout _setup = new() { Spacing = 1 };
    public DashboardPage(IQueueService queue, ISettingsService settings)
    {
        _queue = queue; _settings = settings; Title = "狀態"; _permissionAction.Clicked += async (_, _) => await RequestSmsAsync(); _powerAction.Clicked += (_, _) => RequestBatteryOptimizationExemption();
        var clear = Ui.SmallAction("⋯"); clear.Clicked += async (_, _) => await ClearAllAsync();
        var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } }; header.Add(Ui.Title("最近傳送紀錄", 20)); header.Add(clear); Grid.SetColumn(clear, 1);
        Content = new RefreshView { Content = new ScrollView { Content = new VerticalStackLayout { Padding = new Thickness(16, 14, 16, 28), Spacing = 16, Children = { Ui.Title("SMS Relay", 28), _setup, Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { _permission, _permissionAction, _delivery, _powerAction, _summary } }), header, _recent } } } };
        ((RefreshView)Content).Refreshing += async (_, _) => { await RefreshAsync(); ((RefreshView)Content).IsRefreshing = false; };
        Appearing += async (_, _) => await RefreshAsync();
        TabSwipe.Attach((View)Content);
    }
    private Task RequestSmsAsync()
    {
        var permissions = new List<string> { Android.Manifest.Permission.ReceiveSms, Android.Manifest.Permission.ReadSms };
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) permissions.Add(Android.Manifest.Permission.PostNotifications);
        Platform.CurrentActivity?.RequestPermissions(permissions.ToArray(), 101);
        return Task.CompletedTask;
    }
    private static void RequestBatteryOptimizationExemption()
    {
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations, Android.Net.Uri.Parse($"package:{context.PackageName}"));
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
    }
    private async Task RefreshAsync()
    {
        var context = Android.App.Application.Context; var receive = context.CheckSelfPermission(Android.Manifest.Permission.ReceiveSms) == Permission.Granted; var read = context.CheckSelfPermission(Android.Manifest.Permission.ReadSms) == Permission.Granted; var notification = !OperatingSystem.IsAndroidVersionAtLeast(33) || context.CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted; var unrestricted = Platforms.Android.RelayDeliveryService.IsBatteryOptimizationIgnored();
        var permissionsReady = receive && read && notification;
        _permission.Text = permissionsReady ? "SMS 與通知權限已授權" : "SMS 或通知權限尚未完成"; _permission.TextColor = permissionsReady ? Color.FromArgb("#188038") : Color.FromArgb("#C26A00"); _permissionAction.IsVisible = !permissionsReady;
        var immediateReady = unrestricted && notification;
        _delivery.Text = immediateReady ? "鎖屏即時轉發已就緒" : "鎖屏即時轉發未就緒：仍會排程重試，但系統可能延後傳送。"; _delivery.TextColor = immediateReady ? Color.FromArgb("#188038") : Color.FromArgb("#C26A00"); _powerAction.IsVisible = !unrestricted;
        var settings = await _settings.GetAsync(); BuildSetup(receive && read && notification && unrestricted, settings);
        var all = await _queue.GetAsync(); _summary.Text = $"待送 {all.Count(x => x.Status == DeliveryStatus.Pending)} · 失敗 {all.Count(x => x.Status == DeliveryStatus.Failed)} · 已送 {all.Count(x => x.Status == DeliveryStatus.Sent)}";
        _recent.Children.Clear();
        foreach (var group in all.GroupBy(x => x.MessageGroupId).OrderByDescending(x => x.Max(item => item.ReceivedAt)).Take(20)) _recent.Children.Add(BuildMessageRow(group.ToList()));
        if (all.Count == 0) _recent.Children.Add(Ui.Card(new VerticalStackLayout { Padding = 16, Children = { Ui.Title("尚無傳送紀錄"), Ui.Secondary("符合規則的新 SMS 或手動選取的簡訊會顯示在這裡。") } }));
    }
    private void BuildSetup(bool permission, RelaySettings settings)
    {
        _setup.Children.Clear(); var validGroup = settings.RuleGroups.Any(x => x.IsEnabled && x.Clauses.Any(c => c.Rules.Count > 0) && x.TargetTokenIds.Count > 0); var ready = permission && settings.Servers.SelectMany(x => x.Tokens).Any() && validGroup;
        if (ready) return;
        _setup.Children.Add(Ui.Title("完成設定", 20));
        _setup.Children.Add(Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { Step(permission, "SMS 權限"), Step(settings.Servers.SelectMany(x => x.Tokens).Any(), "Gotify Server 與 Token"), Step(validGroup, "自動轉發規則") } }));
    }
    private static View Step(bool complete, string text) => new HorizontalStackLayout { Spacing = 8, Children = { new Label { Text = complete ? "✓" : "○", TextColor = complete ? Color.FromArgb("#188038") : Color.FromArgb("#C26A00"), FontSize = 18 }, Ui.Themed(new Label { Text = text, VerticalOptions = LayoutOptions.Center }) } };
    private View BuildMessageRow(List<QueueItem> items)
    {
        var sample = items[0]; var sent = items.Count(x => x.Status == DeliveryStatus.Sent); var failed = items.Count(x => x.Status == DeliveryStatus.Failed); var pending = items.Count - sent - failed;
        var state = failed > 0 ? (sent > 0 ? "部分失敗" : "傳送失敗") : pending > 0 ? "處理中" : "已完成";
        var color = failed > 0 ? "#C62828" : pending > 0 ? "#C26A00" : "#188038";
        var title = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } }; title.Add(Ui.Themed(new Label { Text = sample.Sender, FontAttributes = FontAttributes.Bold })); var badge = new Label { Text = state, TextColor = Color.FromArgb(color), FontSize = 13 }; title.Add(badge); Grid.SetColumn(badge, 1);
        var card = Ui.Card(new VerticalStackLayout { Spacing = 4, Children = { title, Ui.Secondary($"{sample.ReceivedAt.LocalDateTime:g} · {(sample.IsManualImport ? "手動" : "自動")}"), Ui.Secondary($"{items.Count} 個目的地 · {sent} 成功 · {failed} 失敗" + (pending > 0 ? $" · {pending} 待送" : "")) } });
        card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Navigation.PushAsync(new DeliveryDetailPage(_queue, items.First().MessageGroupId))) }); return card;
    }
    private async Task ClearAllAsync() { if (await DisplayAlert("清除所有紀錄", "待送、失敗與成功紀錄都會從本機刪除。", "全部清除", "取消")) { await _queue.ClearAllAsync(); await RefreshAsync(); } }
}

public sealed class DeliveryDetailPage : ContentPage
{
    private readonly IQueueService _queue; private readonly Guid _groupId; private readonly VerticalStackLayout _rows = new() { Spacing = 8 };
    public DeliveryDetailPage(IQueueService queue, Guid groupId) { _queue = queue; _groupId = groupId; Title = "傳送詳情"; Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 12, Children = { Ui.Title("投遞目的地", 24), _rows } } }; Appearing += async (_, _) => await LoadAsync(); }
    private async Task LoadAsync()
    {
        _rows.Children.Clear(); var items = (await _queue.GetAsync()).Where(x => x.MessageGroupId == _groupId).ToList(); foreach (var item in items)
        {
            var retry = Ui.SmallAction("重送"); retry.IsVisible = item.Status == DeliveryStatus.Failed; retry.Clicked += async (_, _) => { await _queue.RetryAsync(item.Id); await LoadAsync(); };
            _rows.Children.Add(Ui.Card(new VerticalStackLayout { Spacing = 4, Children = { Ui.Title(item.Target is null ? "未指定目的地" : $"{item.Target.ServerName}／{item.Target.TokenName}"), Ui.Secondary(item.Status.ToString()), Ui.Secondary(item.LastError ?? (item.Status == DeliveryStatus.Sent ? "已成功轉發" : "等待傳送")), retry } }));
        }
    }
}
