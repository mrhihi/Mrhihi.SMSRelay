namespace SmsRelay.Pages;

internal static class TabSwipe
{
    // Android uses MainActivity.DispatchTouchEvent so ScrollView cannot consume
    // the horizontal fling before this fallback recognizer sees it.
    public static void Attach(View root)
    {
        _ = root;
    }
}
