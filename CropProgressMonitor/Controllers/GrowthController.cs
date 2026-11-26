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
public class GrowthController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<Farmer> _userManager;
    private readonly IGrowthStageService _growthStageService;

    public GrowthController(
        ApplicationDbContext dbContext,
        UserManager<Farmer> userManager,
        IGrowthStageService growthStageService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _growthStageService = growthStageService;
    }

    [HttpGet]
    public async Task<IActionResult> Timeline(int? id, bool refresh = false, CancellationToken cancellationToken = default)
    {
        var farmerId = _userManager.GetUserId(User)!;

        FieldCrop? fieldCrop;
        if (id.HasValue)
        {
            fieldCrop = await _dbContext.FieldCrops
                .Include(fc => fc.Field)
                .Include(fc => fc.Crop)
                    .ThenInclude(c => c!.CropStages)
                .FirstOrDefaultAsync(fc => fc.Id == id.Value && fc.Field != null && fc.Field.FarmerId == farmerId, cancellationToken);
        }
        else
        {
            fieldCrop = await _dbContext.FieldCrops
                .Include(fc => fc.Field)
                .Include(fc => fc.Crop)
                    .ThenInclude(c => c!.CropStages)
                .Where(fc => fc.Field != null && fc.Field.FarmerId == farmerId && fc.IsActive)
                .OrderByDescending(fc => fc.SowingDate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (fieldCrop == null || fieldCrop.Field == null || fieldCrop.Crop == null)
        {
            TempData["StatusMessage"] = "Add a field first to view its growth stage timeline.";
            return RedirectToAction("Create", "Fields");
        }

        var calc = await _growthStageService.CalculateAndSaveEstimateAsync(fieldCrop.Id, refresh, cancellationToken);
        var orderedStages = fieldCrop.Crop.CropStages
            .OrderBy(s => s.StageOrder)
            .ThenBy(s => s.GddThreshold)
            .ToList();

        var vm = new GrowthTimelineViewModel
        {
            FieldCropId = fieldCrop.Id,
            FieldId = fieldCrop.FieldId,
            FieldName = fieldCrop.Field.FieldName,
            LocationName = fieldCrop.Field.LocationName,
            CropName = fieldCrop.Crop.Name,
            Variety = fieldCrop.Variety,
            BaseTemperature = fieldCrop.Crop.BaseTemperature,
            SowingDate = fieldCrop.SowingDate,
            AccumulatedGdd = calc.AccumulatedGdd,
            TodayGdd = calc.TodayGdd,
            AverageRecentDailyGdd = calc.AverageRecentDailyGdd,
            ProgressPercent = calc.ProgressPercent,
            CurrentStageName = calc.CurrentStage.StageName,
            CurrentStageId = calc.CurrentStage.Id,
            NextStageName = calc.NextStage?.StageName,
            EstimatedNextStageDate = calc.EstimatedNextStageDate,
            UsedCachedFallback = calc.UsedCachedWeatherFallback,
            WeatherWarning = calc.WeatherWarning,
            DailyGddSeries = calc.DailySeries,
            Stages = orderedStages.Select(s =>
            {
                string status;
                if (s.Id == calc.CurrentStage.Id)
                {
                    status = "current";
                }
                else if (s.StageOrder < calc.CurrentStage.StageOrder)
                {
                    status = "completed";
                }
                else
                {
                    status = "upcoming";
                }

                return new StageTimelineItemViewModel
                {
                    StageId = s.Id,
                    StageOrder = s.StageOrder,
                    StageName = s.StageName,
                    GddThreshold = s.GddThreshold,
                    Status = status,
                    RemainingGdd = Math.Max(0, Math.Round(s.GddThreshold - calc.AccumulatedGdd, 1))
                };
            }).ToList()
        };

        return View(vm);
    }
}
