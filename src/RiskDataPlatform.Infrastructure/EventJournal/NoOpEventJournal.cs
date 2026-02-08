using Microsoft.Extensions.Logging;
using RiskDataPlatform.Core.Interfaces;

namespace RiskDataPlatform.Infrastructure.EventJournal;

public sealed class NoOpEventJournal : IEventJournal
{
    private readonly ILogger<NoOpEventJournal> _logger;

    public NoOpEventJournal(ILogger<NoOpEventJournal> logger)
    {
        _logger = logger;
    }

    public Task AppendEventAsync(
        string tenantId,
        string streamId,
        string eventType,
        object eventData,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "NoOp AppendEvent: tenant={TenantId}, stream={StreamId}, eventType={EventType}, metadata={Metadata}",
            tenantId,
            streamId,
            eventType,
            metadata != null ? string.Join(", ", metadata.Select(kv => $"{kv.Key}={kv.Value}")) : "none");

        return Task.CompletedTask;
    }

    public Task<List<TEvent>> ReadEventsAsync<TEvent>(
        string tenantId,
        string streamId,
        long fromVersion = 0,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "NoOp ReadEvents<{EventType}>: tenant={TenantId}, stream={StreamId}, fromVersion={FromVersion}, maxCount={MaxCount}",
            typeof(TEvent).Name,
            tenantId,
            streamId,
            fromVersion,
            maxCount);

        return Task.FromResult(new List<TEvent>());
    }

    public Task<List<object>> ReadEventsAsync(
        string tenantId,
        string streamId,
        long fromVersion = 0,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "NoOp ReadEvents: tenant={TenantId}, stream={StreamId}, fromVersion={FromVersion}, maxCount={MaxCount}",
            tenantId,
            streamId,
            fromVersion,
            maxCount);

        return Task.FromResult(new List<object>());
    }

    public Task<long> GetStreamVersionAsync(
        string tenantId,
        string streamId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "NoOp GetStreamVersion: tenant={TenantId}, stream={StreamId}",
            tenantId,
            streamId);

        return Task.FromResult(0L);
    }
}
