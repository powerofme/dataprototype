using Orleans;

namespace RiskDataPlatform.Core.Models;

[GenerateSerializer]
public sealed class ReportQueryRequest
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
    public int? TimeSliderValue { get; set; }

    [Id(6)]
    public TimelineRequest? TimelineRequest { get; set; }
}
