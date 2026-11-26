using System.ComponentModel.DataAnnotations;

namespace CropProgressMonitor.Models;

/// <summary>
/// Represents a phenological growth stage of a crop and its cumulative GDD threshold.
/// </summary>
public class CropStage
{
    public int Id { get; set; }

    public int CropId { get; set; }

    [Range(1, 50)]
    public int StageOrder { get; set; }

    [Required]
    [StringLength(100)]
    public string StageName { get; set; } = string.Empty;

    [Range(0, 10000)]
    public double GddThreshold { get; set; }

    public Crop? Crop { get; set; }

    public ICollection<Advisory> Advisories { get; set; } = new List<Advisory>();
}
