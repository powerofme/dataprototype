using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Versions.Compatibility;
using Orleans.Versions.Selector;

namespace RiskDataPlatform.Infrastructure.Orleans;

public static class RollingUpgradeExtensions
{
    public static ISiloBuilder ConfigureRollingUpgrade(this ISiloBuilder siloBuilder)
    {
        siloBuilder.Configure<GrainVersioningOptions>(options =>
        {
            options.DefaultCompatibilityStrategy = nameof(BackwardCompatible);
            options.DefaultVersionSelectorStrategy = nameof(LatestVersion);
        });

        return siloBuilder;
    }

    public static IClientBuilder ConfigureRollingUpgrade(this IClientBuilder clientBuilder)
    {
        clientBuilder.Configure<GrainVersioningOptions>(options =>
        {
            options.DefaultCompatibilityStrategy = nameof(BackwardCompatible);
            options.DefaultVersionSelectorStrategy = nameof(LatestVersion);
        });

        return clientBuilder;
    }
}
