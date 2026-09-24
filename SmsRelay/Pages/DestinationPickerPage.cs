using SmsRelay.Models;

namespace SmsRelay.Pages;

public sealed class DestinationPickerPage : ContentPage
{
    private readonly TaskCompletionSource<IReadOnlyList<Guid>?> _completion = new();
    private readonly HashSet<Guid> _selected = [];
    public Task<IReadOnlyList<Guid>?> Selection => _completion.Task;

    public DestinationPickerPage(IReadOnlyList<GotifyDestination> destinations, IEnumerable<Guid>? initiallySelected = null)
    {
        if (initiallySelected is not null) _selected.UnionWith(initiallySelected);
        Title = "選擇目的地";
        var rows = new VerticalStackLayout { Spacing = 8 };
        foreach (var server in destinations.GroupBy(x => new { x.ServerId, x.ServerName }))
        {
            rows.Children.Add(Ui.Secondary(server.Key.ServerName));
            foreach (var destination in server)
            {
                var check = new CheckBox { IsChecked = _selected.Contains(destination.TokenId) };
                check.CheckedChanged += (_, e) => { if (e.Value) _selected.Add(destination.TokenId); else _selected.Remove(destination.TokenId); };
                rows.Children.Add(Ui.Card(new HorizontalStackLayout { Spacing = 10, Children = { check, Ui.Themed(new Label { Text = destination.TokenName, VerticalOptions = LayoutOptions.Center }) } }, new Thickness(12, 8)));
            }
        }
        var send = Ui.LabeledIconButton("➤", "加入傳送佇列");
        send.Clicked += async (_, _) => { if (_selected.Count == 0) return; _completion.TrySetResult(_selected.ToList()); await Navigation.PopModalAsync(); };
        var cancel = Ui.LabeledIconButton("×", "取消"); cancel.Clicked += async (_, _) => { _completion.TrySetResult(null); await Navigation.PopModalAsync(); };
        var layout = new Grid
        {
            Padding = 16, RowDefinitions = new RowDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) },
            Children = { new ScrollView { Content = new VerticalStackLayout { Spacing = 12, Children = { Ui.Title("傳送到", 26), Ui.Secondary("選擇一個或多個 Gotify Token。"), rows } } }, new HorizontalStackLayout { Spacing = 10, Children = { cancel, send } } }
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
