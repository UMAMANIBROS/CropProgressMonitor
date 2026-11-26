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
public class AdvisoryController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<Farmer> _userManager;
    private readonly IGrowthStageService _growthStageService;
    private readonly IAdvisoryService _advisoryService;

    public AdvisoryController(
        ApplicationDbContext dbContext,
        UserManager<Farmer> userManager,
        IGrowthStageService growthStageService,
        IAdvisoryService advisoryService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _growthStageService = growthStageService;
        _advisoryService = advisoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? fieldCropId, string? lang, CancellationToken cancellationToken = default)
    {
        var farmer = await _userManager.GetUserAsync(User);
        if (farmer == null)
        {
            return Challenge();
        }

        var fieldCrops = await _dbContext.FieldCrops
            .AsNoTracking()
            .Include(fc => fc.Field)
            .Include(fc => fc.Crop)
            .Where(fc => fc.Field != null && fc.Field.FarmerId == farmer.Id && fc.IsActive)
            .OrderBy(fc => fc.Field!.FieldName)
            .ToListAsync(cancellationToken);

        if (fieldCrops.Count == 0)
        {
            TempData["StatusMessage"] = "Add a field first to receive stage-specific advisories.";
            return RedirectToAction("Create", "Fields");
        }

        var selected = fieldCropId.HasValue
            ? fieldCrops.FirstOrDefault(fc => fc.Id == fieldCropId.Value)
            : fieldCrops.First();

        if (selected == null)
        {
            return NotFound();
        }

        var activeLanguage = !string.IsNullOrWhiteSpace(lang) ? lang : farmer.PreferredLanguage;
        var calc = await _growthStageService.CalculateAndSaveEstimateAsync(selected.Id, false, cancellationToken);
        var advisory = await _advisoryService.GetAdvisoryForFieldCropAsync(
            selected.Id,
            calc.CurrentStage.Id,
            activeLanguage,
            cancellationToken);

        var vm = new AdvisoryViewModel
        {
            FieldCropId = selected.Id,
            FieldName = selected.Field!.FieldName,
            CropName = selected.Crop!.Name,
            AccumulatedGdd = calc.AccumulatedGdd,
            Language = advisory.Language,
            Advisory = advisory,
            FieldCrops = fieldCrops.Select(fc => new SelectListItem
            {
                Value = fc.Id.ToString(),
                Text = $"{fc.Field!.FieldName} — {fc.Crop!.Name}",
                Selected = fc.Id == selected.Id
            }).ToList()
        };

        return View(vm);
    }
}
