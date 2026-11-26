using System.ComponentModel.DataAnnotations;

namespace CropProgressMonitor.Models;

/// <summary>
/// Represents a crop species with its thermal-time base temperature (Tbase in Celsius).
/// </summary>
public class Crop
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(-10, 40)]
    public double BaseTemperature { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    public ICollection<CropStage> CropStages { get; set; } = new List<CropStage>();

    public ICollection<FieldCrop> FieldCrops { get; set; } = new List<FieldCrop>();
}
