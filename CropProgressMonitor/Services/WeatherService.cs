using System.Globalization;
using System.Text.Json;
using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.Services.DTOs;
using CropProgressMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CropProgressMonitor.Services;

/// <summary>
/// Integrates with Open-Meteo Geocoding, Archive, and Forecast APIs with retry and SQL Server caching.
/// </summary>
public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _dbContext;
    private readonly OpenMeteoOptions _options;
    private readonly ILogger<WeatherService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WeatherService(
        HttpClient httpClient,
        ApplicationDbContext dbContext,
        IOptions<OpenMeteoOptions> options,
        ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<GeocodingResultDto>> SearchLocationsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new List<GeocodingResultDto>();
        }

        var url = $"{_options.GeocodingUrl}?name={Uri.EscapeDataString(query.Trim())}&count=6&language=en&format=json";
        try
        {
            using var response = await SendWithRetryAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Open-Meteo geocoding returned status {StatusCode}", response.StatusCode);
                return new List<GeocodingResultDto>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<OpenMeteoGeocodingResponse>(stream, JsonOptions, cancellationToken);

            return payload?.Results?
                .Select(r => new GeocodingResultDto
                {
                    Name = r.Name ?? string.Empty,
                    Latitude = r.Latitude,
                    Longitude = r.Longitude,
                    Country = r.Country,
                    Admin1 = r.Admin1
                })
                .ToList() ?? new List<GeocodingResultDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Open-Meteo geocoding for '{Query}'.", query);
            return new List<GeocodingResultDto>();
        }
    }

    public async Task<WeatherSyncResultDto> SyncFieldWeatherAsync(
        Field field,
        DateTime fromDate,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var startDate = fromDate.Date;
        var today = DateTime.UtcNow.Date;
        if (startDate > today)
        {
            startDate = today;
        }

        var forecastWindowEnd = today.AddDays(6);

        var existingRecords = await _dbContext.WeatherRecords
            .Where(w => w.FieldId == field.Id && w.Date >= startDate && w.Date <= forecastWindowEnd)
            .OrderBy(w => w.Date)
            .ToListAsync(cancellationToken);

        var existingByDate = existingRecords.ToDictionary(w => w.Date.Date);

        // Determine whether historical archive data is missing
        var archiveBoundary = today.AddDays(-6);
        bool needsArchive = startDate <= archiveBoundary &&
            (forceRefresh || Enumerable.Range(0, (archiveBoundary - startDate).Days + 1)
                .Select(offset => startDate.AddDays(offset))
                .Any(d => !existingByDate.TryGetValue(d, out var rec) || rec.IsForecast));

        // Determine whether recent + 7-day forecast needs refresh
        bool needsForecast = forceRefresh ||
            !existingByDate.ContainsKey(today) ||
            existingByDate.Values.Any(w => w.Date >= today.AddDays(-2) && w.IsForecast) ||
            Enumerable.Range(0, 7).Any(i => !existingByDate.ContainsKey(today.AddDays(i)));

        var warnings = new List<string>();
        bool usedFallback = false;
        CurrentConditionsDto? currentConditions = null;

        if (needsArchive)
        {
            try
            {
                var archiveEnd = archiveBoundary < startDate ? startDate : archiveBoundary;
                var archivePoints = await FetchDailyRangeAsync(
                    _options.ArchiveUrl,
                    field.Latitude,
                    field.Longitude,
                    startDate,
                    archiveEnd,
                    isForecast: false,
                    cancellationToken);

                UpsertWeatherRecords(field.Id, archivePoints, existingByDate, overwriteNonForecast: forceRefresh);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Open-Meteo archive API failed for field {FieldId}. Falling back to cached records.", field.Id);
                usedFallback = true;
                warnings.Add("Historical weather service is temporarily unreachable; using cached records where available.");
            }
        }

        if (needsForecast)
        {
            try
            {
                var pastDays = Math.Min(14, Math.Max(1, (today - startDate).Days + 1));
                var (forecastPoints, current) = await FetchForecastAsync(
                    field.Latitude,
                    field.Longitude,
                    pastDays,
                    forecastDays: 7,
                    cancellationToken);

                currentConditions = current;

                // Only persist points >= startDate
                var relevantPoints = forecastPoints
                    .Where(p => p.Date >= startDate)
                    .ToList();

                UpsertWeatherRecords(field.Id, relevantPoints, existingByDate, overwriteNonForecast: forceRefresh);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Open-Meteo forecast API failed for field {FieldId}. Falling back to cached records.", field.Id);
                usedFallback = true;
                warnings.Add("Live forecast API is temporarily unreachable; displaying cached weather data.");
            }
        }
        else
        {
            // Try lightweight current conditions fetch without blocking if forecast already cached
            try
            {
                var (_, current) = await FetchForecastAsync(
                    field.Latitude,
                    field.Longitude,
                    pastDays: 1,
                    forecastDays: 7,
                    cancellationToken);
                currentConditions = current;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Unable to refresh current conditions for field {FieldId}; using cached daily records.", field.Id);
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var mergedRecords = existingByDate.Values
            .OrderBy(w => w.Date)
            .ToList();

        return new WeatherSyncResultDto
        {
            Records = mergedRecords,
            Current = currentConditions,
            UsedCachedFallback = usedFallback,
            WarningMessage = warnings.Count > 0 ? string.Join(" ", warnings) : null
        };
    }

    public async Task<WeatherSummaryDto> GetFieldWeatherSummaryAsync(
        Field field,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var summary = new WeatherSummaryDto();

        try
        {
            var (points, current) = await FetchForecastAsync(
                field.Latitude,
                field.Longitude,
                pastDays: 0,
                forecastDays: 7,
                cancellationToken);

            summary.Current = current;
            summary.Forecast7Days = points
                .Where(p => p.Date >= today)
                .OrderBy(p => p.Date)
                .Take(7)
                .ToList();

            // Cache forecast records in DB too
            var existingByDate = await _dbContext.WeatherRecords
                .Where(w => w.FieldId == field.Id && w.Date >= today && w.Date <= today.AddDays(6))
                .ToDictionaryAsync(w => w.Date.Date, cancellationToken);

            UpsertWeatherRecords(field.Id, summary.Forecast7Days, existingByDate, overwriteNonForecast: false);
            if (_dbContext.ChangeTracker.HasChanges())
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open-Meteo forecast failed in WeatherSummary for field {FieldId}. Using cached data.", field.Id);
            summary.UsedCachedFallback = true;
            summary.WarningMessage = "Open-Meteo is currently unreachable. Showing cached forecast data from the database.";

            var cached = await _dbContext.WeatherRecords
                .Where(w => w.FieldId == field.Id && w.Date >= today)
                .OrderBy(w => w.Date)
                .Take(7)
                .ToListAsync(cancellationToken);

            summary.Forecast7Days = cached.Select(c => new DailyWeatherPointDto
            {
                Date = c.Date,
                Tmax = c.Tmax,
                Tmin = c.Tmin,
                Rainfall = c.Rainfall,
                IsForecast = c.IsForecast
            }).ToList();
        }

        return summary;
    }

    private async Task<List<DailyWeatherPointDto>> FetchDailyRangeAsync(
        string baseUrl,
        double latitude,
        double longitude,
        DateTime startDate,
        DateTime endDate,
        bool isForecast,
        CancellationToken cancellationToken)
    {
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "{0}?latitude={1:F4}&longitude={2:F4}&start_date={3:yyyy-MM-dd}&end_date={4:yyyy-MM-dd}&daily=temperature_2m_max,temperature_2m_min,precipitation_sum&timezone=auto",
            baseUrl, latitude, longitude, startDate, endDate);

        using var response = await SendWithRetryAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OpenMeteoForecastResponse>(stream, JsonOptions, cancellationToken);
        return ParseDailySeries(payload?.Daily, isForecast, DateTime.UtcNow.Date);
    }

    private async Task<(List<DailyWeatherPointDto> Points, CurrentConditionsDto? Current)> FetchForecastAsync(
        double latitude,
        double longitude,
        int pastDays,
        int forecastDays,
        CancellationToken cancellationToken)
    {
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "{0}?latitude={1:F4}&longitude={2:F4}&current=temperature_2m,relative_humidity_2m,wind_speed_10m,precipitation,weather_code&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,weather_code&past_days={3}&forecast_days={4}&timezone=auto",
            _options.ForecastUrl, latitude, longitude, pastDays, forecastDays);

        using var response = await SendWithRetryAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<OpenMeteoForecastResponse>(stream, JsonOptions, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var points = ParseDailySeries(payload?.Daily, isForecast: true, today);

        CurrentConditionsDto? current = null;
        if (payload?.Current != null)
        {
            DateTime.TryParse(payload.Current.Time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var observed);
            current = new CurrentConditionsDto
            {
                Temperature = payload.Current.Temperature_2m ?? 0,
                RelativeHumidity = payload.Current.Relative_humidity_2m ?? 0,
                WindSpeed = payload.Current.Wind_speed_10m ?? 0,
                Precipitation = payload.Current.Precipitation ?? 0,
                WeatherCode = payload.Current.Weather_code ?? 0,
                ObservedAt = observed == default ? DateTime.UtcNow : observed
            };
        }

        return (points, current);
    }

    private static List<DailyWeatherPointDto> ParseDailySeries(
        OpenMeteoDailyBlock? daily,
        bool isForecast,
        DateTime today)
    {
        var results = new List<DailyWeatherPointDto>();
        if (daily?.Time == null || daily.Temperature_2m_max == null || daily.Temperature_2m_min == null)
        {
            return results;
        }

        for (int i = 0; i < daily.Time.Count; i++)
        {
            if (!DateTime.TryParseExact(daily.Time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (i >= daily.Temperature_2m_max.Count || i >= daily.Temperature_2m_min.Count)
            {
                continue;
            }

            var tmax = daily.Temperature_2m_max[i];
            var tmin = daily.Temperature_2m_min[i];
            if (!tmax.HasValue || !tmin.HasValue)
            {
                continue;
            }

            var rain = daily.Precipitation_sum != null && i < daily.Precipitation_sum.Count
                ? daily.Precipitation_sum[i] ?? 0
                : 0;

            var code = daily.Weather_code != null && i < daily.Weather_code.Count
                ? daily.Weather_code[i]
                : null;

            results.Add(new DailyWeatherPointDto
            {
                Date = date.Date,
                Tmax = Math.Round(tmax.Value, 2),
                Tmin = Math.Round(tmin.Value, 2),
                Rainfall = Math.Round(rain, 2),
                WeatherCode = code,
                IsForecast = isForecast && date.Date >= today
            });
        }

        return results;
    }

    private void UpsertWeatherRecords(
        int fieldId,
        IEnumerable<DailyWeatherPointDto> points,
        Dictionary<DateTime, WeatherRecord> existingByDate,
        bool overwriteNonForecast)
    {
        foreach (var point in points)
        {
            var date = point.Date.Date;
            if (existingByDate.TryGetValue(date, out var existing))
            {
                if (existing.IsForecast || overwriteNonForecast)
                {
                    existing.Tmax = point.Tmax;
                    existing.Tmin = point.Tmin;
                    existing.Rainfall = point.Rainfall;
                    existing.IsForecast = point.IsForecast;
                }
            }
            else
            {
                var record = new WeatherRecord
                {
                    FieldId = fieldId,
                    Date = date,
                    Tmax = point.Tmax,
                    Tmin = point.Tmin,
                    Rainfall = point.Rainfall,
                    IsForecast = point.IsForecast
                };
                _dbContext.WeatherRecords.Add(record);
                existingByDate[date] = record;
            }
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if ((int)response.StatusCode < 500 || attempt == maxAttempts)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < maxAttempts)
            {
                lastException = ex;
                _logger.LogInformation(ex, "Retrying Open-Meteo request (attempt {Attempt}/{MaxAttempts}).", attempt, maxAttempts);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
        }

        throw lastException ?? new HttpRequestException($"Failed to reach Open-Meteo endpoint: {url}");
    }

    // Internal deserialization contracts for Open-Meteo JSON payloads
    private sealed class OpenMeteoGeocodingResponse
    {
        public List<GeocodingItem>? Results { get; set; }
    }

    private sealed class GeocodingItem
    {
        public string? Name { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Country { get; set; }
        public string? Admin1 { get; set; }
    }

    private sealed class OpenMeteoForecastResponse
    {
        public OpenMeteoCurrentBlock? Current { get; set; }
        public OpenMeteoDailyBlock? Daily { get; set; }
    }

    private sealed class OpenMeteoCurrentBlock
    {
        public string? Time { get; set; }
        public double? Temperature_2m { get; set; }
        public double? Relative_humidity_2m { get; set; }
        public double? Wind_speed_10m { get; set; }
        public double? Precipitation { get; set; }
        public int? Weather_code { get; set; }
    }

    private sealed class OpenMeteoDailyBlock
    {
        public List<string>? Time { get; set; }
        public List<double?>? Temperature_2m_max { get; set; }
        public List<double?>? Temperature_2m_min { get; set; }
        public List<double?>? Precipitation_sum { get; set; }
        public List<int?>? Weather_code { get; set; }
    }
}
