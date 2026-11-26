using CropProgressMonitor.Services.Interfaces;

namespace CropProgressMonitor.ViewModels;

public class GrowthTimelineViewModel
{
    public int FieldCropId { get; set; }
    public int FieldId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string CropName { get; set; } = string.Empty;
    public string? Variety { get; set; }
    public double BaseTemperature { get; set; }
    public DateTime SowingDate { get; set; }
    public double AccumulatedGdd { get; set; }
    public double TodayGdd { get; set; }
    public double AverageRecentDailyGdd { get; set; }
    public double ProgressPercent { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public int CurrentStageId { get; set; }
    public string? NextStageName { get; set; }
    public DateTime? EstimatedNextStageDate { get; set; }
    public bool UsedCachedFallback { get; set; }
    public string? WeatherWarning { get; set; }
    public List<StageTimelineItemViewModel> Stages { get; set; } = new();
    public List<DailyGddEntry> DailyGddSeries { get; set; } = new();
}

public class StageTimelineItemViewModel
{
    public int StageId { get; set; }
    public int StageOrder { get; set; }
    public string StageName { get; set; } = string.Empty;
    public double GddThreshold { get; set; }
    public string Status { get; set; } = "upcoming"; // completed, current, upcoming
    public double RemainingGdd { get; set; }
}
