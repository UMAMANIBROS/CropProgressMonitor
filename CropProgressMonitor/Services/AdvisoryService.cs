using CropProgressMonitor.Data;
using CropProgressMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Services;

/// <summary>
/// Provides stage-specific agronomic guidance in English, Hindi, or Punjabi enriched with rainfall notes.
/// </summary>
public class AdvisoryService : IAdvisoryService
{
    private readonly ApplicationDbContext _dbContext;

    public AdvisoryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StageAdvisoryResult> GetAdvisoryForFieldCropAsync(
        int fieldCropId,
        int stageId,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var lang = NormalizeLanguage(language);

        var fieldCrop = await _dbContext.FieldCrops
            .AsNoTracking()
            .Include(fc => fc.Field)
            .FirstOrDefaultAsync(fc => fc.Id == fieldCropId, cancellationToken);

        var stage = await _dbContext.CropStages
            .AsNoTracking()
            .Include(s => s.Advisories)
            .FirstOrDefaultAsync(s => s.Id == stageId, cancellationToken);

        if (fieldCrop == null || stage == null)
        {
            throw new KeyNotFoundException("Field crop or growth stage not found.");
        }

        var advisory = stage.Advisories.FirstOrDefault();
        var result = new StageAdvisoryResult
        {
            StageName = stage.StageName,
            Language = lang,
            WaterAdvice = SelectLocalized(advisory?.WaterAdvice, advisory?.WaterAdviceHi, advisory?.WaterAdvicePa, lang)
                ?? "Monitor soil moisture at root zone and irrigate according to local crop condition.",
            NutrientAdvice = SelectLocalized(advisory?.NutrientAdvice, advisory?.NutrientAdviceHi, advisory?.NutrientAdvicePa, lang)
                ?? "Follow balanced NPK and micronutrient recommendations for this stage.",
            RiskAdvice = SelectLocalized(advisory?.RiskAdvice, advisory?.RiskAdviceHi, advisory?.RiskAdvicePa, lang)
                ?? "Scout the field weekly for weeds, pests, and disease symptoms."
        };

        var today = DateTime.UtcNow.Date;
        var weatherWindow = await _dbContext.WeatherRecords
            .AsNoTracking()
            .Where(w => w.FieldId == fieldCrop.FieldId && w.Date >= today.AddDays(-2) && w.Date <= today.AddDays(3))
            .ToListAsync(cancellationToken);

        var recentRain = weatherWindow.Where(w => w.Date <= today).Sum(w => w.Rainfall);
        var forecastRain = weatherWindow.Where(w => w.Date > today).Sum(w => w.Rainfall);
        var todayOrTomorrowRain = weatherWindow
            .Where(w => w.Date >= today && w.Date <= today.AddDays(1))
            .Sum(w => w.Rainfall);

        result.Recent3DayRainfallMm = Math.Round(recentRain, 1);
        result.Next3DayForecastRainfallMm = Math.Round(forecastRain, 1);

        if (todayOrTomorrowRain >= 5.0)
        {
            result.RainfallNotes.Add(lang switch
            {
                "hi" => $"आज/कल वर्षा ({todayOrTomorrowRain:F1} मिमी) संभावित है — छिड़काव और उर्वरक डालने को टालें ताकि दवा बह न जाए।",
                "pa" => $"ਅੱਜ/ਕੱਲ੍ਹ ਮੀਂਹ ({todayOrTomorrowRain:F1} ਮਿ.ਮੀ.) ਦੀ ਸੰਭਾਵਨਾ ਹੈ — ਛਿੜਕਾਅ ਅਤੇ ਖਾਦ ਪਾਉਣ ਨੂੰ ਟਾਲੋ।",
                _ => $"Rain recorded or expected within 24 hours ({todayOrTomorrowRain:F1} mm) — delay pesticide spraying or foliar fertilizer application."
            });
        }

        if (recentRain >= 25.0 || forecastRain >= 30.0)
        {
            result.RainfallNotes.Add(lang switch
            {
                "hi" => "भारी वर्षा दर्ज/अनुमानित है — जलभराव रोकने के लिए खेत की मेड़ों और जल निकासी नालियों की तुरंत जांच करें।",
                "pa" => "ਭਾਰੀ ਮੀਂਹ ਦਰਜ/ਅਨੁਮਾਨਿਤ ਹੈ — ਪਾਣੀ ਖੜ੍ਹਨ ਤੋਂ ਰੋਕਣ ਲਈ ਖੇਤ ਦੀਆਂ ਨਿਕਾਸੀ ਨਾਲੀਆਂ ਦੀ ਜਾਂਚ ਕਰੋ।",
                _ => "Heavy rainfall recorded or forecast — inspect drainage channels after rain to prevent root waterlogging."
            });
        }
        else if (recentRain < 2.0 && forecastRain < 2.0)
        {
            result.RainfallNotes.Add(lang switch
            {
                "hi" => "अगले कुछ दिन शुष्क रहने की संभावना है — छिड़काव के लिए मौसम अनुकूल है, पर मिट्टी की नमी की निगरानी करें।",
                "pa" => "ਅਗਲੇ ਕੁਝ ਦਿਨ ਮੌਸਮ ਖੁਸ਼ਕ ਰਹਿਣ ਦੀ ਸੰਭਾਵਨਾ ਹੈ — ਛਿੜਕਾਅ ਲਈ ਸਮਾਂ ਠੀਕ ਹੈ, ਪਰ ਮਿੱਟੀ ਦੀ ਨਮੀ ਦੀ ਜਾਂਚ ਕਰਦੇ ਰਹੋ।",
                _ => "Dry weather window ahead — suitable for field spraying/weeding; monitor topsoil moisture closely."
            });
        }

        return result;
    }

    private static string NormalizeLanguage(string? language)
    {
        return language?.Trim().ToLowerInvariant() switch
        {
            "hi" => "hi",
            "pa" => "pa",
            _ => "en"
        };
    }

    private static string? SelectLocalized(string? en, string? hi, string? pa, string lang)
    {
        return lang switch
        {
            "hi" when !string.IsNullOrWhiteSpace(hi) => hi,
            "pa" when !string.IsNullOrWhiteSpace(pa) => pa,
            _ => en
        };
    }
}
