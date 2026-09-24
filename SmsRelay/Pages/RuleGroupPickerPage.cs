using SmsRelay.Models;

namespace SmsRelay.Pages;

public sealed class RuleGroupPickerPage : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<Guid>?> _completion = new();
    private readonly HashSet<Guid> _selected = [];
    public Task<IReadOnlyList<Guid>?> Selection => _completion.Task;

    public RuleGroupPickerPage(IReadOnlyList<RuleGroup> groups)
    {
        Title = "選擇規則群組";
        var rows = new VerticalStackLayout { Spacing = 8 };
        foreach (var group in groups)
        {
            var check = new CheckBox();
            check.CheckedChanged += (_, e) => { if (e.Value) _selected.Add(group.Id); else _selected.Remove(group.Id); };
            var details = new VerticalStackLayout { Spacing = 2, Children = { Ui.Themed(new Label { Text = group.Name, VerticalOptions = LayoutOptions.Center }), Ui.Secondary($"{group.Clauses.Count} 個條件組 · {group.TargetTokenIds.Distinct().Count()} 個 Token") } };
            var row = Ui.Card(new HorizontalStackLayout { Spacing = 10, Children = { check, details } }, new Thickness(12, 8));
            row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => check.IsChecked = !check.IsChecked) });
            rows.Children.Add(row);
        }
        var send = Ui.LabeledIconButton("➤", "測試並加入佇列");
        send.Clicked += async (_, _) => { if (_selected.Count == 0) return; _completion.TrySetResult(_selected.ToList()); await Navigation.PopModalAsync(); };
        var cancel = Ui.LabeledIconButton("×", "取消");
        cancel.Clicked += async (_, _) => { _completion.TrySetResult(null); await Navigation.PopModalAsync(); };
        var layout = new Grid
        {
            Padding = 16,
            RowDefinitions = new RowDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) },
            Children = { new ScrollView { Content = new VerticalStackLayout { Spacing = 12, Children = { Ui.Title("測試規則群組", 26), Ui.Secondary("選擇一或多個啟用中的群組；命中的簡訊才會加入佇列。"), rows } } }, new HorizontalStackLayout { Spacing = 10, Children = { cancel, send } } }
        };
        layout.SetRow(layout.Children[1], 1);
        Content = layout;
    }

    protected override void OnDisappearing()
    {
        if (!_completion.Task.IsCompleted) _completion.TrySetResult(null);
        base.OnDisappearing();
    }
}
