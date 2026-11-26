using System.ComponentModel.DataAnnotations;
using CropProgressMonitor.Models;

namespace CropProgressMonitor.ViewModels;

public class AdminCropsViewModel
{
    public List<Crop> Crops { get; set; } = new();
    public CropFormViewModel NewCrop { get; set; } = new();
}

public class CropFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Crop Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range(-10, 40)]
    [Display(Name = "Base Temperature (°C)")]
    public double BaseTemperature { get; set; } = 10.0;

    [StringLength(300)]
    public string? Description { get; set; }
}

public class CropStageFormViewModel
{
    public int? StageId { get; set; }
    public int CropId { get; set; }
    public string CropName { get; set; } = string.Empty;

    [Required]
    [Range(1, 50)]
    [Display(Name = "Stage Order")]
    public int StageOrder { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Stage Name")]
    public string StageName { get; set; } = string.Empty;

    [Required]
    [Range(0, 10000)]
    [Display(Name = "Cumulative GDD Threshold")]
    public double GddThreshold { get; set; }

    [Required]
    [StringLength(1000)]
    [Display(Name = "Water Advice (EN)")]
    public string WaterAdvice { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    [Display(Name = "Nutrient Advice (EN)")]
    public string NutrientAdvice { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    [Display(Name = "Risk Advice (EN)")]
    public string RiskAdvice { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Water Advice (Hindi)")]
    public string? WaterAdviceHi { get; set; }

    [StringLength(1000)]
    [Display(Name = "Nutrient Advice (Hindi)")]
    public string? NutrientAdviceHi { get; set; }

    [StringLength(1000)]
    [Display(Name = "Risk Advice (Hindi)")]
    public string? RiskAdviceHi { get; set; }

    [StringLength(1000)]
    [Display(Name = "Water Advice (Punjabi)")]
    public string? WaterAdvicePa { get; set; }

    [StringLength(1000)]
    [Display(Name = "Nutrient Advice (Punjabi)")]
    public string? NutrientAdvicePa { get; set; }

    [StringLength(1000)]
    [Display(Name = "Risk Advice (Punjabi)")]
    public string? RiskAdvicePa { get; set; }
}
