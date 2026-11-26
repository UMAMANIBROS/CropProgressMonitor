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
public class FieldsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<Farmer> _userManager;
    private readonly IWeatherService _weatherService;
    private readonly IGrowthStageService _growthStageService;

    public FieldsController(
        ApplicationDbContext dbContext,
        UserManager<Farmer> userManager,
        IWeatherService weatherService,
        IGrowthStageService growthStageService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _weatherService = weatherService;
        _growthStageService = growthStageService;
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var vm = new FieldFormViewModel
        {
            SowingDate = DateTime.UtcNow.Date.AddDays(-14),
            AvailableCrops = await GetCropSelectListAsync(cancellationToken)
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FieldFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.SowingDate.Date > DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(model.SowingDate), "Sowing date cannot be in the future.");
        }

        if (!ModelState.IsValid)
        {
            model.AvailableCrops = await GetCropSelectListAsync(cancellationToken);
            return View(model);
        }

        var farmerId = _userManager.GetUserId(User)!;

        var field = new Field
        {
            FarmerId = farmerId,
            FieldName = model.FieldName.Trim(),
            LocationName = model.LocationName.Trim(),
            Latitude = model.Latitude,
            Longitude = model.Longitude
        };

        _dbContext.Fields.Add(field);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var fieldCrop = new FieldCrop
        {
            FieldId = field.Id,
            CropId = model.CropId,
            Variety = string.IsNullOrWhiteSpace(model.Variety) ? null : model.Variety.Trim(),
            SowingDate = model.SowingDate.Date,
            IsActive = true
        };

        _dbContext.FieldCrops.Add(fieldCrop);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Immediately compute the first growth estimate
        await _growthStageService.CalculateAndSaveEstimateAsync(fieldCrop.Id, forceWeatherRefresh: true, cancellationToken);

        TempData["StatusMessage"] = $"Field '{field.FieldName}' added and initial growth stage estimated.";
        return RedirectToAction("Timeline", "Growth", new { id = fieldCrop.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var farmerId = _userManager.GetUserId(User)!;
        var fieldCrop = await _dbContext.FieldCrops
            .Include(fc => fc.Field)
            .FirstOrDefaultAsync(fc => fc.Id == id && fc.Field != null && fc.Field.FarmerId == farmerId, cancellationToken);

        if (fieldCrop == null || fieldCrop.Field == null)
        {
            return NotFound();
        }

        var vm = new FieldFormViewModel
        {
            FieldId = fieldCrop.FieldId,
            FieldCropId = fieldCrop.Id,
            FieldName = fieldCrop.Field.FieldName,
            LocationName = fieldCrop.Field.LocationName,
            Latitude = fieldCrop.Field.Latitude,
            Longitude = fieldCrop.Field.Longitude,
            CropId = fieldCrop.CropId,
            Variety = fieldCrop.Variety,
            SowingDate = fieldCrop.SowingDate.Date,
            AvailableCrops = await GetCropSelectListAsync(cancellationToken)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FieldFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.SowingDate.Date > DateTime.UtcNow.Date)
        {
            ModelState.AddModelError(nameof(model.SowingDate), "Sowing date cannot be in the future.");
        }

        if (!ModelState.IsValid)
        {
            model.AvailableCrops = await GetCropSelectListAsync(cancellationToken);
            return View(model);
        }

        var farmerId = _userManager.GetUserId(User)!;
        var fieldCrop = await _dbContext.FieldCrops
            .Include(fc => fc.Field)
            .FirstOrDefaultAsync(fc => fc.Id == id && fc.Field != null && fc.Field.FarmerId == farmerId, cancellationToken);

        if (fieldCrop == null || fieldCrop.Field == null)
        {
            return NotFound();
        }

        bool locationChanged = Math.Abs(fieldCrop.Field.Latitude - model.Latitude) > 0.0001 ||
                               Math.Abs(fieldCrop.Field.Longitude - model.Longitude) > 0.0001;

        fieldCrop.Field.FieldName = model.FieldName.Trim();
        fieldCrop.Field.LocationName = model.LocationName.Trim();
        fieldCrop.Field.Latitude = model.Latitude;
        fieldCrop.Field.Longitude = model.Longitude;
        fieldCrop.CropId = model.CropId;
        fieldCrop.Variety = string.IsNullOrWhiteSpace(model.Variety) ? null : model.Variety.Trim();
        fieldCrop.SowingDate = model.SowingDate.Date;

        if (locationChanged)
        {
            var oldWeather = await _dbContext.WeatherRecords
                .Where(w => w.FieldId == fieldCrop.FieldId)
                .ToListAsync(cancellationToken);
            _dbContext.WeatherRecords.RemoveRange(oldWeather);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _growthStageService.CalculateAndSaveEstimateAsync(fieldCrop.Id, forceWeatherRefresh: locationChanged, cancellationToken);

        TempData["StatusMessage"] = $"Field '{fieldCrop.Field.FieldName}' updated.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var farmerId = _userManager.GetUserId(User)!;
        var fieldCrop = await _dbContext.FieldCrops
            .Include(fc => fc.Field)
            .FirstOrDefaultAsync(fc => fc.Id == id && fc.Field != null && fc.Field.FarmerId == farmerId, cancellationToken);

        if (fieldCrop == null || fieldCrop.Field == null)
        {
            return NotFound();
        }

        var fieldName = fieldCrop.Field.FieldName;
        _dbContext.Fields.Remove(fieldCrop.Field);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Field '{fieldName}' deleted.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> SearchLocation(string query, CancellationToken cancellationToken)
    {
        var results = await _weatherService.SearchLocationsAsync(query, cancellationToken);
        return Json(results);
    }

    private async Task<List<SelectListItem>> GetCropSelectListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Crops
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Name} (Tbase {c.BaseTemperature}°C)"
            })
            .ToListAsync(cancellationToken);
    }
}
