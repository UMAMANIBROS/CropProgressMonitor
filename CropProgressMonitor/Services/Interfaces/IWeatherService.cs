using CropProgressMonitor.Models;
using CropProgressMonitor.Services.DTOs;

namespace CropProgressMonitor.Services.Interfaces;

public interface IWeatherService
{
    Task<List<GeocodingResultDto>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default);

    Task<WeatherSyncResultDto> SyncFieldWeatherAsync(
        Field field,
        DateTime fromDate,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    Task<WeatherSummaryDto> GetFieldWeatherSummaryAsync(
        Field field,
        CancellationToken cancellationToken = default);
}
