using CropProgressMonitor.Models;

namespace CropProgressMonitor.Services.Interfaces;

public interface IHistoryService
{
    Task<List<GrowthEstimate>> GetEstimatesForFieldCropAsync(int fieldCropId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<GrowthEstimate>>> GetComparisonEstimatesAsync(IEnumerable<int> fieldCropIds, CancellationToken cancellationToken = default);
}
