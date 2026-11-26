using System.ComponentModel.DataAnnotations;

namespace CropProgressMonitor.Models;

/// <summary>
/// Represents an agricultural field registered by a farmer with geographic coordinates.
/// </summary>
public class Field
{
    public int Id { get; set; }

    [Required]
    public string FarmerId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FieldName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string LocationName { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    public Farmer? Farmer { get; set; }

    public ICollection<FieldCrop> FieldCrops { get; set; } = new List<FieldCrop>();

    public ICollection<WeatherRecord> WeatherRecords { get; set; } = new List<WeatherRecord>();
}
