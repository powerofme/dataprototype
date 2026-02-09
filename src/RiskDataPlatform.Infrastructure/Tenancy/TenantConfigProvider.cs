using System.Collections.Concurrent;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Infrastructure.Tenancy;

public sealed class TenantConfigProvider
{
    private readonly ConcurrentDictionary<string, TenantConfiguration> _configurations = new();

    public TenantConfigProvider()
    {
        // Seed with default tenant configuration
        var defaultTenant = new TenantConfiguration
        {
            TenantId = "default",
            TenantName = "Default Tenant",
            IsActive = true,
            StorageBucket = "risk-data-platform",
            StoragePrefix = "default",
            Settings = new Dictionary<string, string>
            {
                { "MaxQueryTimeoutSeconds", "300" },
                { "EnableCaching", "true" }
            },
            AllowedReportTypes = new List<string> { "VaR", "Stress", "SSRM", "PnL" },
            MaxConcurrentExecutions = 10,
            CreatedAt = DateTime.UtcNow
        };

        _configurations[defaultTenant.TenantId] = defaultTenant;
    }

    public TenantConfiguration? GetConfiguration(string tenantId)
    {
        _configurations.TryGetValue(tenantId, out var config);
        return config;
    }

    public void AddOrUpdateConfiguration(TenantConfiguration configuration)
    {
        configuration.UpdatedAt = DateTime.UtcNow;
        _configurations[configuration.TenantId] = configuration;
    }

    public void RemoveConfiguration(string tenantId)
    {
        _configurations.TryRemove(tenantId, out _);
    }

    public IEnumerable<TenantConfiguration> GetAllConfigurations()
    {
        return _configurations.Values;
    }

    public bool TenantExists(string tenantId)
    {
        return _configurations.ContainsKey(tenantId);
    }
}
