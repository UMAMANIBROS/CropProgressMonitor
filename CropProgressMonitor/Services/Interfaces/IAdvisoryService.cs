namespace CropProgressMonitor.Services.Interfaces;

public class StageAdvisoryResult
{
    public string StageName { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string WaterAdvice { get; set; } = string.Empty;
    public string NutrientAdvice { get; set; } = string.Empty;
    public string RiskAdvice { get; set; } = string.Empty;
    public List<string> RainfallNotes { get; set; } = new();
    public double Recent3DayRainfallMm { get; set; }
    public double Next3DayForecastRainfallMm { get; set; }
}

public interface IAdvisoryService
{
    Task<StageAdvisoryResult> GetAdvisoryForFieldCropAsync(
        int fieldCropId,
        int stageId,
        string language = "en",
        CancellationToken cancellationToken = default);
}
