namespace RiskDataPlatform.Core.Constants;

public static class GrainKeys
{
    public static string FormatGrainKey(string tenantId, string logicalKey)
    {
        return $"{tenantId}/{logicalKey}";
    }

    public static (string tenantId, string logicalKey) ParseGrainKey(string grainKey)
    {
        var parts = grainKey.Split('/', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid grain key format: {grainKey}. Expected format: 'tenantId/logicalKey'");
        }
        return (parts[0], parts[1]);
    }

    public static string ExecutionKey(string tenantId, Guid executionId)
    {
        return FormatGrainKey(tenantId, executionId.ToString());
    }

    public static string DeskKey(string tenantId, string deskId)
    {
        return FormatGrainKey(tenantId, deskId);
    }

    public static string BookKey(string tenantId, string deskId, string bookName)
    {
        return FormatGrainKey(tenantId, $"{deskId}/{bookName}");
    }
}
