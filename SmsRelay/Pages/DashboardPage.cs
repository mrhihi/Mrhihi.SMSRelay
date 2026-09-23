using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;
using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class DashboardPage : ContentPage
{
    private readonly IQueueService _queue;
    private readonly Label _permission = new();
    private readonly Label _summary = new();
    private readonly Button _permissionAction = new() { Text = "授權讀取與接收 SMS" };
    private readonly Button _clearAll = new() { Text = "全部清除" };
    private readonly VerticalStackLayout _recent = new() { Spacing = 8 };

    public DashboardPage(IQueueService queue)
    {
        _queue = queue; Title = "SMS Relay";
        _permissionAction.Clicked += async (_, _) => await RequestSmsAsync();
        var refresh = new Button { Text = "重新整理" }; refresh.Clicked += async (_, _) => await RefreshAsync();
        _clearAll.Clicked += async (_, _) => await ClearAllAsync();
        var recentHeader = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } };
        var recentTitle = new Label { Text = "最近傳送紀錄", FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        recentHeader.Add(recentTitle); recentHeader.Add(_clearAll); Grid.SetColumn(_clearAll, 1);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 14, Children = { new Label { Text = "SMS → Gotify", FontSize = 28, FontAttributes = FontAttributes.Bold }, new Label { Text = "僅會自動轉發符合來源規則的新簡訊。" }, _permission, _permissionAction, _summary, refresh, recentHeader, _recent } } };
        Appearing += async (_, _) => await RefreshAsync();
    }

    private Task RequestSmsAsync()
    {
        var activity = Platform.CurrentActivity;
        activity?.RequestPermissions([Android.Manifest.Permission.ReceiveSms, Android.Manifest.Permission.ReadSms], 101);
        return Task.CompletedTask;
    }

    private async Task RefreshAsync()
    {
        var context = Android.App.Application.Context;
        var receive = context.CheckSelfPermission(Android.Manifest.Permission.ReceiveSms) == Permission.Granted;
        var read = context.CheckSelfPermission(Android.Manifest.Permission.ReadSms) == Permission.Granted;
        _permission.Text = $"SMS 權限：接收 {(receive ? "已授權" : "未授權")}／讀取 {(read ? "已授權" : "未授權")}";
        _permission.TextColor = receive && read ? Color.FromArgb("#4ADE80") : Color.FromArgb("#FBBF24");
        _permissionAction.IsVisible = !receive || !read;
        var all = await _queue.GetAsync();
        _clearAll.IsVisible = all.Count > 0;
        _summary.Text = $"待送 {all.Count(x => x.Status == DeliveryStatus.Pending)}  · 失敗 {all.Count(x => x.Status == DeliveryStatus.Failed)}  · 已送 {all.Count(x => x.Status == DeliveryStatus.Sent)}";
        _recent.Children.Clear();
        foreach (var item in all.Take(20))
        {
            var retry = new Button { Text = "重送", IsVisible = item.Status == DeliveryStatus.Failed };
            retry.Clicked += async (_, _) => { await _queue.RetryAsync(item.Id); await RefreshAsync(); };
            var clear = new Button
            {
                Text = "×", WidthRequest = 38, HeightRequest = 38, Padding = 0,
                BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#F87171"), FontSize = 24
            };
            clear.Clicked += async (_, _) => { await _queue.RemoveAsync(item.Id); await RefreshAsync(); };
            var title = new Label { Text = $"{item.Status} · {item.Sender} · {item.ReceivedAt.LocalDateTime:g}", TextColor = Colors.White, VerticalOptions = LayoutOptions.Center };
            var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } };
            header.Add(title); header.Add(clear); Grid.SetColumn(clear, 1);
            _recent.Children.Add(new Frame
            {
                BackgroundColor = Color.FromArgb("#1E293B"), BorderColor = Color.FromArgb("#334155"), Padding = 12, CornerRadius = 10,
                Content = new VerticalStackLayout { Children =
                {
                    header,
                    new Label { Text = item.Status == DeliveryStatus.Sent ? "已成功轉發（本文已清除）" : (item.LastError ?? item.Body), TextColor = Color.FromArgb("#CBD5E1"), LineBreakMode = LineBreakMode.TailTruncation },
                    retry
                } }
            });
        }
    }
    private async Task ClearAllAsync()
    {
        if (!await DisplayAlert("清除所有紀錄", "待送、失敗與成功紀錄都會從本機刪除。確定要繼續嗎？", "全部清除", "取消")) return;
        await _queue.ClearAllAsync();
        await RefreshAsync();
    }
}
