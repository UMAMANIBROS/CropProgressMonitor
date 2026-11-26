using System.Diagnostics;
using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext dbContext, ILogger<HomeController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var crops = await _dbContext.Crops
            .AsNoTracking()
            .Include(c => c.CropStages.OrderBy(s => s.StageOrder))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var totalFields = await _dbContext.Fields.AsNoTracking().CountAsync(cancellationToken);

        var vm = new HomeViewModel
        {
            TotalCrops = crops.Count,
            TotalStages = crops.Sum(c => c.CropStages.Count),
            TotalRegisteredFields = totalFields,
            Crops = crops.Select(c => new HomeCropShowcaseItem
            {
                Id = c.Id,
                Name = c.Name,
                BaseTemperature = c.BaseTemperature,
                Description = c.Description,
                Stages = c.CropStages
                    .OrderBy(s => s.StageOrder)
                    .Select(s => new HomeStagePreviewItem
                    {
                        StageOrder = s.StageOrder,
                        StageName = s.StageName,
                        GddThreshold = s.GddThreshold
                    })
                    .ToList()
            }).ToList()
        };

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogError("Unhandled error served via Home/Error. RequestId={RequestId}", requestId);
        return View(new ErrorViewModel { RequestId = requestId });
    }
}
