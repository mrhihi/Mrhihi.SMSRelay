using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmsRelay.Models;

namespace SmsRelay.Services;

public sealed class QueueService : IQueueService
{
    private const string KeyName = "queue-encryption-key";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "queue.dat");

    public async Task<bool> EnqueueIncomingAsync(string sender, string body, DateTimeOffset receivedAt)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{sender}\n{receivedAt.ToUnixTimeMilliseconds()}\n{body}")));
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadUnlockedAsync();
            if (entries.Any(x => x.DeduplicationKey == key)) return false;
            entries.Add(new QueueItem { Sender = sender, Body = body, ReceivedAt = receivedAt, DeduplicationKey = key });
            await SaveUnlockedAsync(entries);
        }
        finally { _gate.Release(); }
        Platforms.Android.AndroidJobScheduler.Schedule();
        return true;
    }

    public async Task EnqueueManualAsync(IEnumerable<SmsRecord> messages)
    {
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadUnlockedAsync();
            entries.AddRange(messages.Select(x => new QueueItem { Sender = x.Sender, Body = x.Body, ReceivedAt = x.ReceivedAt, IsManualImport = true }));
            await SaveUnlockedAsync(entries);
        }
        finally { _gate.Release(); }
        Platforms.Android.AndroidJobScheduler.Schedule();
    }

    public async Task<IReadOnlyList<QueueItem>> GetAsync()
    {
        await _gate.WaitAsync(); try { return (await LoadUnlockedAsync()).OrderByDescending(x => x.ReceivedAt).ToList(); } finally { _gate.Release(); }
    }
    public async Task<IReadOnlyList<QueueItem>> GetDueAsync(DateTimeOffset now)
    {
        await _gate.WaitAsync(); try { return (await LoadUnlockedAsync()).Where(x => (x.Status is DeliveryStatus.Pending or DeliveryStatus.Failed) && (x.NextAttemptAt is null || x.NextAttemptAt <= now)).ToList(); } finally { _gate.Release(); }
    }
    public async Task UpdateAsync(QueueItem item)
    {
        await _gate.WaitAsync();
        try { var list = await LoadUnlockedAsync(); var index = list.FindIndex(x => x.Id == item.Id); if (index >= 0) { list[index] = item; await SaveUnlockedAsync(list); } }
        finally { _gate.Release(); }
    }
    public async Task RemoveAsync(Guid id)
    {
        await _gate.WaitAsync(); try { var list = await LoadUnlockedAsync(); list.RemoveAll(x => x.Id == id); await SaveUnlockedAsync(list); } finally { _gate.Release(); }
    }
    public async Task ClearAllAsync()
    {
        await _gate.WaitAsync(); try { await SaveUnlockedAsync([]); } finally { _gate.Release(); }
    }
    public async Task RetryAsync(Guid id)
    {
        await _gate.WaitAsync();
        try { var list = await LoadUnlockedAsync(); var item = list.SingleOrDefault(x => x.Id == id); if (item is not null) { item.Status = DeliveryStatus.Pending; item.NextAttemptAt = null; item.LastError = null; await SaveUnlockedAsync(list); } }
        finally { _gate.Release(); }
        Platforms.Android.AndroidJobScheduler.Schedule();
    }

    private async Task<List<QueueItem>> LoadUnlockedAsync()
    {
        if (!File.Exists(_path)) return [];
        var encrypted = Convert.FromBase64String(await File.ReadAllTextAsync(_path));
        var key = await GetKeyAsync();
        var nonce = encrypted[..12]; var tag = encrypted[12..28]; var cipher = encrypted[28..]; var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16); aes.Decrypt(nonce, cipher, tag, plain);
        return JsonSerializer.Deserialize<List<QueueItem>>(plain) ?? [];
    }

    private async Task SaveUnlockedAsync(List<QueueItem> entries)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(entries); var key = await GetKeyAsync(); var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag);
        var combined = nonce.Concat(tag).Concat(cipher).ToArray();
        await File.WriteAllTextAsync(_path, Convert.ToBase64String(combined));
    }

    private static async Task<byte[]> GetKeyAsync()
    {
        var stored = await SecureStorage.GetAsync(KeyName);
        if (stored is not null) return Convert.FromBase64String(stored);
        var key = RandomNumberGenerator.GetBytes(32); await SecureStorage.SetAsync(KeyName, Convert.ToBase64String(key)); return key;
    }
}
