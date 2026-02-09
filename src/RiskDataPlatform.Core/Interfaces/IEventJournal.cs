namespace RiskDataPlatform.Core.Interfaces;

public interface IEventJournal
{
    Task AppendEventAsync(
        string tenantId,
        string streamId,
        string eventType,
        object eventData,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<List<TEvent>> ReadEventsAsync<TEvent>(
        string tenantId,
        string streamId,
        long fromVersion = 0,
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    Task<List<object>> ReadEventsAsync(
        string tenantId,
        string streamId,
        long fromVersion = 0,
        int maxCount = 100,
        CancellationToken cancellationToken = default);

    Task<long> GetStreamVersionAsync(
        string tenantId,
        string streamId,
        CancellationToken cancellationToken = default);
}
