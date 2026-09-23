using Android.Provider;
using SmsRelay.Models;
using SmsRelay.Services;

namespace SmsRelay.Services;

public sealed class SmsHistoryService : ISmsHistoryService
{
    public Task<SmsPage> SearchAsync(DateTime? startDate, int days, string? query, int offset, int limit = 100)
    {
        var context = Android.App.Application.Context;
        var result = new List<SmsRecord>();
        string? selection = null; string[]? args = null;
        if (startDate is not null)
        {
            var start = startDate.Value.Date;
            var end = start.AddDays(Math.Clamp(days, 1, 365));
            var startMs = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start)).ToUnixTimeMilliseconds();
            var endMs = new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end)).ToUnixTimeMilliseconds();
            selection = "date >= ? AND date < ?"; args = [startMs.ToString(), endMs.ToString()];
        }
        using var cursor = context.ContentResolver?.Query(Android.Net.Uri.Parse("content://sms"), new[] { "_id", "address", "body", "date" }, selection, args, "date DESC");
        if (cursor is null) return Task.FromResult(new SmsPage(result, false));
        var needle = query?.Trim();
        var skipped = 0;
        var hasMore = false;
        while (cursor.MoveToNext())
        {
            var sender = cursor.GetString(1) ?? "未知來源"; var body = cursor.GetString(2) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(needle) && !sender.Contains(needle, StringComparison.OrdinalIgnoreCase) && !body.Contains(needle, StringComparison.OrdinalIgnoreCase)) continue;
            if (skipped++ < offset) continue;
            if (result.Count >= Math.Clamp(limit, 1, 100)) { hasMore = true; break; }
            result.Add(new SmsRecord(cursor.GetLong(0), sender, body, DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(3))));
        }
        return Task.FromResult(new SmsPage(result, hasMore));
    }

    public Task<DateTime?> FindAdjacentMessageDateAsync(DateTime startDate, int days, int direction)
    {
        var context = Android.App.Application.Context;
        var isForward = direction > 0;
        var anchor = isForward ? startDate.Date.AddDays(Math.Clamp(days, 1, 365)) : startDate.Date;
        var end = DateTime.Today.AddDays(1);
        var anchorMs = new DateTimeOffset(anchor, TimeZoneInfo.Local.GetUtcOffset(anchor)).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end)).ToUnixTimeMilliseconds();
        var selection = isForward ? "date >= ? AND date < ?" : "date < ?";
        var args = isForward ? new[] { anchorMs.ToString(), endMs.ToString() } : new[] { anchorMs.ToString() };
        var sort = isForward ? "date ASC LIMIT 1" : "date DESC LIMIT 1";
        using var cursor = context.ContentResolver?.Query(Android.Net.Uri.Parse("content://sms"), new[] { "date" }, selection, args, sort);
        if (cursor is null || !cursor.MoveToFirst()) return Task.FromResult<DateTime?>(null);
        return Task.FromResult<DateTime?>(DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(0)).LocalDateTime.Date);
    }
}
