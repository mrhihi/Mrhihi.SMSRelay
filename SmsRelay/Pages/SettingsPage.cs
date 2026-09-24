using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly ISettingsService _settings;
    private readonly VerticalStackLayout _servers = new() { Spacing = 1 };
    private readonly VerticalStackLayout _groups = new() { Spacing = 1 };
    private RelaySettings _current = new();

    private readonly IGotifyClient _gotify;
    public SettingsPage(ISettingsService settings, IGotifyClient gotify)
    {
        _settings = settings; _gotify = gotify; Title = "設定";
        var addServer = Ui.IconButton("＋", "新增 Gotify Server"); addServer.Clicked += async (_, _) => await Navigation.PushAsync(new ServerEditorPage(_settings, _gotify, new GotifyServer()));
        var addGroup = Ui.IconButton("＋", "新增規則群組"); addGroup.Clicked += async (_, _) => await Navigation.PushAsync(new RuleGroupEditorPage(_settings, new RuleGroup()));
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = new Thickness(16, 14, 16, 28), Spacing = 22, Children =
        {
            Ui.Title("設定", 28),
            SectionHeader("Gotify Servers", addServer), Ui.Card(_servers, new Thickness(0)),
            SectionHeader("自動轉發規則", addGroup), Ui.Card(_groups, new Thickness(0))
        } } };
        Appearing += async (_, _) => await LoadAsync();
        TabSwipe.Attach((View)Content);
    }

    private static View SectionHeader(string title, View action)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(Ui.Title(title, 20)); grid.Add(action); Grid.SetColumn(action, 1); return grid;
    }

    private async Task LoadAsync()
    {
        _current = await _settings.GetAsync(); _servers.Children.Clear(); _groups.Children.Clear();
        if (_current.Servers.Count == 0) _servers.Children.Add(Empty("尚未設定 Gotify Server", "新增 Server", async () => await Navigation.PushAsync(new ServerEditorPage(_settings, _gotify, new GotifyServer()))));
        foreach (var server in _current.Servers)
        {
            Func<Task> open = async () => await Navigation.PushAsync(new ServerEditorPage(_settings, _gotify, server));
            var row = SettingsRow(server.Name, $"{Host(server.BaseUrl)} · {server.Tokens.Count} 個 Token · 優先權 {server.Priority}", server.Tokens.Count == 0 ? "尚未設定 Token" : null, open);
            row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await open()) }); _servers.Children.Add(row);
        }
        if (_current.RuleGroups.Count == 0) _groups.Children.Add(Empty("尚未設定規則群組", "新增規則群組", async () => await Navigation.PushAsync(new RuleGroupEditorPage(_settings, new RuleGroup()))));
        foreach (var group in _current.RuleGroups)
        {
            var summary = $"{group.Clauses.Sum(x => x.Rules.Count)} 個條件 · {group.TargetTokenIds.Count} 個目的地";
            Func<Task> open = async () => await Navigation.PushAsync(new RuleGroupEditorPage(_settings, group));
            var row = SettingsRow(group.Name, summary, group.TargetTokenIds.Count == 0 ? "尚未選擇目的地" : null, open);
            var enabled = new Switch { IsToggled = group.IsEnabled, VerticalOptions = LayoutOptions.Center };
            enabled.Toggled += async (_, e) => { group.IsEnabled = e.Value; await _settings.SaveAsync(_current); };
            ((Grid)row.Content).Add(enabled); Grid.SetColumn(enabled, 2);
            row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await open()) }); _groups.Children.Add(row);
        }
    }

    private static Border SettingsRow(string title, string subtitle, string? warning, Func<Task> open)
    {
        var text = new VerticalStackLayout { Spacing = 2, Children = { Ui.Themed(new Label { Text = title, FontSize = 16 }), Ui.Secondary(subtitle) } };
        if (warning is not null) text.Children.Add(new Label { Text = warning, FontSize = 12, TextColor = Color.FromArgb("#C26A00") });
        var chevron = Ui.IconButton("⚙", $"編輯 {title}"); chevron.VerticalOptions = LayoutOptions.Center;
        chevron.Clicked += async (_, _) => await open();
        var grid = new Grid { Padding = new Thickness(16, 12), ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) } };
        grid.Add(text); grid.Add(chevron); Grid.SetColumn(chevron, 1);
        return new Border { Content = grid, StrokeThickness = 0 };
    }

    private static View Empty(string text, string action, Func<Task> execute)
    {
        var icon = Ui.SmallAction(action); icon.Clicked += async (_, _) => await execute();
        return new VerticalStackLayout { Padding = 16, Spacing = 8, Children = { Ui.Secondary(text), icon } };
    }
    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
}
