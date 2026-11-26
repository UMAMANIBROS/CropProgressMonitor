namespace CropProgressMonitor.Services.DTOs;

public class GeocodingResultDto
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Country { get; set; }
    public string? Admin1 { get; set; }

    public string DisplayName => string.Join(", ",
        new[] { Name, Admin1, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class DailyWeatherPointDto
{
    public DateTime Date { get; set; }
    public double Tmax { get; set; }
    public double Tmin { get; set; }
    public double Rainfall { get; set; }
    public int? WeatherCode { get; set; }
    public bool IsForecast { get; set; }
}

public class CurrentConditionsDto
{
    public double Temperature { get; set; }
    public double RelativeHumidity { get; set; }
    public double WindSpeed { get; set; }
    public double Precipitation { get; set; }
    public int WeatherCode { get; set; }
    public DateTime ObservedAt { get; set; }
}

public class WeatherSummaryDto
{
    public CurrentConditionsDto? Current { get; set; }
    public List<DailyWeatherPointDto> Forecast7Days { get; set; } = new();
    public bool UsedCachedFallback { get; set; }
    public string? WarningMessage { get; set; }
}

public class WeatherSyncResultDto
{
    public List<Models.WeatherRecord> Records { get; set; } = new();
    public CurrentConditionsDto? Current { get; set; }
    public bool UsedCachedFallback { get; set; }
    public string? WarningMessage { get; set; }
}
