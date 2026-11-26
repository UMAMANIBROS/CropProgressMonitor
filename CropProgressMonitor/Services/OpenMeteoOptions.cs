namespace CropProgressMonitor.Services;

/// <summary>
/// Configuration options for Open-Meteo API endpoints loaded from appsettings.json.
/// </summary>
public class OpenMeteoOptions
{
    public const string SectionName = "OpenMeteo";

    public string GeocodingUrl { get; set; } = "https://geocoding-api.open-meteo.com/v1/search";
    public string ForecastUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";
    public string ArchiveUrl { get; set; } = "https://archive-api.open-meteo.com/v1/archive";
}
