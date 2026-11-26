namespace CropProgressMonitor.Models;

/// <summary>
/// Cached daily weather observation or forecast retrieved from Open-Meteo for a field.
/// </summary>
public class WeatherRecord
{
    public int Id { get; set; }

    public int FieldId { get; set; }

    public DateTime Date { get; set; }

    public double Tmax { get; set; }

    public double Tmin { get; set; }

    public double Rainfall { get; set; }

    /// <summary>
    /// Indicates whether this record came from the forecast endpoint (subject to later updates).
    /// </summary>
    public bool IsForecast { get; set; }

    public Field? Field { get; set; }
}
