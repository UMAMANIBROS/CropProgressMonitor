using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.Services.Interfaces;
using CropProgressMonitor.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<Farmer> _userManager;
    private readonly IGrowthStageService _growthStageService;
    private readonly IWeatherService _weatherService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ApplicationDbContext dbContext,
        UserManager<Farmer> userManager,
        IGrowthStageService growthStageService,
        IWeatherService weatherService,
        ILogger<DashboardController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _growthStageService = growthStageService;
        _weatherService = weatherService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool refresh = false, CancellationToken cancellationToken = default)
    {
        var farmer = await _userManager.GetUserAsync(User);
        if (farmer == null)
        {
            return Challenge();
        }

        var fieldCrops = await _dbContext.FieldCrops
            .Include(fc => fc.Field)
            .Include(fc => fc.Crop)
                .ThenInclude(c => c!.CropStages)
            .Where(fc => fc.Field != null && fc.Field.FarmerId == farmer.Id && fc.IsActive)
            .OrderByDescending(fc => fc.SowingDate)
            .ToListAsync(cancellationToken);

        var vm = new DashboardViewModel
        {
            FarmerName = farmer.Name,
            FarmerLocation = farmer.Location,
            PreferredLanguage = farmer.PreferredLanguage,
            StatusMessage = TempData["StatusMessage"] as string
        };

        var warnings = new List<string>();
        var today = DateTime.UtcNow.Date;

        foreach (var fc in fieldCrops)
        {
            try
            {
                var calc = await _growthStageService.CalculateAndSaveEstimateAsync(fc.Id, refresh, cancellationToken);
                var weatherSummary = await _weatherService.GetFieldWeatherSummaryAsync(fc.Field!, cancellationToken);
                var todayWeather = calc.DailySeries.LastOrDefault();

                if (calc.UsedCachedWeatherFallback || weatherSummary.UsedCachedFallback)
                {
                    warnings.Add(calc.WeatherWarning ?? weatherSummary.WarningMessage ?? "Using cached weather data.");
                }

                vm.Fields.Add(new FieldCardViewModel
                {
                    FieldId = fc.FieldId,
                    FieldCropId = fc.Id,
                    FieldName = fc.Field!.FieldName,
                    LocationName = fc.Field.LocationName,
                    Latitude = fc.Field.Latitude,
                    Longitude = fc.Field.Longitude,
                    CropName = fc.Crop!.Name,
                    Variety = fc.Variety,
                    SowingDate = fc.SowingDate,
                    DaysSinceSowing = Math.Max(0, (today - fc.SowingDate.Date).Days),
                    AccumulatedGdd = calc.AccumulatedGdd,
                    ProgressPercent = calc.ProgressPercent,
                    CurrentStageName = calc.CurrentStage.StageName,
                    CurrentStageId = calc.CurrentStage.Id,
                    NextStageName = calc.NextStage?.StageName,
                    EstimatedNextStageDate = calc.EstimatedNextStageDate,
                    CurrentWeather = weatherSummary.Current,
                    TodayTmax = todayWeather?.Tmax,
                    TodayTmin = todayWeather?.Tmin,
                    TodayRainfall = todayWeather?.Rainfall,
                    UsedCachedFallback = calc.UsedCachedWeatherFallback
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating dashboard card for FieldCrop {FieldCropId}", fc.Id);
                warnings.Add($"Unable to update {fc.Field?.FieldName}: {ex.Message}");
            }
        }

        if (warnings.Count > 0)
        {
            vm.WarningMessage = warnings.Distinct().First();
        }

        return View(vm);
    }
}
