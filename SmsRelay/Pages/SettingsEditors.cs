using System.Text.Json;
using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class ServerEditorPage : ContentPage
{
    private readonly ISettingsService _settings; private readonly IGotifyClient _gotify; private readonly GotifyServer _draft; private readonly bool _isNew; private readonly Dictionary<Guid, string> _secrets = [];
    private readonly Entry _name = new() { Placeholder = "例如：家庭 Gotify" };
    private readonly Entry _url = new() { Placeholder = "https://gotify.example.com/", Keyboard = Keyboard.Url };
    private readonly Label _priority = new(); private readonly VerticalStackLayout _tokens = new() { Spacing = 1 };

    public ServerEditorPage(ISettingsService settings, IGotifyClient gotify, GotifyServer source)
    {
        _settings = settings; _gotify = gotify; _draft = Clone(source); _isNew = source.Name.Length == 0 && source.Tokens.Count == 0; Title = _isNew ? "新增 Server" : "編輯 Server";
        _name.Text = _draft.Name; _url.Text = _draft.BaseUrl; UpdatePriority();
        ToolbarItems.Add(new ToolbarItem { Text = "✓", Command = new Command(async () => await SaveAsync()) });
        var minus = Ui.IconButton("−", "降低優先權"); minus.Clicked += (_, _) => { _draft.Priority = Math.Max(0, _draft.Priority - 1); UpdatePriority(); };
        var plus = Ui.IconButton("＋", "提高優先權"); plus.Clicked += (_, _) => { _draft.Priority = Math.Min(10, _draft.Priority + 1); UpdatePriority(); };
        var add = Ui.SmallAction("＋ Token"); add.Clicked += async (_, _) => await Navigation.PushAsync(new TokenEditorPage(_settings, _gotify, _draft, null, OnTokenSaved, null));
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 18, Children =
        {
            Ui.Card(new VerticalStackLayout { Spacing = 10, Children = { Ui.Title("基本資料"), _name, _url, new HorizontalStackLayout { Spacing = 10, Children = { Ui.Secondary("優先權"), minus, _priority, plus } } } }),
            Header("Application Tokens", add), Ui.Card(_tokens, new Thickness(0)),
            _isNew ? new BoxView { IsVisible = false } : DeleteRow()
        } } };
        RenderTokens();
    }
    private static View Header(string text, View action) { var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } }; g.Add(Ui.Title(text, 20)); g.Add(action); Grid.SetColumn(action, 1); return g; }
    private void UpdatePriority() => _priority.Text = $"{_draft.Priority} / 10";
    private void RenderTokens()
    {
        _tokens.Children.Clear(); if (_draft.Tokens.Count == 0) _tokens.Children.Add(new Label { Text = "尚未新增 Token", Margin = 16 });
        foreach (var token in _draft.Tokens)
        {
            var row = new Grid { Padding = new Thickness(16, 12), ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } };
            row.Add(new VerticalStackLayout { Spacing = 2, Children = { Ui.Themed(new Label { Text = token.Name }), Ui.Secondary(_secrets.ContainsKey(token.Id) ? "已更新，尚未儲存" : "已安全保存") } });
            var arrow = Ui.IconButton("⚙", $"編輯 Token {token.Name}"); row.Add(arrow); Grid.SetColumn(arrow, 1);
            var border = new Border { Content = row, StrokeThickness = 0 }; border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Navigation.PushAsync(new TokenEditorPage(_settings, _gotify, _draft, token, OnTokenSaved, RemoveToken))) }); _tokens.Children.Add(border);
        }
    }
    private void OnTokenSaved(GotifyToken token, string? secret)
    {
        var index = _draft.Tokens.FindIndex(x => x.Id == token.Id); if (index >= 0) _draft.Tokens[index] = token; else _draft.Tokens.Add(token);
        if (!string.IsNullOrWhiteSpace(secret)) _secrets[token.Id] = secret; RenderTokens();
    }
    private void RemoveToken(Guid tokenId) { _draft.Tokens.RemoveAll(x => x.Id == tokenId); _secrets.Remove(tokenId); RenderTokens(); }
    private View DeleteRow()
    {
        var remove = Ui.LabeledIconButton("⌫", "刪除 Server", true); remove.Clicked += async (_, _) => { if (!await DisplayAlert("刪除 Server", "會移除其 Token 與相關規則目的地。", "刪除", "取消")) return; var all = await _settings.GetAsync(); var item = all.Servers.FirstOrDefault(x => x.Id == _draft.Id); if (item is not null) { var ids = item.Tokens.Select(x => x.Id).ToHashSet(); all.Servers.Remove(item); foreach (var group in all.RuleGroups) group.TargetTokenIds.RemoveAll(ids.Contains); await _settings.SaveAsync(all); } await Navigation.PopAsync(); };
        return Ui.Card(new VerticalStackLayout { Children = { remove } });
    }
    private async Task SaveAsync()
    {
        _draft.Name = _name.Text?.Trim() ?? ""; _draft.BaseUrl = _url.Text?.Trim() ?? "";
        var all = await _settings.GetAsync(); var index = all.Servers.FindIndex(x => x.Id == _draft.Id); if (index >= 0) all.Servers[index] = _draft; else all.Servers.Add(_draft);
        try { await _settings.SaveAsync(all, _secrets); await Navigation.PopAsync(); } catch (Exception ex) { await DisplayAlert("無法儲存", ex.Message, "確定"); }
    }
    private static GotifyServer Clone(GotifyServer source) => JsonSerializer.Deserialize<GotifyServer>(JsonSerializer.Serialize(source))!;
}

