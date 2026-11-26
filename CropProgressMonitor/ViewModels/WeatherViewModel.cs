using CropProgressMonitor.Services.DTOs;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CropProgressMonitor.ViewModels;

public class WeatherViewModel
{
    public int SelectedFieldId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public CurrentConditionsDto? Current { get; set; }
    public List<DailyWeatherPointDto> Forecast7Days { get; set; } = new();
    public bool UsedCachedFallback { get; set; }
    public string? WarningMessage { get; set; }
    public List<SelectListItem> Fields { get; set; } = new();
}
