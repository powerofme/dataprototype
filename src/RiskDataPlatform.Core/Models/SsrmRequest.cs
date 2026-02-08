using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class SsrmRequest
{
    [Id(0)]
    public object? FilterModel { get; set; }

    [Id(1)]
    public List<object>? SortModel { get; set; }

    [Id(2)]
    public List<string>? GroupKeys { get; set; }

    [Id(3)]
    public int StartRow { get; set; }

    [Id(4)]
    public int EndRow { get; set; }

    [Id(5)]
    public List<string>? RowGroupCols { get; set; }

    [Id(6)]
    public List<string>? ValueCols { get; set; }

    [Id(7)]
    public List<string>? PivotCols { get; set; }

    [Id(8)]
    public bool PivotMode { get; set; }
}
