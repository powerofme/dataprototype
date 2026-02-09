using RiskDataPlatform.Core.Models;

namespace RiskDataPlatform.Core.Interfaces;

public interface ITenantContext
{
    string TenantId { get; }
    
    TenantConfiguration Configuration { get; }
    
    bool IsActive { get; }
}
