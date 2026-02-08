using Orleans.Runtime;
using RiskDataPlatform.Core.Interfaces;
using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Infrastructure.Tenancy;

public sealed class OrleansGrainTenantContext : ITenantContext
{
    private const string TenantIdKey = "TenantId";
    private readonly TenantConfigProvider _configProvider;

    public OrleansGrainTenantContext(TenantConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    public string TenantId
    {
        get
        {
            var tenantId = RequestContext.Get(TenantIdKey) as string;
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new InvalidOperationException("TenantId not found in Orleans RequestContext. Ensure tenant context is set before grain calls.");
            }
            return tenantId;
        }
    }

    public TenantConfiguration Configuration
    {
        get
        {
            var config = _configProvider.GetConfiguration(TenantId);
            if (config == null)
            {
                throw new InvalidOperationException($"Configuration not found for tenant: {TenantId}");
            }
            return config;
        }
    }

    public bool IsActive => Configuration.IsActive;

    public static void SetTenantId(string tenantId)
    {
        RequestContext.Set(TenantIdKey, tenantId);
    }

    public static void ClearTenantId()
    {
        RequestContext.Remove(TenantIdKey);
    }
}
