using System.ComponentModel.DataAnnotations;

namespace CropProgressMonitor.Models;

/// <summary>
/// Associates a specific crop, optional variety, and sowing date with a farmer's field.
/// </summary>
public class FieldCrop
{
    public int Id { get; set; }

    public int FieldId { get; set; }

    public int CropId { get; set; }

    [StringLength(100)]
    public string? Variety { get; set; }

    [DataType(DataType.Date)]
    public DateTime SowingDate { get; set; }

    public bool IsActive { get; set; } = true;

    public Field? Field { get; set; }

    public Crop? Crop { get; set; }

    public ICollection<GrowthEstimate> GrowthEstimates { get; set; } = new List<GrowthEstimate>();
}
