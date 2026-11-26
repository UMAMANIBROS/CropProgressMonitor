using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Services;

/// <summary>
/// Implements the thermal-time Growing Degree Day (GDD) model and stage projection engine.
/// </summary>
public class GrowthStageService : IGrowthStageService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IWeatherService _weatherService;
    private readonly ILogger<GrowthStageService> _logger;

    public GrowthStageService(
        ApplicationDbContext dbContext,
        IWeatherService weatherService,
        ILogger<GrowthStageService> logger)
    {
        _dbContext = dbContext;
        _weatherService = weatherService;
        _logger = logger;
    }

    /// <summary>
    /// Computes daily Growing Degree Days: max(0, (Tmax + Tmin) / 2 - Tbase).
    /// </summary>
    public static double CalculateDailyGdd(double tmax, double tmin, double baseTemperature)
    {
        var meanTemp = (tmax + tmin) / 2.0;
        return Math.Max(0.0, Math.Round(meanTemp - baseTemperature, 2));
    }

    /// <summary>
    /// Evaluates accumulated GDD, current stage, next stage, and projected next-stage date
    /// from daily weather records and ordered crop stages.
    /// </summary>
    public static GrowthCalculationResult EvaluateProgress(
        IReadOnlyList<WeatherRecord> weatherRecords,
        IReadOnlyList<CropStage> stages,
        DateTime sowingDate,
        DateTime today,
        double baseTemperature)
    {
        if (stages == null || stages.Count == 0)
        {
            throw new InvalidOperationException("Crop has no configured growth stages.");
        }

        var orderedStages = stages
            .OrderBy(s => s.GddThreshold)
            .ThenBy(s => s.StageOrder)
            .ToList();

        var startDate = sowingDate.Date;
        var referenceToday = today.Date;

        var observedDays = weatherRecords
            .Where(w => w.Date.Date >= startDate && w.Date.Date <= referenceToday)
            .OrderBy(w => w.Date)
            .ToList();

        double accumulatedGdd = 0;
        double todayGdd = 0;
        var dailySeries = new List<DailyGddEntry>(observedDays.Count);

        foreach (var day in observedDays)
        {
            var gdd = CalculateDailyGdd(day.Tmax, day.Tmin, baseTemperature);
            accumulatedGdd = Math.Round(accumulatedGdd + gdd, 2);

            if (day.Date.Date == referenceToday)
            {
                todayGdd = gdd;
            }

            dailySeries.Add(new DailyGddEntry
            {
                Date = day.Date.Date,
                Tmax = day.Tmax,
                Tmin = day.Tmin,
                Rainfall = day.Rainfall,
                DailyGdd = gdd,
                CumulativeGdd = accumulatedGdd
            });
        }

        var currentStage = orderedStages.LastOrDefault(s => accumulatedGdd >= s.GddThreshold)
            ?? orderedStages.First();

        var nextStage = orderedStages.FirstOrDefault(s =>
            s.StageOrder > currentStage.StageOrder || s.GddThreshold > currentStage.GddThreshold);

        var recentDays = dailySeries.TakeLast(7).ToList();
        double avgRecentDailyGdd = recentDays.Count > 0
            ? Math.Round(recentDays.Average(d => d.DailyGdd), 2)
            : 0;

        var forecastDays = weatherRecords
            .Where(w => w.Date.Date > referenceToday)
            .OrderBy(w => w.Date)
            .Take(7)
            .ToList();

        if (avgRecentDailyGdd <= 0 && forecastDays.Count > 0)
        {
            avgRecentDailyGdd = Math.Round(
                forecastDays.Average(f => CalculateDailyGdd(f.Tmax, f.Tmin, baseTemperature)),
                2);
        }

        DateTime? estimatedNextStageDate = ProjectNextStageDate(
            accumulatedGdd,
            nextStage,
            forecastDays,
            avgRecentDailyGdd,
            baseTemperature,
            referenceToday);

        var maxThreshold = orderedStages.Last().GddThreshold;
        double progressPercent = maxThreshold > 0
            ? Math.Clamp(Math.Round((accumulatedGdd / maxThreshold) * 100.0, 1), 0, 100)
            : 100.0;

        return new GrowthCalculationResult
        {
            AccumulatedGdd = accumulatedGdd,
            TodayGdd = todayGdd,
            AverageRecentDailyGdd = avgRecentDailyGdd,
            ProgressPercent = progressPercent,
            CurrentStage = currentStage,
            NextStage = nextStage,
            EstimatedNextStageDate = estimatedNextStageDate,
            DailySeries = dailySeries
        };
    }

    private static DateTime? ProjectNextStageDate(
        double accumulatedGdd,
        CropStage? nextStage,
        IReadOnlyList<WeatherRecord> forecastDays,
        double avgRecentDailyGdd,
        double baseTemperature,
        DateTime today)
    {
        if (nextStage == null)
        {
            return null;
        }

        double projectedGdd = accumulatedGdd;
        DateTime lastForecastDate = today;

        foreach (var day in forecastDays)
        {
            lastForecastDate = day.Date.Date;
            projectedGdd += CalculateDailyGdd(day.Tmax, day.Tmin, baseTemperature);

            if (projectedGdd >= nextStage.GddThreshold)
            {
                return lastForecastDate;
            }
        }

        if (avgRecentDailyGdd <= 0)
        {
            return null;
        }

        double remainingGdd = nextStage.GddThreshold - projectedGdd;
        if (remainingGdd <= 0)
        {
            return lastForecastDate;
        }

        int extraDays = (int)Math.Ceiling(remainingGdd / avgRecentDailyGdd);
        // Cap projection at 365 days for cold off-season sanity
        extraDays = Math.Min(extraDays, 365);
        return lastForecastDate.AddDays(extraDays);
    }

    public async Task<GrowthCalculationResult> CalculateAndSaveEstimateAsync(
        int fieldCropId,
        bool forceWeatherRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var fieldCrop = await _dbContext.FieldCrops
            .Include(fc => fc.Field)
            .Include(fc => fc.Crop)
                .ThenInclude(c => c!.CropStages)
            .FirstOrDefaultAsync(fc => fc.Id == fieldCropId, cancellationToken);

        if (fieldCrop == null || fieldCrop.Field == null || fieldCrop.Crop == null)
        {
            throw new KeyNotFoundException($"FieldCrop with ID {fieldCropId} was not found.");
        }

        var today = DateTime.UtcNow.Date;
        var weatherSync = await _weatherService.SyncFieldWeatherAsync(
            fieldCrop.Field,
            fieldCrop.SowingDate,
            forceWeatherRefresh,
            cancellationToken);

        var result = EvaluateProgress(
            weatherSync.Records,
            fieldCrop.Crop.CropStages.ToList(),
            fieldCrop.SowingDate,
            today,
            fieldCrop.Crop.BaseTemperature);

        result.UsedCachedWeatherFallback = weatherSync.UsedCachedFallback;
        result.WeatherWarning = weatherSync.WarningMessage;

        // Deduplicate by (FieldCropId, EstimateDate)
        var existingEstimate = await _dbContext.GrowthEstimates
            .FirstOrDefaultAsync(
                g => g.FieldCropId == fieldCropId && g.EstimateDate == today,
                cancellationToken);

        if (existingEstimate != null)
        {
            existingEstimate.AccumulatedGdd = result.AccumulatedGdd;
            existingEstimate.CurrentStageId = result.CurrentStage.Id;
            existingEstimate.NextStageId = result.NextStage?.Id;
            existingEstimate.NextStageDate = result.EstimatedNextStageDate;
        }
        else
        {
            _dbContext.GrowthEstimates.Add(new GrowthEstimate
            {
                FieldCropId = fieldCropId,
                EstimateDate = today,
                AccumulatedGdd = result.AccumulatedGdd,
                CurrentStageId = result.CurrentStage.Id,
                NextStageId = result.NextStage?.Id,
                NextStageDate = result.EstimatedNextStageDate
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Saved growth estimate for FieldCrop {FieldCropId}: GDD={Gdd}, Stage={Stage}",
            fieldCropId, result.AccumulatedGdd, result.CurrentStage.StageName);

        return result;
    }
}
