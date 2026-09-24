using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using SmsRelay.Pages;

namespace SmsRelay;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    private float _startX;
    private float _startY;
    private bool _dragging;
    private Microsoft.Maui.Controls.View? _dragView;
    private AppShell? _shell;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Platforms.Android.AndroidJobScheduler.Schedule();
    }

    public override bool DispatchTouchEvent(MotionEvent? ev)
    {
        if (ev is not null) HandleTabDrag(ev);
        return base.DispatchTouchEvent(ev);
    }

    private void HandleTabDrag(MotionEvent ev)
    {
        var density = Resources?.DisplayMetrics?.Density ?? 1;
        switch (ev.ActionMasked)
        {
            case MotionEventActions.Down:
                _startX = ev.RawX; _startY = ev.RawY; _dragging = false; _dragView = null; _shell = null;
                break;
            case MotionEventActions.Move:
                var dx = (ev.RawX - _startX) / density;
                var dy = (ev.RawY - _startY) / density;
                if (!_dragging && Math.Abs(dx) >= 18 && Math.Abs(dx) > Math.Abs(dy) * 1.3f)
                {
                    if (!TryGetRootTab(out var page, out var shell)) return;
                    _dragging = true; _dragView = page.Content; _shell = shell;
                }
                if (_dragging && _dragView is not null && _shell is not null)
                {
                    var direction = dx < 0 ? 1 : -1;
                    // Follow the finger directly. At the first/last tab, retain a small
                    // resistance so it feels like a page boundary rather than a dead swipe.
                    _dragView.TranslationX = dx * (_shell.CanSwitchTab(direction) ? 1 : .18);
                }
                break;
            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                if (_dragging && _dragView is not null && _shell is not null)
                    _ = FinishTabDragAsync(_dragView, _shell, (ev.RawX - _startX) / density);
                _dragging = false; _dragView = null; _shell = null;
                break;
        }
    }

    private static bool TryGetRootTab(out ContentPage page, out AppShell shell)
    {
        page = null!; shell = null!;
        if (Shell.Current?.CurrentPage is not ContentPage current || current is not (DashboardPage or HistoryPage or SettingsPage)) return false;
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is not AppShell appShell) return false;
        page = current; shell = appShell; return true;
    }

    private static async Task FinishTabDragAsync(Microsoft.Maui.Controls.View view, AppShell shell, float totalX)
    {
        var direction = totalX < 0 ? 1 : -1;
        if (Math.Abs(totalX) >= 72 && shell.CanSwitchTab(direction))
        {
            await view.TranslateTo(direction > 0 ? -Math.Max(view.Width, 280) : Math.Max(view.Width, 280), 0, 120, Easing.CubicOut);
            view.TranslationX = 0;
            shell.SwitchTab(direction);
        }
        else await view.TranslateTo(0, 0, 160, Easing.CubicOut);
    }
}
