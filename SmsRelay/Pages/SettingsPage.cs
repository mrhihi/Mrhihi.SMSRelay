using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class SettingsPage : ContentPage
{
    private readonly ISettingsService _settings;
    private readonly IGotifyClient _gotify;
    private readonly Entry _url = new() { Placeholder = "https://gotify.example.com/" };
    private readonly Entry _token = new() { Placeholder = "Application token", IsPassword = true };
    private readonly Stepper _priority = new() { Minimum = 0, Maximum = 10, Increment = 1, Value = 5 };
    private readonly Label _priorityValue = new();
    private readonly Entry _pattern = new() { Placeholder = "來源號碼、名稱或 regex" };
    private readonly Picker _target = new() { Title = "比對欄位", ItemsSource = new List<string> { "發送者（電話／名稱）", "簡訊內容" } };
    private readonly Picker _mode = new() { Title = "比對方式", ItemsSource = Enum.GetNames<RuleMatchMode>().ToList() };
    private readonly VerticalStackLayout _rules = new() { Spacing = 6 };
    private readonly VerticalStackLayout _gotifyPanel = new() { Spacing = 10 };
    private readonly Button _gotifyToggle = new();
    private RelaySettings _current = new();

    public SettingsPage(ISettingsService settings, IGotifyClient gotify)
    {
        _settings = settings; _gotify = gotify; Title = "設定"; _target.SelectedIndex = 0; _mode.SelectedIndex = 0;
        _priority.ValueChanged += (_, _) => UpdatePriorityLabel();
        var add = new Button { Text = "新增來源規則" }; add.Clicked += (_, _) => AddRule();
        var save = new Button { Text = "儲存 Gotify 設定" }; save.Clicked += async (_, _) => await SaveAsync();
        var test = new Button { Text = "測試 Gotify 連線" }; test.Clicked += async (_, _) => await TestAsync();
        _gotifyToggle.Clicked += (_, _) => { _gotifyPanel.IsVisible = !_gotifyPanel.IsVisible; UpdateGotifyToggle(); };
        _gotifyPanel.Children.Add(_url);
        _gotifyPanel.Children.Add(_token);
        _gotifyPanel.Children.Add(new HorizontalStackLayout { Children = { _priorityValue, _priority } });
        _gotifyPanel.Children.Add(save);
        _gotifyPanel.Children.Add(test);
        UpdatePriorityLabel();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 10, Children =
        {
            _gotifyToggle, _gotifyPanel,
            new Label { Text = "自動轉發來源規則", FontSize = 22, FontAttributes = FontAttributes.Bold },
            new Label { Text = "即時收到的 SMS 至少命中一條啟用規則才會自動轉發。可比對發送者或簡訊內容。" }, _pattern, _target, _mode, add, _rules
        } } };
        Appearing += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _current = await _settings.GetAsync();
        _url.Text = _current.GotifyBaseUrl; _token.Text = await _settings.GetTokenAsync() ?? string.Empty; _priority.Value = _current.Priority;
        _gotifyPanel.IsVisible = string.IsNullOrWhiteSpace(_current.GotifyBaseUrl);
        UpdateGotifyToggle();
        RenderRules();
    }
    private void AddRule()
    {
        if (string.IsNullOrWhiteSpace(_pattern.Text) || _target.SelectedIndex < 0 || _mode.SelectedIndex < 0) return;
        _current.Rules.Add(new SourceRule(Guid.NewGuid(), _pattern.Text.Trim(), (RuleMatchMode)_mode.SelectedIndex, (RuleTarget)_target.SelectedIndex));
        _pattern.Text = string.Empty;
        _ = SaveRulesAsync();
    }
    private void RenderRules()
    {
        _rules.Children.Clear();
        foreach (var rule in _current.Rules.ToList())
        {
            var enabled = new Switch { IsToggled = rule.IsEnabled };
            enabled.Toggled += async (_, e) => { Replace(rule with { IsEnabled = e.Value }); await SaveRulesAsync(); };
            var remove = new Button
            {
                Text = "×", WidthRequest = 38, HeightRequest = 38, Padding = 0,
                BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#F87171"), FontSize = 24
            };
            remove.Clicked += async (_, _) => { _current.Rules.Remove(rule); await SaveRulesAsync(); };
            var title = new Label { Text = rule.ToString(), TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
            row.Add(enabled); row.Add(title); row.Add(remove);
            Grid.SetColumn(title, 1); Grid.SetColumn(remove, 2);
            _rules.Children.Add(new Frame
            {
                BackgroundColor = Color.FromArgb("#1E293B"), BorderColor = Color.FromArgb("#334155"), Padding = 12, CornerRadius = 10,
                Content = row
            });
        }
    }
    private async Task SaveRulesAsync()
    {
        try
        {
            await _settings.SaveAsync(_current, null);
            RenderRules();
        }
        catch (Exception ex) { await DisplayAlert("規則未儲存", ex.Message, "確定"); }
    }
    private void UpdatePriorityLabel() => _priorityValue.Text = $"優先權：{(int)_priority.Value}／10";
    private void UpdateGotifyToggle() => _gotifyToggle.Text = _gotifyPanel.IsVisible ? "Gotify 設定（收合）" : "Gotify 設定（已設定，點選展開）";
    private void Replace(SourceRule rule) { var index = _current.Rules.FindIndex(x => x.Id == rule.Id); if (index >= 0) _current.Rules[index] = rule; }
    private async Task SaveAsync()
    {
        try { _current.GotifyBaseUrl = _url.Text?.Trim() ?? string.Empty; _current.Priority = (int)_priority.Value; await _settings.SaveAsync(_current, _token.Text); await DisplayAlert("已儲存", "設定已安全儲存於此裝置。", "確定"); }
        catch (Exception ex) { await DisplayAlert("無法儲存", ex.Message, "確定"); }
    }
    private async Task TestAsync()
    {
        try { await SaveAsync(); await _gotify.TestAsync(CancellationToken.None); await DisplayAlert("成功", "Gotify 已收到測試訊息。", "確定"); }
        catch (Exception ex) { await DisplayAlert("連線失敗", ex.Message, "確定"); }
    }
}
