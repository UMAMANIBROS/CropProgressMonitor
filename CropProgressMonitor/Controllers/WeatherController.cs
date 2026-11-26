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
public class WeatherController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<Farmer> _userManager;
    private readonly IWeatherService _weatherService;

    public WeatherController(
        ApplicationDbContext dbContext,
        UserManager<Farmer> userManager,
        IWeatherService weatherService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _weatherService = weatherService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? fieldId, CancellationToken cancellationToken = default)
    {
        var farmerId = _userManager.GetUserId(User)!;
        var fields = await _dbContext.Fields
            .AsNoTracking()
            .Where(f => f.FarmerId == farmerId)
            .OrderBy(f => f.FieldName)
            .ToListAsync(cancellationToken);

        if (fields.Count == 0)
        {
            TempData["StatusMessage"] = "Add a field first to view local weather forecasts.";
            return RedirectToAction("Create", "Fields");
        }

        var selected = fieldId.HasValue
            ? fields.FirstOrDefault(f => f.Id == fieldId.Value)
            : fields.First();

        if (selected == null)
        {
            return NotFound();
        }

        var summary = await _weatherService.GetFieldWeatherSummaryAsync(selected, cancellationToken);

        var vm = new WeatherViewModel
        {
            SelectedFieldId = selected.Id,
            FieldName = selected.FieldName,
            LocationName = selected.LocationName,
            Latitude = selected.Latitude,
            Longitude = selected.Longitude,
            Current = summary.Current,
            Forecast7Days = summary.Forecast7Days,
            UsedCachedFallback = summary.UsedCachedFallback,
            WarningMessage = summary.WarningMessage,
            Fields = fields.Select(f => new SelectListItem
            {
                Value = f.Id.ToString(),
                Text = $"{f.FieldName} ({f.LocationName})",
                Selected = f.Id == selected.Id
            }).ToList()
        };

        return View(vm);
    }
}
