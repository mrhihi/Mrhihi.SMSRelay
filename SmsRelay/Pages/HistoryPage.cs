using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Pages;

public sealed class HistoryPage : ContentPage
{
    private readonly ISmsHistoryService _history;
    private readonly IQueueService _queue;
    private readonly ISettingsService _settings;
    private readonly Entry _search = new() { Placeholder = "搜尋來源或本文" };
    private readonly DatePicker _date = new() { Date = DateTime.Today, Format = "yyyy-MM-dd" };
    private int _days = 1;
    private readonly Label _daysLabel = new();
    private readonly VerticalStackLayout _rows = new() { Spacing = 8 };
    private readonly Dictionary<long, SmsRecord> _selected = [];
    private readonly Label _selection = Ui.Themed(new Label { VerticalOptions = LayoutOptions.Center });
    private readonly VerticalStackLayout _advancedSearch = new() { IsVisible = false, Spacing = 8 };
    private readonly Button _advancedToggle = Ui.LabeledIconButton("⌕", "搜尋");
    private readonly VerticalStackLayout _dateConditions = new() { Spacing = 8 };
    private readonly Button _loadMore = Ui.IconButton("↓", "載入更多簡訊");
    private readonly Button _selectToggle = Ui.LabeledIconButton("☑", "選取");
    private readonly Button _send = Ui.IconButton("➤", "轉發已選取項目");
    private bool _selectionMode;
    private int _resultCount;
    private int _offset;
    private bool _hasMore;

