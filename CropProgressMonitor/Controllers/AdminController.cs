using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Controllers;

[Authorize(Roles = DbInitializer.AdminRole)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public AdminController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var crops = await _dbContext.Crops
            .Include(c => c.CropStages.OrderBy(s => s.StageOrder))
                .ThenInclude(s => s.Advisories)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return View(new AdminCropsViewModel { Crops = crops });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCrop(AdminCropsViewModel model, CancellationToken cancellationToken)
    {
        var cropVm = model.NewCrop;
        if (!TryValidateModel(cropVm, nameof(model.NewCrop)))
        {
            model.Crops = await _dbContext.Crops
                .Include(c => c.CropStages.OrderBy(s => s.StageOrder))
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
            return View("Index", model);
        }

        if (await _dbContext.Crops.AnyAsync(c => c.Name.ToLower() == cropVm.Name.Trim().ToLower(), cancellationToken))
        {
            TempData["ErrorMessage"] = $"A crop named '{cropVm.Name}' already exists.";
            return RedirectToAction(nameof(Index));
        }

        var crop = new Crop
        {
            Name = cropVm.Name.Trim(),
            BaseTemperature = cropVm.BaseTemperature,
            Description = cropVm.Description?.Trim()
        };

        // Add default initial Sowing stage so the crop is immediately usable
        crop.CropStages.Add(new CropStage
        {
            StageOrder = 1,
            StageName = "Sowing",
            GddThreshold = 0,
            Advisories = new List<Advisory>
            {
                new()
                {
                    WaterAdvice = "Ensure pre-sowing field moisture is uniform.",
                    NutrientAdvice = "Apply recommended basal fertilizer dose at sowing.",
                    RiskAdvice = "Use certified, treated seed."
                }
            }
        });

        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Crop '{crop.Name}' added with default Sowing stage. Add more growth stages below.";
        return RedirectToAction(nameof(Stages), new { cropId = crop.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Stages(int cropId, CancellationToken cancellationToken)
    {
        var crop = await _dbContext.Crops
            .Include(c => c.CropStages.OrderBy(s => s.StageOrder))
                .ThenInclude(s => s.Advisories)
            .FirstOrDefaultAsync(c => c.Id == cropId, cancellationToken);

        if (crop == null)
        {
            return NotFound();
        }

        ViewBag.Crop = crop;
        var nextOrder = crop.CropStages.Any() ? crop.CropStages.Max(s => s.StageOrder) + 1 : 1;
        return View(new CropStageFormViewModel
        {
            CropId = crop.Id,
            CropName = crop.Name,
            StageOrder = nextOrder
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStage(CropStageFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Crop = await _dbContext.Crops
                .Include(c => c.CropStages.OrderBy(s => s.StageOrder))
                    .ThenInclude(s => s.Advisories)
                .FirstOrDefaultAsync(c => c.Id == model.CropId, cancellationToken);
            return View("Stages", model);
        }

        if (model.StageId.HasValue)
        {
            var stage = await _dbContext.CropStages
                .Include(s => s.Advisories)
                .FirstOrDefaultAsync(s => s.Id == model.StageId.Value && s.CropId == model.CropId, cancellationToken);

            if (stage == null)
            {
                return NotFound();
            }

            stage.StageOrder = model.StageOrder;
            stage.StageName = model.StageName.Trim();
            stage.GddThreshold = model.GddThreshold;

            var advisory = stage.Advisories.FirstOrDefault();
            if (advisory == null)
            {
                advisory = new Advisory { CropStageId = stage.Id };
                _dbContext.Advisories.Add(advisory);
            }

            advisory.WaterAdvice = model.WaterAdvice.Trim();
            advisory.NutrientAdvice = model.NutrientAdvice.Trim();
            advisory.RiskAdvice = model.RiskAdvice.Trim();
            advisory.WaterAdviceHi = model.WaterAdviceHi?.Trim();
            advisory.NutrientAdviceHi = model.NutrientAdviceHi?.Trim();
            advisory.RiskAdviceHi = model.RiskAdviceHi?.Trim();
            advisory.WaterAdvicePa = model.WaterAdvicePa?.Trim();
            advisory.NutrientAdvicePa = model.NutrientAdvicePa?.Trim();
            advisory.RiskAdvicePa = model.RiskAdvicePa?.Trim();

            TempData["StatusMessage"] = $"Updated stage '{stage.StageName}'.";
        }
        else
        {
            var stage = new CropStage
            {
                CropId = model.CropId,
                StageOrder = model.StageOrder,
                StageName = model.StageName.Trim(),
                GddThreshold = model.GddThreshold,
                Advisories = new List<Advisory>
                {
                    new()
                    {
                        WaterAdvice = model.WaterAdvice.Trim(),
                        NutrientAdvice = model.NutrientAdvice.Trim(),
                        RiskAdvice = model.RiskAdvice.Trim(),
                        WaterAdviceHi = model.WaterAdviceHi?.Trim(),
                        NutrientAdviceHi = model.NutrientAdviceHi?.Trim(),
                        RiskAdviceHi = model.RiskAdviceHi?.Trim(),
                        WaterAdvicePa = model.WaterAdvicePa?.Trim(),
                        NutrientAdvicePa = model.NutrientAdvicePa?.Trim(),
                        RiskAdvicePa = model.RiskAdvicePa?.Trim()
                    }
                }
            };

            _dbContext.CropStages.Add(stage);
            TempData["StatusMessage"] = $"Added stage '{stage.StageName}'.";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Stages), new { cropId = model.CropId });
    }

    [HttpGet]
    public async Task<IActionResult> EditStage(int stageId, CancellationToken cancellationToken)
    {
        var stage = await _dbContext.CropStages
            .Include(s => s.Crop)
            .Include(s => s.Advisories)
            .FirstOrDefaultAsync(s => s.Id == stageId, cancellationToken);

        if (stage == null || stage.Crop == null)
        {
            return NotFound();
        }

        var advisory = stage.Advisories.FirstOrDefault();
        ViewBag.Crop = await _dbContext.Crops
            .Include(c => c.CropStages.OrderBy(s => s.StageOrder))
                .ThenInclude(s => s.Advisories)
            .FirstOrDefaultAsync(c => c.Id == stage.CropId, cancellationToken);

        var vm = new CropStageFormViewModel
        {
            StageId = stage.Id,
            CropId = stage.CropId,
            CropName = stage.Crop.Name,
            StageOrder = stage.StageOrder,
            StageName = stage.StageName,
            GddThreshold = stage.GddThreshold,
            WaterAdvice = advisory?.WaterAdvice ?? string.Empty,
            NutrientAdvice = advisory?.NutrientAdvice ?? string.Empty,
            RiskAdvice = advisory?.RiskAdvice ?? string.Empty,
            WaterAdviceHi = advisory?.WaterAdviceHi,
            NutrientAdviceHi = advisory?.NutrientAdviceHi,
            RiskAdviceHi = advisory?.RiskAdviceHi,
            WaterAdvicePa = advisory?.WaterAdvicePa,
            NutrientAdvicePa = advisory?.NutrientAdvicePa,
            RiskAdvicePa = advisory?.RiskAdvicePa
        };

        return View("Stages", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStage(int stageId, int cropId, CancellationToken cancellationToken)
    {
        var isReferenced = await _dbContext.GrowthEstimates
            .AnyAsync(g => g.CurrentStageId == stageId || g.NextStageId == stageId, cancellationToken);

        if (isReferenced)
        {
            TempData["ErrorMessage"] = "Cannot delete a stage that is already referenced by historical growth estimates.";
            return RedirectToAction(nameof(Stages), new { cropId });
        }

        var stage = await _dbContext.CropStages.FirstOrDefaultAsync(s => s.Id == stageId, cancellationToken);
        if (stage != null)
        {
            _dbContext.CropStages.Remove(stage);
            await _dbContext.SaveChangesAsync(cancellationToken);
            TempData["StatusMessage"] = $"Stage '{stage.StageName}' deleted.";
        }

        return RedirectToAction(nameof(Stages), new { cropId });
    }
}
