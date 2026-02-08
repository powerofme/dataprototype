using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RiskDataPlatform.Core.Interfaces;

namespace RiskDataPlatform.Infrastructure.Storage;

public sealed class TestDataSeeder
{
    private readonly IS3Store _s3Store;
    private readonly ILogger<TestDataSeeder> _logger;

    public TestDataSeeder(IS3Store s3Store, ILogger<TestDataSeeder> logger)
    {
        _s3Store = s3Store;
        _logger = logger;
    }

    public async Task SeedAsync(string bucket, string tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting test data seeding for tenant {TenantId} in bucket {Bucket}", tenantId, bucket);

        var executionId = Guid.NewGuid();
        var asOfDate = DateTime.UtcNow.Date;
        var books = new[] { "Book250", "Book100", "Book50", "Book500" };
        var tradeCounts = new Dictionary<string, int>
        {
            { "Book250", 250 },
            { "Book100", 100 },
            { "Book50", 50 },
            { "Book500", 500 }
        };

        // Seed execution manifest
        await SeedExecutionManifestAsync(bucket, tenantId, executionId, asOfDate, books, cancellationToken);

        // Seed Arrow IPC files for each book
        foreach (var book in books)
        {
            await SeedArrowFileAsync(bucket, tenantId, executionId, book, tradeCounts[book], cancellationToken);
        }

        _logger.LogInformation("Test data seeding completed for tenant {TenantId}", tenantId);
    }

    private async Task SeedExecutionManifestAsync(
        string bucket,
        string tenantId,
        Guid executionId,
        DateTime asOfDate,
        string[] books,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            ExecutionId = executionId,
            TenantId = tenantId,
            DeskId = "TestDesk",
            ReportType = "VaR",
            AsOfDate = asOfDate.ToString("yyyy-MM-dd"),
            Books = books,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddMinutes(5),
            DataFiles = books.Select(b => new
            {
                BookId = b,
                Path = $"{tenantId}/executions/{executionId:N}/data/{b}.arrow",
                RecordCount = 0,
                SizeBytes = 0
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        var key = $"{tenantId}/executions/{executionId:N}/manifest.json";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await _s3Store.PutObjectAsync(bucket, key, stream, "application/json", null, cancellationToken);

        _logger.LogInformation("Seeded execution manifest: {Key}", key);
    }

    private async Task SeedArrowFileAsync(
        string bucket,
        string tenantId,
        Guid executionId,
        string bookId,
        int tradeCount,
        CancellationToken cancellationToken)
    {
        // Create placeholder Arrow IPC file content
        // This is a minimal placeholder - actual Arrow IPC format would be more complex
        var placeholderContent = $"# Placeholder Arrow IPC file for {bookId}\n" +
                                 $"# Trade count: {tradeCount}\n" +
                                 $"# This would be a binary Arrow IPC file in production\n" +
                                 $"# ExecutionId: {executionId}\n";

        var key = $"{tenantId}/executions/{executionId:N}/data/{bookId}.arrow";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(placeholderContent));

        var metadata = new Dictionary<string, string>
        {
            { "book-id", bookId },
            { "trade-count", tradeCount.ToString() },
            { "format", "arrow-ipc" },
            { "schema-version", "1.0" }
        };

        await _s3Store.PutObjectAsync(bucket, key, stream, "application/vnd.apache.arrow.file", metadata, cancellationToken);

        _logger.LogInformation("Seeded Arrow file: {Key}, trades={TradeCount}", key, tradeCount);
    }
}
