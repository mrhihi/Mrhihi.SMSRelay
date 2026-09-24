using System.Text.Json;
using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class RuleGroupEditorPage : ContentPage
{
    private readonly ISettingsService _settings; private readonly RuleGroup _draft; private readonly Entry _name = new() { Placeholder = "例如：銀行驗證碼" }; private readonly Switch _enabled = new(); private readonly VerticalStackLayout _clauses = new() { Spacing = 10 }; private readonly Label _destinationSummary = new();
    public RuleGroupEditorPage(ISettingsService settings, RuleGroup source)
    {
        _settings = settings; _draft = Clone(source); Title = source.Clauses.Count == 0 && source.Name == "新規則群組" ? "新增規則群組" : "編輯規則群組"; _name.Text = _draft.Name; _enabled.IsToggled = _draft.IsEnabled;
        ToolbarItems.Add(new ToolbarItem { Text = "✓", Command = new Command(async () => await SaveAsync()) });
        var addClause = Ui.IconButton("＋", "新增 OR 條件組"); addClause.Clicked += async (_, _) => { _draft.Clauses.Add(new RuleClause()); Render(); await Navigation.PushAsync(new RuleEditorPage(null, rule => { _draft.Clauses[^1].Rules.Add(rule); Render(); })); };
        var clausesTitle = new HorizontalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { addClause, Ui.Title("符合以下任一條件組", 20) } };
        var destinations = Ui.Card(new Grid { Padding = new Thickness(2), ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, Children = { _destinationSummary, Ui.Secondary("›") } });
        ((Grid)destinations.Content).SetColumn(((Grid)destinations.Content).Children[1], 1);
        destinations.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await SelectDestinationsAsync()) });
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 16, Children =
        {
            Ui.Card(new VerticalStackLayout { Spacing = 8, Children = { Ui.Title("基本資料"), _name, new HorizontalStackLayout { Spacing = 10, Children = { Ui.Secondary("啟用此群組"), _enabled } } } }),
            clausesTitle, Ui.Secondary("同一組中的所有條件都必須符合；不同組之間為 OR。"), _clauses,
            Ui.Title("傳送目的地", 20), destinations
        } } };
        Render();
    }
    private void Render()
    {
        _clauses.Children.Clear();
        if (_draft.Clauses.Count == 0) _clauses.Children.Add(Ui.Secondary("尚未新增條件；此群組不會轉發任何 SMS。"));
        for (var i = 0; i < _draft.Clauses.Count; i++)
        {
            var clause = _draft.Clauses[i]; var rows = new VerticalStackLayout { Spacing = 5 };
            var add = Ui.IconButton("＋", $"在條件組 {i + 1} 加入 AND 條件"); add.Clicked += async (_, _) => await Navigation.PushAsync(new RuleEditorPage(null, rule => { clause.Rules.Add(rule); Render(); }));
            var delete = Ui.SmallAction("⋯", true); delete.Clicked += (_, _) => { _draft.Clauses.Remove(clause); Render(); };
            var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } };
            header.Add(new HorizontalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { add, Ui.Title($"條件組 {i + 1}") } }); header.Add(delete); Grid.SetColumn(delete, 1); rows.Children.Add(header);
            foreach (var rule in clause.Rules.ToList())
            {
                var row = Ui.Card(new Grid { Padding = new Thickness(2), ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, Children = { Ui.Themed(new Label { Text = rule.ToString(), LineBreakMode = LineBreakMode.WordWrap }), Ui.IconButton("⚙", "編輯條件") } }, new Thickness(10));
                var grid = (Grid)row.Content; grid.SetColumn(grid.Children[1], 1); row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Navigation.PushAsync(new RuleEditorPage(rule, updated => { var n = clause.Rules.FindIndex(x => x.Id == rule.Id); if (n >= 0) clause.Rules[n] = updated; Render(); }))) }); rows.Children.Add(row);
            }
            _clauses.Children.Add(Ui.Card(rows));
            if (i < _draft.Clauses.Count - 1) _clauses.Children.Add(new Label { Text = "OR", HorizontalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#0A63C9"), FontAttributes = FontAttributes.Bold });
        }
        _destinationSummary.Text = _draft.TargetTokenIds.Count == 0 ? "尚未選擇 Token" : $"已選擇 {_draft.TargetTokenIds.Count} 個 Token";
    }
    private async Task SelectDestinationsAsync()
    {
        var picker = new DestinationPickerPage(await _settings.GetDestinationsAsync(), _draft.TargetTokenIds); await Navigation.PushModalAsync(new NavigationPage(picker)); var selected = await picker.Selection; if (selected is null) return; _draft.TargetTokenIds = selected.ToList(); Render();
    }
    private async Task SaveAsync()
    {
        _draft.Name = _name.Text?.Trim() ?? ""; _draft.IsEnabled = _enabled.IsToggled; var all = await _settings.GetAsync(); var index = all.RuleGroups.FindIndex(x => x.Id == _draft.Id); if (index >= 0) all.RuleGroups[index] = _draft; else all.RuleGroups.Add(_draft);
        try { await _settings.SaveAsync(all); await Navigation.PopAsync(); } catch (Exception ex) { await DisplayAlert("無法儲存", ex.Message, "確定"); }
    }
    private static RuleGroup Clone(RuleGroup source) => JsonSerializer.Deserialize<RuleGroup>(JsonSerializer.Serialize(source))!;
}

public sealed class RuleEditorPage : ContentPage
{
    private readonly SourceRule _draft; private readonly Action<SourceRule> _saved; private readonly Picker _target = new() { Title = "比對欄位", ItemsSource = new List<string> { "發送者", "簡訊內容" } }; private readonly Picker _mode = new() { Title = "比對方式", ItemsSource = new List<string> { "完全相符", "前綴", "Regex" } }; private readonly Entry _pattern = new() { Placeholder = "輸入比對內容" };
    public RuleEditorPage(SourceRule? source, Action<SourceRule> saved)
    {
        _draft = source ?? new SourceRule(Guid.NewGuid(), "", RuleMatchMode.Exact); _saved = saved; Title = source is null ? "新增條件" : "編輯條件"; _target.SelectedIndex = (int)_draft.Target; _mode.SelectedIndex = (int)_draft.Mode; _pattern.Text = _draft.Pattern;
        ToolbarItems.Add(new ToolbarItem { Text = "✓", Command = new Command(async () => await SaveAsync()) });
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 16, Children = { Ui.Card(new VerticalStackLayout { Spacing = 10, Children = { Ui.Title("條件"), _target, _mode, _pattern, Ui.Secondary("Regex 可用於名稱、號碼或簡訊內容的進階比對。") } }) } } };
    }
    private async Task SaveAsync() { if (string.IsNullOrWhiteSpace(_pattern.Text)) { await DisplayAlert("請輸入內容", "比對內容不可空白。", "確定"); return; } _saved(_draft with { Pattern = _pattern.Text.Trim(), Target = (RuleTarget)_target.SelectedIndex, Mode = (RuleMatchMode)_mode.SelectedIndex, Join = RuleJoinOperator.And }); await Navigation.PopAsync(); }
}