public sealed class TokenEditorPage : ContentPage
{
    private readonly ISettingsService _settings; private readonly IGotifyClient _gotify; private readonly GotifyServer _server; private readonly GotifyToken _draft; private readonly Action<GotifyToken, string?> _saved; private readonly Action<Guid>? _removed;
    private readonly Entry _name = new() { Placeholder = "例如：工作通知" }; private readonly Entry _secret = new() { Placeholder = "Application token", IsPassword = true };
    public TokenEditorPage(ISettingsService settings, IGotifyClient gotify, GotifyServer server, GotifyToken? source, Action<GotifyToken, string?> saved, Action<Guid>? removed)
    {
        _settings = settings; _gotify = gotify; _server = server; _draft = source is null ? new GotifyToken() : new GotifyToken { Id = source.Id, Name = source.Name }; _saved = saved; _removed = removed; Title = source is null ? "新增 Token" : "編輯 Token"; _name.Text = _draft.Name;
        ToolbarItems.Add(new ToolbarItem { Text = "✓", Command = new Command(async () => await SaveAsync()) });
        var test = Ui.LabeledIconButton("➤", "測試連線"); test.Clicked += async (_, _) => await TestAsync();
        var content = new VerticalStackLayout { Spacing = 10, Children = { Ui.Title("Token 資料"), _name, _secret, Ui.Secondary(source is null ? "Token 會安全保存於裝置。" : "留白代表保留原本 Token。"), test } };
        if (source is not null) { var delete = Ui.LabeledIconButton("⌫", "刪除 Token", true); delete.Clicked += async (_, _) => await DeleteAsync(); content.Children.Add(delete); }
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 16, Children = { Ui.Card(content) } } };
    }
    private async Task TestAsync()
    {
        var secret = _secret.Text; if (string.IsNullOrWhiteSpace(secret)) secret = await _settings.GetTokenAsync(_draft.Id);
        if (string.IsNullOrWhiteSpace(secret)) { await DisplayAlert("請輸入 Token", "測試前需要 application token。", "確定"); return; }
        try { await _gotify.TestAsync(new DeliveryTarget(_server.Id, _draft.Id, _server.Name, _name.Text ?? "", _server.BaseUrl, secret, _server.Priority), CancellationToken.None); await DisplayAlert("測試成功", "Gotify 已收到測試訊息。", "確定"); } catch (Exception ex) { await DisplayAlert("連線失敗", ex.Message, "確定"); }
    }
    private async Task SaveAsync() { if (string.IsNullOrWhiteSpace(_name.Text)) { await DisplayAlert("請輸入名稱", "Token 名稱不可空白。", "確定"); return; } _draft.Name = _name.Text.Trim(); _saved(_draft, _secret.Text); await Navigation.PopAsync(); }
    private async Task DeleteAsync() { if (!await DisplayAlert("刪除 Token", "確定刪除此 Token？", "刪除", "取消")) return; _removed?.Invoke(_draft.Id); await Navigation.PopAsync(); }
}
