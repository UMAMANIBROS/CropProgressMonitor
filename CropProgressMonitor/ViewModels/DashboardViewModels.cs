using CropProgressMonitor.Services.DTOs;

namespace CropProgressMonitor.ViewModels;

public class DashboardViewModel
{
    public string FarmerName { get; set; } = string.Empty;
    public string FarmerLocation { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "en";
    public List<FieldCardViewModel> Fields { get; set; } = new();
    public string? StatusMessage { get; set; }
    public string? WarningMessage { get; set; }
}

public class FieldCardViewModel
{
    public int FieldId { get; set; }
    public int FieldCropId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string CropName { get; set; } = string.Empty;
    public string? Variety { get; set; }
    public DateTime SowingDate { get; set; }
    public int DaysSinceSowing { get; set; }
    public double AccumulatedGdd { get; set; }
    public double ProgressPercent { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public int CurrentStageId { get; set; }
    public string? NextStageName { get; set; }
    public DateTime? EstimatedNextStageDate { get; set; }
    public CurrentConditionsDto? CurrentWeather { get; set; }
    public double? TodayTmax { get; set; }
    public double? TodayTmin { get; set; }
    public double? TodayRainfall { get; set; }
    public bool UsedCachedFallback { get; set; }
}
