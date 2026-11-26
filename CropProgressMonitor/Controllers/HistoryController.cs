using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.Services.Interfaces;
using CropProgressMonitor.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Controllers;

[Authorize]
public class HistoryController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<Farmer> _userManager;
    private readonly IHistoryService _historyService;
    private readonly IGrowthStageService _growthStageService;

    public HistoryController(
        ApplicationDbContext dbContext,
        UserManager<Farmer> userManager,
        IHistoryService historyService,
        IGrowthStageService growthStageService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _historyService = historyService;
        _growthStageService = growthStageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? fieldCropId, int? compareFieldCropId, CancellationToken cancellationToken = default)
    {
        var farmerId = _userManager.GetUserId(User)!;
        var fieldCrops = await _dbContext.FieldCrops
            .AsNoTracking()
            .Include(fc => fc.Field)
            .Include(fc => fc.Crop)
            .Where(fc => fc.Field != null && fc.Field.FarmerId == farmerId)
            .OrderBy(fc => fc.Field!.FieldName)
            .ToListAsync(cancellationToken);

        if (fieldCrops.Count == 0)
        {
            TempData["StatusMessage"] = "Add a field first to track growth estimate history.";
            return RedirectToAction("Create", "Fields");
        }

        var primary = fieldCropId.HasValue
            ? fieldCrops.FirstOrDefault(fc => fc.Id == fieldCropId.Value)
            : fieldCrops.First();

        if (primary == null)
        {
            return NotFound();
        }

        // Ensure today's estimate exists before showing history
        await _growthStageService.CalculateAndSaveEstimateAsync(primary.Id, false, cancellationToken);

        var compare = compareFieldCropId.HasValue && compareFieldCropId.Value != primary.Id
            ? fieldCrops.FirstOrDefault(fc => fc.Id == compareFieldCropId.Value)
            : null;

        if (compare != null)
        {
            await _growthStageService.CalculateAndSaveEstimateAsync(compare.Id, false, cancellationToken);
        }

        var estimates = await _historyService.GetEstimatesForFieldCropAsync(primary.Id, cancellationToken);

        // Also compute daily cumulative GDD trajectory from WeatherRecords so the chart is rich even on Day 1!
        var primaryCalc = await _growthStageService.CalculateAndSaveEstimateAsync(primary.Id, false, cancellationToken);
        var primarySeries = primaryCalc.DailySeries.Select(d => new ChartPointViewModel
        {
            DateLabel = d.Date.ToString("MMM dd"),
            DaysAfterSowing = Math.Max(0, (d.Date - primary.SowingDate.Date).Days),
            AccumulatedGdd = d.CumulativeGdd
        }).ToList();

        var compareSeries = new List<ChartPointViewModel>();
        if (compare != null)
        {
            var compareCalc = await _growthStageService.CalculateAndSaveEstimateAsync(compare.Id, false, cancellationToken);
            compareSeries = compareCalc.DailySeries.Select(d => new ChartPointViewModel
            {
                DateLabel = d.Date.ToString("MMM dd"),
                DaysAfterSowing = Math.Max(0, (d.Date - compare.SowingDate.Date).Days),
                AccumulatedGdd = d.CumulativeGdd
            }).ToList();
        }

        var vm = new HistoryViewModel
        {
            SelectedFieldCropId = primary.Id,
            CompareFieldCropId = compare?.Id,
            SelectedLabel = $"{primary.Field!.FieldName} ({primary.Crop!.Name})",
            CompareLabel = compare != null ? $"{compare.Field!.FieldName} ({compare.Crop!.Name})" : null,
            FieldCrops = fieldCrops.Select(fc => new SelectListItem
            {
                Value = fc.Id.ToString(),
                Text = $"{fc.Field!.FieldName} — {fc.Crop!.Name} (Sown {fc.SowingDate:dd MMM yyyy})",
                Selected = fc.Id == primary.Id
            }).ToList(),
            Estimates = estimates.Select(e => new HistoryRowViewModel
            {
                EstimateDate = e.EstimateDate,
                AccumulatedGdd = e.AccumulatedGdd,
                CurrentStageName = e.CurrentStage?.StageName ?? "Unknown",
                NextStageName = e.NextStage?.StageName,
                NextStageDate = e.NextStageDate
            }).ToList(),
            PrimarySeries = primarySeries,
            CompareSeries = compareSeries
        };

        return View(vm);
    }
}
