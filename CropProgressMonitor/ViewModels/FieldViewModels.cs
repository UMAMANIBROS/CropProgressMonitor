using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CropProgressMonitor.ViewModels;

public class FieldFormViewModel
{
    public int? FieldId { get; set; }
    public int? FieldCropId { get; set; }

    [Required(ErrorMessage = "Field name is required.")]
    [StringLength(100)]
    [Display(Name = "Field Name")]
    public string FieldName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please search and select a location.")]
    [StringLength(200)]
    [Display(Name = "Location Name")]
    public string LocationName { get; set; } = string.Empty;

    [Required]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Required]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "Please select a crop.")]
    [Display(Name = "Crop")]
    public int CropId { get; set; }

    [StringLength(100)]
    [Display(Name = "Variety (Optional)")]
    public string? Variety { get; set; }

    [Required(ErrorMessage = "Sowing date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Sowing Date")]
    public DateTime SowingDate { get; set; } = DateTime.UtcNow.Date.AddDays(-14);

    public List<SelectListItem> AvailableCrops { get; set; } = new();
}
