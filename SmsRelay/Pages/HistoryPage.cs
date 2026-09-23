using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class HistoryPage : ContentPage
{
    private readonly ISmsHistoryService _history;
    private readonly IQueueService _queue;
    private readonly Entry _search = new() { Placeholder = "搜尋來源或本文" };
    private readonly DatePicker _date = new() { Date = DateTime.Today, Format = "yyyy-MM-dd" };
    private readonly Stepper _days = new() { Minimum = 1, Maximum = 10, Increment = 1, Value = 1 };
    private readonly Label _daysLabel = new();
    private readonly VerticalStackLayout _rows = new() { Spacing = 8 };
    private readonly Dictionary<long, SmsRecord> _selected = [];
    private readonly Label _selection = new();
    private readonly VerticalStackLayout _advancedSearch = new() { IsVisible = false, Spacing = 8 };
    private readonly Button _advancedToggle = new();
    private readonly VerticalStackLayout _dateConditions = new() { Spacing = 8 };
    private readonly Button _loadMore = new() { Text = "載入更多", IsVisible = false };
    private int _resultCount;
    private int _offset;
    private bool _hasMore;

    public HistoryPage(ISmsHistoryService history, IQueueService queue)
    {
        _history = history; _queue = queue; Title = "歷史簡訊";
        var find = new Button { Text = "套用搜尋" }; find.Clicked += async (_, _) => await LoadAsync();
        var send = new Button { Text = "將已選取項目加入傳送佇列" }; send.Clicked += async (_, _) => await SendSelectedAsync();
        var previous = new Button { Text = "‹", WidthRequest = 48 }; previous.Clicked += async (_, _) => await MoveDateAsync(-1);
        var next = new Button { Text = "›", WidthRequest = 48 }; next.Clicked += async (_, _) => await MoveDateAsync(1);
        _date.DateSelected += async (_, _) => await LoadAsync();
        _days.ValueChanged += async (_, _) => { UpdateDaysLabel(); await LoadAsync(); };
        _advancedToggle.Clicked += async (_, _) => { _advancedSearch.IsVisible = !_advancedSearch.IsVisible; _dateConditions.IsVisible = !_advancedSearch.IsVisible; UpdateAdvancedToggle(); await LoadAsync(); };
        _advancedSearch.Children.Add(_search);
        _advancedSearch.Children.Add(find);
        _loadMore.Clicked += async (_, _) => await LoadMoreAsync();
        UpdateAdvancedToggle();
        var dateBar = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        dateBar.Add(previous); dateBar.Add(_date); dateBar.Add(next);
        Grid.SetColumn(_date, 1); Grid.SetColumn(next, 2);
        var today = new Button { Text = "今天" }; today.Clicked += (_, _) => _date.Date = DateTime.Today;
        var previousMonth = new Button { Text = "上月" }; previousMonth.Clicked += (_, _) => _date.Date = (_date.Date ?? DateTime.Today).AddMonths(-1);
        var nextMonth = new Button { Text = "下月" }; nextMonth.Clicked += (_, _) => _date.Date = MinDate((_date.Date ?? DateTime.Today).AddMonths(1), DateTime.Today);
        _dateConditions.Children.Add(dateBar);
        _dateConditions.Children.Add(new HorizontalStackLayout { Children = { today, previousMonth, nextMonth } });
        _dateConditions.Children.Add(new HorizontalStackLayout { Children = { _daysLabel, _days } });
        UpdateDaysLabel();
        var header = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "手動選取的所有 SMS 都可發送，不受自動規則限制。", LineBreakMode = LineBreakMode.WordWrap },
                _dateConditions, _advancedToggle, _advancedSearch, _selection, send
            }
        };
        var scroll = new ScrollView { Content = _rows };
        var layout = new Grid
        {
            Padding = 16,
            RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star)
            }
        };
        layout.Add(header);
        layout.Add(scroll);
        Grid.SetRow(scroll, 1);
        Content = layout;
        Appearing += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _rows.Children.Clear(); _selected.Clear(); _resultCount = 0; _offset = 0; _hasMore = false; UpdateSelection();
        await LoadMoreAsync();
    }

    private async Task LoadMoreAsync()
    {
        try
        {
            _rows.Children.Remove(_loadMore);
            var page = await _history.SearchAsync(_advancedSearch.IsVisible ? null : _date.Date ?? DateTime.Today, (int)_days.Value, _search.Text, _offset);
            foreach (var sms in page.Items)
            {
                _resultCount++;
                var check = new CheckBox();
                check.CheckedChanged += (_, e) => { if (e.Value) _selected[sms.Id] = sms; else _selected.Remove(sms.Id); UpdateSelection(); };
                var details = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Fill };
                details.Children.Add(new Label { Text = $"{sms.Sender} · {sms.ReceivedAt.LocalDateTime:g}", FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
                details.Children.Add(new Label { Text = sms.Body, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2, TextColor = Color.FromArgb("#CBD5E1") });
                var row = new HorizontalStackLayout { Spacing = 10 };
                row.Children.Add(check);
                row.Children.Add(details);
                _rows.Children.Add(new Frame
                {
                    Content = row,
                    BackgroundColor = Color.FromArgb("#1E293B"),
                    BorderColor = Color.FromArgb("#334155"),
                    Padding = 12,
                    CornerRadius = 10
                });
            }
            _offset += page.Items.Count;
            _hasMore = page.HasMore;
            _loadMore.IsVisible = _hasMore;
            if (_hasMore) _rows.Children.Add(_loadMore);
            UpdateSelection();
        }
        catch (Exception ex) { await DisplayAlert("無法讀取 SMS", ex.Message, "確定"); }
    }

    private void UpdateSelection() => _selection.Text = $"讀取到 {_resultCount} 則 · 已選取 {_selected.Count} 則";
    private void UpdateAdvancedToggle() => _advancedToggle.Text = _advancedSearch.IsVisible ? "進階搜尋（收合）" : "進階搜尋";
    private void UpdateDaysLabel() => _daysLabel.Text = $"連續 {(int)_days.Value} 天（由選擇日往後）";
    private async Task MoveDateAsync(int direction)
    {
        var current = _date.Date ?? DateTime.Today;
        var target = await _history.FindAdjacentMessageDateAsync(current, (int)_days.Value, direction);
        if (target is not null) { _date.Date = target; return; }
        await DisplayAlert("找不到訊息", "找不到下一個有 SMS 的日期。", "確定");
    }
    private static DateTime MinDate(DateTime left, DateTime right) => left <= right ? left : right;
    private async Task SendSelectedAsync()
    {
        if (_selected.Count == 0) return;
        await _queue.EnqueueManualAsync(_selected.Values);
        await DisplayAlert("已加入佇列", $"{_selected.Count} 則 SMS 將在網路可用時傳送。", "確定");
        await LoadAsync();
    }
}
