using CropProgressMonitor.Models;

namespace CropProgressMonitor.Services.Interfaces;

public class GrowthCalculationResult
{
    public double AccumulatedGdd { get; set; }
    public double TodayGdd { get; set; }
    public double AverageRecentDailyGdd { get; set; }
    public double ProgressPercent { get; set; }
    public CropStage CurrentStage { get; set; } = null!;
    public CropStage? NextStage { get; set; }
    public DateTime? EstimatedNextStageDate { get; set; }
    public List<DailyGddEntry> DailySeries { get; set; } = new();
    public bool UsedCachedWeatherFallback { get; set; }
    public string? WeatherWarning { get; set; }
}

public class DailyGddEntry
{
    public DateTime Date { get; set; }
    public double Tmax { get; set; }
    public double Tmin { get; set; }
    public double Rainfall { get; set; }
    public double DailyGdd { get; set; }
    public double CumulativeGdd { get; set; }
}

public interface IGrowthStageService
{
    Task<GrowthCalculationResult> CalculateAndSaveEstimateAsync(
        int fieldCropId,
        bool forceWeatherRefresh = false,
        CancellationToken cancellationToken = default);
}
