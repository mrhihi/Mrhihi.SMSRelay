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

    public async Task<bool> EnqueueIncomingAsync(string sender, string body, DateTimeOffset receivedAt, IEnumerable<DeliveryTarget> targets)
    {
        var selected = targets.GroupBy(x => x.TokenId).Select(x => x.First()).ToList();
        if (selected.Count == 0) return false;
        var baseKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{sender}\n{receivedAt.ToUnixTimeMilliseconds()}\n{body}")));
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadUnlockedAsync();
            var messageGroupId = Guid.NewGuid();
            foreach (var target in selected.Where(target => !entries.Any(x => x.DeduplicationKey == $"{baseKey}:{target.TokenId:N}")))
                entries.Add(new QueueItem { Sender = sender, Body = body, ReceivedAt = receivedAt, Target = target, MessageGroupId = messageGroupId, DeduplicationKey = $"{baseKey}:{target.TokenId:N}" });
            await SaveUnlockedAsync(entries);
        }
        finally { _gate.Release(); }
        Platforms.Android.RelayDeliveryService.RequestDelivery();
        return true;
    }

    public async Task EnqueueManualAsync(IEnumerable<SmsRecord> messages, IEnumerable<DeliveryTarget> targets)
    {
        var selected = targets.GroupBy(x => x.TokenId).Select(x => x.First()).ToList();
        if (selected.Count == 0) return;
        await EnqueueManualAsync(messages.Select(message => new ManualDelivery(message, selected)).ToList());
    }

    public async Task EnqueueManualAsync(IEnumerable<ManualDelivery> deliveries)
    {
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadUnlockedAsync();
            foreach (var delivery in deliveries)
            {
                var message = delivery.Message;
                var messageGroupId = Guid.NewGuid();
                foreach (var target in delivery.Targets.GroupBy(x => x.TokenId).Select(x => x.First()))
                    entries.Add(new QueueItem { Sender = message.Sender, Body = message.Body, ReceivedAt = message.ReceivedAt, Target = target, MessageGroupId = messageGroupId, IsManualImport = true });
            }
            await SaveUnlockedAsync(entries);
        }
        finally { _gate.Release(); }
        Platforms.Android.RelayDeliveryService.RequestDelivery();
    }

    public async Task<IReadOnlyList<QueueItem>> GetAsync() { await _gate.WaitAsync(); try { return (await LoadUnlockedAsync()).OrderByDescending(x => x.ReceivedAt).ToList(); } finally { _gate.Release(); } }
    public async Task<IReadOnlyList<QueueItem>> GetDueAsync(DateTimeOffset now)
    {
        await _gate.WaitAsync();
        try
        {
            var entries = await LoadUnlockedAsync();
            var expired = entries.Where(x => x.Status == DeliveryStatus.Sending && (x.SendingStartedAt is null || x.SendingStartedAt <= now.AddMinutes(-5))).ToList();
            foreach (var item in expired)
            {
                item.Status = DeliveryStatus.Pending;
                item.SendingStartedAt = null;
                item.NextAttemptAt = now;
                item.LastError = "上次傳送程序中斷，已重新排程。";
            }
            if (expired.Count != 0) await SaveUnlockedAsync(entries);
            return entries.Where(x => (x.Status is DeliveryStatus.Pending or DeliveryStatus.Failed) && (x.NextAttemptAt is null || x.NextAttemptAt <= now)).ToList();
        }
        finally { _gate.Release(); }
    }
    public async Task UpdateAsync(QueueItem item) { await _gate.WaitAsync(); try { var list = await LoadUnlockedAsync(); var index = list.FindIndex(x => x.Id == item.Id); if (index >= 0) { list[index] = item; await SaveUnlockedAsync(list); } } finally { _gate.Release(); } }
    public async Task RemoveAsync(Guid id) { await _gate.WaitAsync(); try { var list = await LoadUnlockedAsync(); list.RemoveAll(x => x.Id == id); await SaveUnlockedAsync(list); } finally { _gate.Release(); } }
    public async Task ClearAllAsync() { await _gate.WaitAsync(); try { await SaveUnlockedAsync([]); } finally { _gate.Release(); } }
    public async Task RetryAsync(Guid id)
    {
        await _gate.WaitAsync();
        try { var list = await LoadUnlockedAsync(); var item = list.SingleOrDefault(x => x.Id == id); if (item is not null) { item.Status = DeliveryStatus.Pending; item.NextAttemptAt = null; item.SendingStartedAt = null; item.LastError = null; await SaveUnlockedAsync(list); } }
        finally { _gate.Release(); }
        Platforms.Android.RelayDeliveryService.RequestDelivery();
    }

    private async Task<List<QueueItem>> LoadUnlockedAsync()
    {
        if (!File.Exists(_path)) return [];
        var encrypted = Convert.FromBase64String(await File.ReadAllTextAsync(_path)); var key = await GetKeyAsync(); var nonce = encrypted[..12]; var tag = encrypted[12..28]; var cipher = encrypted[28..]; var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16); aes.Decrypt(nonce, cipher, tag, plain);
        var entries = JsonSerializer.Deserialize<List<QueueItem>>(plain) ?? [];
        foreach (var legacy in entries.Where(x => x.MessageGroupId == Guid.Empty)) legacy.MessageGroupId = Guid.NewGuid();
        return entries;
    }
    private async Task SaveUnlockedAsync(List<QueueItem> entries)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(entries); var key = await GetKeyAsync(); var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(key, 16); aes.Encrypt(nonce, plain, cipher, tag); await File.WriteAllTextAsync(_path, Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray()));
    }
    private static async Task<byte[]> GetKeyAsync()
    {
        var stored = await SecureStorage.GetAsync(KeyName); if (stored is not null) return Convert.FromBase64String(stored);
        var key = RandomNumberGenerator.GetBytes(32); await SecureStorage.SetAsync(KeyName, Convert.ToBase64String(key)); return key;
    }
}