    public HistoryPage(ISmsHistoryService history, IQueueService queue, ISettingsService settings)
    {
        _history = history; _queue = queue; _settings = settings; Title = "歷史簡訊";
        var find = Ui.LabeledIconButton("⌕", "套用搜尋"); find.Clicked += async (_, _) => await LoadAsync();
        _send.Clicked += async (_, _) => await SendSelectedAsync();
        _selectToggle.Clicked += (_, _) => { _selectionMode = !_selectionMode; _selected.Clear(); UpdateSelection(); _selectToggle.Text = _selectionMode ? "×  取消" : "☑  選取"; SemanticProperties.SetDescription(_selectToggle, _selectionMode ? "取消選取" : "選取簡訊"); _ = LoadAsync(); };
        var previous = Ui.IconButton("‹", "上一個有簡訊的日期"); previous.Clicked += async (_, _) => await MoveDateAsync(-1);
        var next = Ui.IconButton("›", "下一個有簡訊的日期"); next.Clicked += async (_, _) => await MoveDateAsync(1);
        _date.DateSelected += async (_, _) => await LoadAsync();
        _advancedToggle.Clicked += async (_, _) => { _advancedSearch.IsVisible = !_advancedSearch.IsVisible; _dateConditions.IsVisible = !_advancedSearch.IsVisible; UpdateAdvancedToggle(); await LoadAsync(); };
        _advancedSearch.Children.Add(_search);
        _advancedSearch.Children.Add(find);
        _loadMore.Clicked += async (_, _) => await LoadMoreAsync();
        UpdateAdvancedToggle();
        var dateBar = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        dateBar.Add(previous); dateBar.Add(_date); dateBar.Add(next);
        Grid.SetColumn(_date, 1); Grid.SetColumn(next, 2);
        var today = Ui.LabeledIconButton("●", "今天"); today.Clicked += (_, _) => _date.Date = DateTime.Today;
        var previousMonth = Ui.LabeledIconButton("‹", "上月"); previousMonth.Clicked += (_, _) => _date.Date = (_date.Date ?? DateTime.Today).AddMonths(-1);
        var nextMonth = Ui.LabeledIconButton("›", "下月"); nextMonth.Clicked += (_, _) => _date.Date = MinDate((_date.Date ?? DateTime.Today).AddMonths(1), DateTime.Today);
        _dateConditions.Children.Add(dateBar);
        var decreaseDays = Ui.IconButton("−", "減少連續天數"); decreaseDays.Clicked += async (_, _) => { _days = Math.Max(1, _days - 1); UpdateDaysLabel(); await LoadAsync(); };
        var increaseDays = Ui.IconButton("＋", "增加連續天數"); increaseDays.Clicked += async (_, _) => { _days = Math.Min(10, _days + 1); UpdateDaysLabel(); await LoadAsync(); };
        _dateConditions.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { today, previousMonth, nextMonth } });
        _dateConditions.Children.Add(new HorizontalStackLayout { Spacing = 4, Children = { _daysLabel, decreaseDays, increaseDays } });
        UpdateDaysLabel();
        var top = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } };
        top.Add(Ui.Title("歷史簡訊", 28));
        var selection = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) } };
        selection.Add(_selection); selection.Add(_selectToggle); selection.SetColumn(selection.Children[1], 1);
        var header = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                top, Ui.Secondary(_selectionMode ? "點選簡訊以加入或取消選取。" : "依日期瀏覽 SMS；按「選取」後可批次轉發。"),
                _dateConditions, _advancedToggle, _advancedSearch, selection
            }
        };
        var scroll = new ScrollView { Content = _rows };
        var layout = new Grid
        {
            Padding = 16,
            RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star), new(GridLength.Auto)
            }
        };
        layout.Add(header);
        layout.Add(scroll);
        Grid.SetRow(scroll, 1);
        layout.Add(_send); Grid.SetRow(_send, 2);
        Content = layout;
        Appearing += async (_, _) => await LoadAsync();
        TabSwipe.Attach(layout);
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
            var page = await _history.SearchAsync(_advancedSearch.IsVisible ? null : _date.Date ?? DateTime.Today, _days, _search.Text, _offset);
            foreach (var sms in page.Items)
            {
                _resultCount++;
                var check = new CheckBox { IsVisible = _selectionMode };
                check.CheckedChanged += (_, e) => { if (e.Value) _selected[sms.Id] = sms; else _selected.Remove(sms.Id); UpdateSelection(); };
                var details = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Fill };
                details.Children.Add(Ui.Themed(new Label { Text = $"{sms.Sender} · {sms.ReceivedAt.LocalDateTime:g}", FontAttributes = FontAttributes.Bold }));
                details.Children.Add(Ui.Secondary(sms.Body));
                var row = new HorizontalStackLayout { Spacing = 10 };
                row.Children.Add(check);
                row.Children.Add(details);
                var card = Ui.Card(row, new Thickness(12));
                card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { if (!_selectionMode) return; check.IsChecked = !check.IsChecked; }) });
                _rows.Children.Add(card);
            }
            _offset += page.Items.Count;
            _hasMore = page.HasMore;
            _loadMore.IsVisible = _hasMore;
            if (_hasMore) _rows.Children.Add(_loadMore);
            UpdateSelection();
        }
        catch (Exception ex) { await DisplayAlert("無法讀取 SMS", ex.Message, "確定"); }
    }

    private void UpdateSelection()
    {
        _selection.Text = _selectionMode ? $"已選取 {_selected.Count} 則" : $"讀取到 {_resultCount} 則";
        _send.IsVisible = _selectionMode; _send.IsEnabled = _selected.Count > 0; SemanticProperties.SetDescription(_send, _selected.Count == 0 ? "選擇要轉發的簡訊" : $"轉發 {_selected.Count} 則簡訊");
    }
    private void UpdateAdvancedToggle()
    {
        _advancedToggle.Text = _advancedSearch.IsVisible ? "×  收合" : "⌕  搜尋";
        SemanticProperties.SetDescription(_advancedToggle, _advancedSearch.IsVisible ? "收合進階搜尋" : "展開進階搜尋");
    }
    private void UpdateDaysLabel() => _daysLabel.Text = $"連續 {_days} 天（由選擇日往後）";
    private async Task MoveDateAsync(int direction)
    {
        var current = _date.Date ?? DateTime.Today;
        var target = await _history.FindAdjacentMessageDateAsync(current, _days, direction);
        if (target is not null) { _date.Date = target; return; }
        await DisplayAlert("找不到訊息", "找不到下一個有 SMS 的日期。", "確定");
    }
    private static DateTime MinDate(DateTime left, DateTime right) => left <= right ? left : right;
    private async Task SendSelectedAsync()
    {
        if (_selected.Count == 0) return;
        var destinations = await _settings.GetDestinationsAsync();
        if (destinations.Count == 0) { await DisplayAlert("沒有目的地", "請先在設定新增 Gotify Server 與 Token。", "確定"); return; }
        var picker = new DestinationPickerPage(destinations);
        await Navigation.PushModalAsync(new NavigationPage(picker));
        var selected = await picker.Selection;
        if (selected is null) return;
        var targets = await _settings.GetTargetsAsync(selected);
        if (targets.Count == 0) { await DisplayAlert("沒有可用 Token", "所選 Token 缺少設定值。", "確定"); return; }
        await _queue.EnqueueManualAsync(_selected.Values, targets);
        await DisplayAlert("已加入佇列", $"{_selected.Count} 則 SMS 將送至 {targets.Count} 個 Token。", "確定");
        _selectionMode = false; _selectToggle.Text = "☑  選取";
        await LoadAsync();
    }
}
