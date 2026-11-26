using CropProgressMonitor.Data;
using CropProgressMonitor.Models;
using CropProgressMonitor.Services;
using CropProgressMonitor.Services.DTOs;
using CropProgressMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CropProgressMonitor.Tests;

public class GrowthStageServiceTests
{
    private static List<CropStage> CreateSampleWheatStages() =>
    [
        new CropStage { Id = 1, CropId = 1, StageOrder = 1, StageName = "Sowing", GddThreshold = 0 },
        new CropStage { Id = 2, CropId = 1, StageOrder = 2, StageName = "Germination", GddThreshold = 120 },
        new CropStage { Id = 3, CropId = 1, StageOrder = 3, StageName = "Vegetative Growth", GddThreshold = 380 },
        new CropStage { Id = 4, CropId = 1, StageOrder = 4, StageName = "Flowering", GddThreshold = 820 }
    ];

    [Theory]
    [InlineData(25.0, 15.0, 5.0, 15.0)]
    [InlineData(10.0, 0.0, 5.0, 0.0)]
    [InlineData(6.0, 2.0, 5.0, 0.0)]
    [InlineData(32.0, 20.0, 10.0, 16.0)]
    public void CalculateDailyGdd_ReturnsExpectedThermalTime(double tmax, double tmin, double tbase, double expected)
    {
        var gdd = GrowthStageService.CalculateDailyGdd(tmax, tmin, tbase);
        Assert.Equal(expected, gdd, precision: 2);
    }

    [Fact]
    public void EvaluateProgress_SelectsLatestReachedStage_AndAccumulatesGdd()
    {
        var sowing = new DateTime(2026, 11, 1);
        var today = new DateTime(2026, 11, 10); // 10 days inclusive -> 10 * 15 GDD = 150 GDD
        var stages = CreateSampleWheatStages();

        var weather = Enumerable.Range(0, 10)
            .Select(i => new WeatherRecord
            {
                FieldId = 1,
                Date = sowing.AddDays(i),
                Tmax = 25.0,
                Tmin = 15.0,
                Rainfall = 0
            })
            .ToList();

        var result = GrowthStageService.EvaluateProgress(weather, stages, sowing, today, baseTemperature: 5.0);

        Assert.Equal(150.0, result.AccumulatedGdd, precision: 1);
        Assert.Equal("Germination", result.CurrentStage.StageName);
        Assert.NotNull(result.NextStage);
        Assert.Equal("Vegetative Growth", result.NextStage!.StageName);
    }

    [Fact]
    public void EvaluateProgress_ProjectsNextStageDate_Within7DayForecast()
    {
        var sowing = new DateTime(2026, 11, 1);
        var today = new DateTime(2026, 11, 6); // 6 days * 15 = 90 GDD
        var stages = CreateSampleWheatStages(); // Germination = 120 GDD

        var weather = new List<WeatherRecord>();
        for (int i = 0; i < 6; i++)
        {
            weather.Add(new WeatherRecord { Date = sowing.AddDays(i), Tmax = 25, Tmin = 15 }); // 15/day -> 90 GDD
        }
        // Forecast days: 15 GDD/day -> Nov 7 (105), Nov 8 (120 -> reaches Germination!)
        for (int i = 1; i <= 7; i++)
        {
            weather.Add(new WeatherRecord { Date = today.AddDays(i), Tmax = 25, Tmin = 15, IsForecast = true });
        }

        var result = GrowthStageService.EvaluateProgress(weather, stages, sowing, today, baseTemperature: 5.0);

        Assert.Equal("Sowing", result.CurrentStage.StageName);
        Assert.Equal(new DateTime(2026, 11, 8), result.EstimatedNextStageDate);
    }

    [Fact]
    public void EvaluateProgress_ProjectsNextStageDate_Using7DayAverageBeyondForecast()
    {
        var sowing = new DateTime(2026, 11, 1);
        var today = new DateTime(2026, 11, 10); // 10 days * 15 = 150 GDD
        var stages = CreateSampleWheatStages(); // Vegetative Growth = 380 GDD (needs 230 more)

        var weather = new List<WeatherRecord>();
        for (int i = 0; i < 10; i++)
        {
            weather.Add(new WeatherRecord { Date = sowing.AddDays(i), Tmax = 25, Tmin = 15 }); // 15/day -> 150 GDD
        }
        // 7 forecast days at 15/day = +105 GDD -> 255 GDD on Nov 17
        for (int i = 1; i <= 7; i++)
        {
            weather.Add(new WeatherRecord { Date = today.AddDays(i), Tmax = 25, Tmin = 15, IsForecast = true });
        }

        // Remaining after Nov 17 = 380 - 255 = 125 GDD. At 15/day -> ceil(125/15) = 9 days -> Nov 26
        var result = GrowthStageService.EvaluateProgress(weather, stages, sowing, today, baseTemperature: 5.0);

        Assert.Equal(new DateTime(2026, 11, 26), result.EstimatedNextStageDate);
    }

    [Fact]
    public async Task CalculateAndSaveEstimateAsync_DoesNotCreateDuplicateForSameDate()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(dbOptions);

        var farmer = new Farmer { Id = "farmer-1", UserName = "f@test.com", Email = "f@test.com", Name = "Test", Location = "Punjab" };
        var crop = new Crop { Id = 1, Name = "Wheat", BaseTemperature = 5.0 };
        crop.CropStages.Add(new CropStage { Id = 1, CropId = 1, StageOrder = 1, StageName = "Sowing", GddThreshold = 0 });
        crop.CropStages.Add(new CropStage { Id = 2, CropId = 1, StageOrder = 2, StageName = "Germination", GddThreshold = 100 });

        var field = new Field { Id = 1, FarmerId = farmer.Id, FieldName = "Plot A", LocationName = "Phagwara", Latitude = 31.2, Longitude = 75.7 };
        var fieldCrop = new FieldCrop { Id = 1, FieldId = 1, CropId = 1, SowingDate = DateTime.UtcNow.Date.AddDays(-5), IsActive = true };

        context.Users.Add(farmer);
        context.Crops.Add(crop);
        context.Fields.Add(field);
        context.FieldCrops.Add(fieldCrop);
        await context.SaveChangesAsync();

        var stubWeather = new StubWeatherService();
        var service = new GrowthStageService(context, stubWeather, NullLogger<GrowthStageService>.Instance);

        await service.CalculateAndSaveEstimateAsync(1);
        await service.CalculateAndSaveEstimateAsync(1);

        var savedCount = await context.GrowthEstimates.CountAsync(g => g.FieldCropId == 1);
        Assert.Equal(1, savedCount);
    }

    private sealed class StubWeatherService : IWeatherService
    {
        public Task<List<GeocodingResultDto>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<GeocodingResultDto>());

        public Task<WeatherSyncResultDto> SyncFieldWeatherAsync(Field field, DateTime fromDate, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            var today = DateTime.UtcNow.Date;
            var records = Enumerable.Range(0, 6)
                .Select(i => new WeatherRecord
                {
                    FieldId = field.Id,
                    Date = fromDate.Date.AddDays(i),
                    Tmax = 26,
                    Tmin = 14,
                    Rainfall = 0
                })
                .Where(w => w.Date <= today)
                .ToList();

            return Task.FromResult(new WeatherSyncResultDto { Records = records });
        }

        public Task<WeatherSummaryDto> GetFieldWeatherSummaryAsync(Field field, CancellationToken cancellationToken = default)
            => Task.FromResult(new WeatherSummaryDto());
    }
}
