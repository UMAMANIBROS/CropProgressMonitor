using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Services;

/// <summary>
/// Queries historical growth estimates for individual and multi-field comparisons.
/// </summary>
public class HistoryService : IHistoryService
{
    private readonly ApplicationDbContext _dbContext;

    public HistoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<GrowthEstimate>> GetEstimatesForFieldCropAsync(
        int fieldCropId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.GrowthEstimates
            .AsNoTracking()
            .Include(g => g.CurrentStage)
            .Include(g => g.NextStage)
            .Where(g => g.FieldCropId == fieldCropId)
            .OrderByDescending(g => g.EstimateDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, List<GrowthEstimate>>> GetComparisonEstimatesAsync(
        IEnumerable<int> fieldCropIds,
        CancellationToken cancellationToken = default)
    {
        var ids = fieldCropIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, List<GrowthEstimate>>();
        }

        var estimates = await _dbContext.GrowthEstimates
            .AsNoTracking()
            .Include(g => g.CurrentStage)
            .Include(g => g.FieldCrop)
                .ThenInclude(fc => fc!.Field)
            .Include(g => g.FieldCrop)
                .ThenInclude(fc => fc!.Crop)
            .Where(g => ids.Contains(g.FieldCropId))
            .OrderBy(g => g.EstimateDate)
            .ToListAsync(cancellationToken);

        return estimates
            .GroupBy(g => g.FieldCropId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
