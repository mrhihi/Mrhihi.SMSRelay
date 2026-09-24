namespace SmsRelay.Pages;

internal static class Ui
{
    private static readonly Color LightText = Color.FromArgb("#1C1C1E");
    private static readonly Color DarkText = Color.FromArgb("#F2F2F7");
    public static Border Card(View content, Thickness? padding = null)
    {
        var card = new Border { Content = content, Padding = padding ?? new Thickness(14), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 } };
        card.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1C1E"));
        return card;
    }
    public static Label Title(string text, double size = 17) => Themed(new Label { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold });
    public static Label Secondary(string text) => Themed(new Label { Text = text, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap }, secondary: true);
    public static Label Themed(Label label, bool secondary = false)
    {
        label.SetAppThemeColor(Label.TextColorProperty, secondary ? Color.FromArgb("#6B7280") : LightText, secondary ? Color.FromArgb("#A1A1AA") : DarkText);
        return label;
    }
    public static Button IconButton(string glyph, string description, bool destructive = false) => CreateIcon(glyph, description, destructive);
    public static Button LabeledIconButton(string glyph, string label, bool destructive = false)
    {
        var button = CreateIcon(glyph, label, destructive);
        button.Text = $"{glyph}  {label}";
        button.FontSize = 14;
        button.HeightRequest = 36;
        button.WidthRequest = -1;
        button.Padding = new Thickness(6, 0);
        return button;
    }
    public static HorizontalStackLayout IconLabel(Button icon, string label, bool destructive = false) => new()
    {
        Spacing = 2,
        Children = { icon, Themed(new Label { Text = label, VerticalOptions = LayoutOptions.Center, FontSize = 13 }, destructive) }
    };
    public static Button SmallAction(string text, bool destructive = false) => CreateIcon(IconFor(text, destructive), text, destructive);
    private static Button CreateIcon(string glyph, string description, bool destructive)
    {
        var button = new Button { Text = glyph, FontSize = 22, Padding = 0, WidthRequest = 44, HeightRequest = 44, CornerRadius = 22, BackgroundColor = Colors.Transparent, TextColor = destructive ? Color.FromArgb("#C62828") : Color.FromArgb("#0A63C9") };
        SemanticProperties.SetDescription(button, description);
        return button;
    }
    private static string IconFor(string action, bool destructive)
    {
        if (destructive || action.Contains("刪除") || action == "×") return "⌫";
        if (action.Contains("完成")) return "✓";
        if (action.Contains("取消")) return "×";
        if (action.Contains("測試") || action.Contains("傳送") || action.Contains("轉發")) return "➤";
        if (action.Contains("重送")) return "↻";
        if (action.Contains("載入")) return "↓";
        if (action.Contains("更多") || action == "⋯") return "⋯";
        if (action.Contains("新增") || action.Contains("加入") || action.Contains("＋")) return "＋";
        if (action == "−") return "−";
        if (action.Contains("編輯")) return "⚙";
        return "⚙";
    }
}
